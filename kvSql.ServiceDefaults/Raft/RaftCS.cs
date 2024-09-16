using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using kvSql.ServiceDefaults.Rpc;
using kvSql.ServiceDefaults.JumpKV;

namespace kvSql.ServiceDefaults.Raft
{
    public class RaftCS
    {
        private static RaftCS? instance; //单例
        private readonly static object lockObj = new();  //单例锁

        public List<RaftLog> raftLogs { get; set; }   //日志，添加快照会清空
        public RaftState meState { get; set;}  //节点状态
        public readonly object meMute = new();   //互斥锁
        public readonly int meID;  //节点ID
        public readonly int selectTimeOut = 2000;  //选举超时时间ms
        public readonly int heartBeatTimeOut = 2000;   //心跳超时时间ms

        //leader
        private Dictionary<int, int> nextIndex { get; set; }  //下一个要发送的日志的索引，每个服务器单独设置，默认为已知的最高日志索引+1
        private Dictionary<int, int> matchIndex { get; set; }  //已知的已经复制到的最高日志索引

        //all node
        private double commitIndex { get; set; }  //已知的已经提交的最高日志索引
        private double lastApplied { get; set; }  //已知的已经应用的最高日志索引
        public int term { get; set; }  //当前任期

        //rpc使用
        private readonly Dictionary<int, RpcClient> allNodes;
        public int leaderTerm { get; set; }    //已知领导者最新任期,开始为0
        public int votedFor { get; set; }  //已投票给谁
        public int leaderID { get; set; }  //领导者ID
        
        public DateTime lastResetHeartBeatTime { get; set; }  //上次重置心跳时间
        public DateTime lastResetSelectTime { get; set; }  //上次重置选举时间

        //快照
        private int lastIncludedIndex { get; set; }  //快照中包含的最后一个日志条目的索引
        private int lastIncludedTerm { get; set; }  //快照中包含的最后一个日志条目的任期
        
        //kvSql
        public readonly IKVDataBase kvSql;
        private readonly Dictionary<string, Func<object[], Task<object>>> methods;

        private RaftCS()
        {
            lock(meMute)
            {
                string relativePath = Path.Combine($"RaftSetting.json");
                string? solutionPath = null;
                try
                {
                    var currentDirectory = Directory.GetParent(Directory.GetCurrentDirectory());
                    var parentDirectory = currentDirectory?.Parent;
                    var grandParentDirectory = parentDirectory?.Parent;

                    if (grandParentDirectory != null)
                    {
                        solutionPath = grandParentDirectory.FullName;
                    }
                    else
                    {
                        throw new NotSupportedException("无法获取解决方案路径，目录层级不足。");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                string filePath = solutionPath == null ? 
                    relativePath : 
                    Path.Combine(solutionPath, relativePath);
                var config = ConfigLoader.LoadConfig(filePath);
                var connectNodes = config.ConnectNodes;
                allNodes = [];
                nextIndex = [];
                matchIndex = [];
                foreach(var connectNode in connectNodes)
                {
                    var client = new RpcClient(connectNode.Node.IpAddress, connectNode.Node.Port);
                    allNodes.Add(connectNode.Node.id, client);
                    nextIndex.Add(connectNode.Node.id, 0);
                    matchIndex.Add(connectNode.Node.id, 0);
                }
                meID = config.meNodeId;
                term = 0;
                commitIndex = 0;
                lastApplied = 0;
                meState = RaftState.Follower;
                raftLogs = [];
                votedFor = -1;
                lastIncludedIndex = 0;
                lastIncludedTerm = 0;
                leaderID = -1;
                lastResetHeartBeatTime = DateTime.Now;
                lastResetSelectTime = DateTime.Now;
                //
                methods = [];
                kvSql = AllTable.GetInstance(); 
                MethonInit();
            }
            
            Thread t1 = new(() => LeaderTimeOutTicker(this))
            {
                IsBackground = true
            };
            Thread t2 = new(() => SelectTimeOutTicker(this))
            {
                IsBackground = true
            };
            Thread t3 = new(() => WriteLogTicker(this))
            {
                IsBackground = true
            };
            t1.Start();
            t2.Start();
            t3.Start();
        }

        //单例
        public static RaftCS GetInstance() 
        {
            if(instance == null)
            {
                lock(lockObj)
                {
                    instance ??= new RaftCS();
                }
            }
            return instance;
        }

        //持久化
        public async void Persist()
        {
            string json;
            DateTime time = DateTime.Now;
            string relativePath = Path.Combine("SnapFile", $"{time}.json");
            RaftLogJson raftLogJson = new()
            {
                LastIndex = GetLastLogIndex(),
                LastTerm = GetLastLogTerm(),
                Logs = []
            };
            foreach(var log in raftLogs)
            {
                raftLogJson.Logs.Add(log);
            }
            lock(meMute)
            {
                lastIncludedIndex = GetLastLogIndex();
                lastIncludedTerm = GetLastLogTerm();
                raftLogs.Clear();
            }
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = RaftRpcLogJsonContent.Default
            };
            json = JsonSerializer.Serialize(raftLogJson, options);
            await File.WriteAllTextAsync(relativePath, json);
        }

        public int GetLastLogIndex()
        {
            if(raftLogs.Count == 0)
            {
                return lastIncludedIndex;
            }
            else
            {
                return raftLogs[^1].Index;
            }
        }

        public int GetLastLogTerm()
        {
            if(raftLogs.Count == 0)
            {
                return lastIncludedTerm;
            }
            else
            {
                return raftLogs[^1].Term;
            }
        }

        public void LeaderTimeOutTicker(RaftCS raft)
        {
            while(true)
            {
                var TimeNow = DateTime.Now;
                var suitableSleepTime = 0;
                lock(meMute)
                {
                    suitableSleepTime = new Random().Next(100, heartBeatTimeOut) + (int)(raft.lastResetHeartBeatTime - TimeNow).TotalMilliseconds;
                }

                if(suitableSleepTime < 1)
                {
                    suitableSleepTime = 1;
                }
                Thread.Sleep(suitableSleepTime);
                if((int)(raft.lastResetHeartBeatTime - TimeNow).TotalMilliseconds > 0)
                {
                    continue;
                }
                DoHeartBeat(raft);
            }
        }

        public void SelectTimeOutTicker(RaftCS raft)
        {
            while(true)
            {
                var TimeNow = DateTime.Now;
                var SleepTime = 0;
                lock(meMute)
                {
                    SleepTime = new Random().Next(100, selectTimeOut) + (int)(raft.lastResetSelectTime - TimeNow).TotalMilliseconds;
                }
                if(SleepTime > 1)
                {
                    Thread.Sleep(SleepTime);
                }

                if((int)(raft.lastResetSelectTime - TimeNow).TotalMilliseconds > 0)
                {
                    continue;
                }
                DoSelect(raft);
            }
        }

        public void WriteLogTicker(RaftCS raft)
        {
            while(true)
            {
                Thread.Sleep(10);
                raft.commitIndex = raft.GetLastLogIndex();
                lock(raft.meMute)
                {
                    //Console.WriteLine($"WriteLogTicker {raft.lastApplied}");
                    if(raft.raftLogs.Count == 0)
                    {
                        continue;
                    }
                    if(raft.lastApplied >= raft.commitIndex)
                    {
                        commitIndex = raft.GetLastLogIndex();
                        continue;
                    }
                    if(raft.lastApplied < raft.commitIndex)
                    {
                        // while(raft.lastApplied < raft.raftLogs[(int)raft.lastApplied].Index)
                        // {
                        //     raft.lastApplied++;
                        // }
                        // if(raft.raftLogs[(int)raft.lastApplied].Index != raft.lastApplied)
                        // {
                        //     raft.lastApplied++;
                        //     continue;
                        // }
                        raft.methods[raft.raftLogs[(int)raft.lastApplied].Method](raft.raftLogs[(int)raft.lastApplied].Parameters);
                        raft.lastApplied++;
                    }
                }
                //提交日志
                //待处理：持久化。读取命令不应该持久化，只有写入命令才应该持久化
                //处理读取命令绕过日志

            }
        }

        public bool SendRaftRequestVote(RaftSendSelectMsg msg, Shared<IntWrapper> voteNum, RaftCS raft, int dstNodeID)
        {
            Console.WriteLine($"SendRaftRequestVote {dstNodeID}");
            //调用RequestVote并等待回复
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = RaftRpcSelectSendJsonContent.Default
            };
            string json = JsonSerializer.Serialize(msg, options);
            (bool ok, string? replyJson)= allNodes[dstNodeID].RequestVote(json);
            Console.WriteLine($"SendRaftRequestVote {dstNodeID} replyJson {replyJson}");
            if(!ok || replyJson == null || replyJson == "null")
            {
                return false;
            }
            options = new JsonSerializerOptions
            {
                TypeInfoResolver = RaftRpcSelectResponseJsonContent.Default
            };
            RaftResponseSelectMsg reply = JsonSerializer.Deserialize<RaftResponseSelectMsg>(replyJson, options);
            if(reply == null)
            {
                return false;
            }
            //Persist();
            lock(meMute)
            {
                if(reply.Term > raft.term)
                {
                    raft.term = reply.Term;
                    raft.meState = RaftState.Follower;
                    raft.votedFor = -1;
                    //持久化

                    return true;
                }
                else if(reply.Term < raft.term)
                {
                    return true;
                }
                if(!reply.GetVote)
                {
                    return true;
                }
                Console.WriteLine($"{raft.meID} get vote");
                voteNum.Value.Value++;
                if(voteNum.Value.Value >= ((raft.allNodes.Count / 2) + 1))
                {
                    Console.WriteLine($"{raft.meID} become leader");
                    voteNum.Value.Value = 0;
                    raft.meState = RaftState.Leader;
                    raft.leaderID = raft.meID;
                    raft.leaderTerm = raft.term;
                    for(int i = 0;i < raft.nextIndex.Count; i++)
                    {
                        raft.nextIndex[i] = raft.GetLastLogIndex() + 1;
                        raft.matchIndex[i] = 0;
                    }

                    Thread t = new(() => DoHeartBeat(raft))
                    {
                        IsBackground = true
                    };
                    t.Start();
                }
                return true;
            }
        }

        public void SendRaftHeartBeat(RaftHeartBeatMsg msg, RaftCS raft, int nodeID)
        {
            Console.WriteLine($"SendRaftHeartBeat {nodeID}");
            string json;
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = RaftRpcHeartBeatSendJsonContent.Default
            };
            json = JsonSerializer.Serialize(msg, options);
            (bool ok, string? replyJson) = raft.allNodes[nodeID].HeartBeat(json);
            if(!ok || replyJson == null || replyJson == "null")
            {
                return;
            }
            options = new JsonSerializerOptions
            {
                TypeInfoResolver = RaftRpcHeartBeatLogSendJsonContent.Default
            };
            RaftResponseHeartBeatMsg reply = JsonSerializer.Deserialize<RaftResponseHeartBeatMsg>(replyJson, options);
            if(reply == null)
            {
                return;
            }
            if(reply.HBSuccess)
            {
                return;
            }
            //
        }

        public void SendRaftHeartBeatLog(RaftHeartBeatLogMsg msg, RaftCS raft, int nodeID)
        {
            Console.WriteLine($"SendRaftHeartBeatLog {nodeID}");
            string json;
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = RaftRpcHeartBeatLogSendJsonContent.Default
            };
            json = JsonSerializer.Serialize(msg, options);
            (bool ok, string? replyJson) = allNodes[nodeID].HeartBeatLog(json);
            if(!ok || replyJson == null || replyJson == "null")
            {
                return;
            }
            options = new JsonSerializerOptions
            {
                TypeInfoResolver = RaftRpcHeartBeatLogSendJsonContent.Default
            };
            RaftResponseHeartBeatLogMsg reply = JsonSerializer.Deserialize<RaftResponseHeartBeatLogMsg>(replyJson, options);
            if(reply == null)
            {
                return;
            }

            lock(raft.meMute)
            {
                if(reply.LogSuccess)
                {
                    raft.nextIndex[nodeID] = raft.nextIndex[nodeID] + 1;
                    raft.matchIndex[nodeID] = raft.nextIndex[nodeID] + 1;
                    return;
                }
                else
                {
                    if(reply.NodeLogLastIndex != -1)
                    {
                        raft.nextIndex[nodeID] = reply.NodeLogLastIndex + 1;
                    }
                    return;
                }
            }

        }

        public void DoSelect(RaftCS raft)
        {
            lock(raft.meMute)
            {
                if(raft.meState == RaftState.Leader)
                {
                    return;
                }
                Console.WriteLine("DoSelect");

                raft.meState = RaftState.Candidate;
                raft.term++;
                raft.votedFor = meID;
                var voteNum = new Shared<IntWrapper>(new IntWrapper(1));

                //持久化

                raft.lastResetSelectTime = DateTime.Now;
                if(raft.allNodes.Count == 1)
                {
                    raft.meState = RaftState.Leader;
                    raft.leaderID = raft.meID;
                    raft.leaderTerm = raft.term;
                    return;
                }
                //Rpc
                foreach(var node in allNodes)
                {
                    if(node.Key == meID)
                    {
                        continue;
                    }
                    var msg = new RaftSendSelectMsg
                    {
                        Term = raft.term,
                        CandidateID = raft.meID,
                        LastLogIndex = GetLastLogIndex(),
                        LastLogTerm = GetLastLogTerm()
                    };
                    //SendRaftRequestVote();
                    Thread t = new(() => 
                    {
                        var localVoteNum = voteNum.AddReference();
                        try
                        {
                            SendRaftRequestVote(msg, voteNum, raft, node.Key);
                        }
                        finally
                        {
                            localVoteNum.Release();
                        }
                    })
                    {
                        IsBackground = true
                    };
                    t.Start();
                }
                //
            }
        }

        //重新设置心跳，去掉cpp部分代码，简易实现
        public void DoHeartBeat(RaftCS raft)
        {
            lock(raft.meMute)
            {
                if(raft.meState == RaftState.Leader)
                {
                    Console.WriteLine("DoHeartBeat");
                    raft.lastResetHeartBeatTime = DateTime.Now;
                    int meLastLogIndex = GetLastLogIndex();
                    foreach(var node in raft.allNodes)
                    {
                        if(node.Key == raft.meID)
                        {
                            continue;
                        }
                        int nodeID = node.Key;
                        if(raft.nextIndex[nodeID] - 1 == meLastLogIndex)
                        {
                            //发送心跳
                            RaftHeartBeatMsg msg = new()
                            {
                                Term = raft.term,
                                LeaderID = raft.meID,
                                NewIndex = meLastLogIndex
                            };
                            Thread t = new(() => 
                            {
                                try
                                {
                                    SendRaftHeartBeat(msg, raft, nodeID);
                                }
                                catch(Exception e)
                                {
                                    Console.WriteLine(e.Message);
                                }
                            })
                            {
                                IsBackground = true
                            };
                            t.Start();
                        }
                        else
                        {
                            //发送日志，暂时不处理快照的实现，默认全部都在内存中
                            RaftHeartBeatLogMsg msg = new()
                            {
                                Term = raft.term,
                                LeaderID = raft.meID,
                                Log = raft.raftLogs
                                    .FirstOrDefault(x => x.Index == raft.nextIndex[nodeID])
                            };

                            Thread t = new(() =>
                            {
                                try
                                {
                                    SendRaftHeartBeatLog(msg, raft, nodeID);
                                }
                                catch(Exception e)
                                {
                                    Console.WriteLine(e.Message);
                                }
                            })
                            {
                                IsBackground = true
                            };
                            t.Start();
                        }
                    }
                }
            }
        }

        public void RegisterRaftMethod(string methodName, Func<object[], Task<object>> method)
        {
            methods.Add(methodName, method);
        }

        public void MethonInit()
        {
            RegisterRaftMethod("CreateKVAsync", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                string val = (string)parameters[2];
                return await kvSql.CreateKVAsync(s, key, val);
            });

            RegisterRaftMethod("AddTableNodeAsync", async (parameters) =>
            {
                string s = (string)parameters[0];
                return await kvSql.AddTableNodeAsync(s);
            });

            RegisterRaftMethod("GetKValAsync", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                return await kvSql.GetKValAsync(s, key);
            });

            RegisterRaftMethod("ChangeValAsync", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                string newVal = (string)parameters[2];
                return await kvSql.ChangeValAsync(s, key, newVal);
            });

            RegisterRaftMethod("SaveDataBaseAsync", async (parameters) =>
            {
                string s = (string)parameters[0];
                await kvSql.SaveDataBaseAsync(s);
                return true;
            });

            RegisterRaftMethod("CreateKVInt64Async", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                long val = (long)parameters[2];
                Console.WriteLine($"CreateKVInt64Async {s} {key} {val}");
                return await kvSql.CreateKVInt64Async(s, key, val);
            });

            RegisterRaftMethod("AddTableNodeInt64Async", async (parameters) =>
            {
                string s = (string)parameters[0];
                Console.WriteLine($"AddTableNodeInt64Async {s}");
                return await kvSql.AddTableNodeInt64Async(s);
            });

            RegisterRaftMethod("GetKValInt64Async", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                return await kvSql.GetKValInt64Async(s, key);
            });

            RegisterRaftMethod("ChangeValInt64Async", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                long newVal = (long)parameters[2];
                return await kvSql.ChangeValInt64Async(s, key, newVal);
            });

            RegisterRaftMethod("SaveDataBaseInt64Async", async (parameters) =>
            {
                string s = (string)parameters[0];
                await kvSql.SaveDataBaseInt64Async(s);
                return true;
            });

            RegisterRaftMethod("AddTableNode", async (parameters) =>
            {
                string s = (string)parameters[0];
                return await kvSql.AddTableNode<string, string>(s);
            });

            RegisterRaftMethod("DeleteTableNode", async (parameters) =>
            {
                string s = (string)parameters[0];
                return await kvSql.DeleteTableNode(s);
            });

            RegisterRaftMethod("GetKValGeneric", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                return await kvSql.GetKValAsync<string, string>(s, key);
            });

            RegisterRaftMethod("CreateKVGeneric", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                string val = (string)parameters[2];
                return await kvSql.CreateKVAsync<string, string>(s, key, val);
            });

            RegisterRaftMethod("ChangeValGeneric", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                string newVal = (string)parameters[2];
                return await kvSql.ChangeValAsync<string, string>(s, key, newVal);
            });
        }

        /*
        public void DoHeartBeat(RaftCS raft)
        {
            lock(raft.meMute)
            {
                if(raft.meState == RaftState.Leader)
                {
                    Shared<IntWrapper> commitNum = new(new IntWrapper(1));
                    for(int i = 0; i < raft.allNodes.Count; i++)
                    {
                        if(i == raft.meID)
                        {
                            continue;
                        }
                        if(raft.nextIndex[i] <= raft.lastIncludedIndex)
                        {

                            continue;
                        }
                        int preLogIndex = -1;
                        int PrevLogTerm = -1;
                        GetPrevLogInfo(raft, i, ref preLogIndex, ref PrevLogTerm);

                        Shared<AppendEntriesArgs> args = new(new AppendEntriesArgs
                        {
                            Term = raft.term,
                            LeaderID = raft.meID,
                            PrevLogIndex = preLogIndex,
                            PrevLogTerm = PrevLogTerm,
                            //Entries = raftLogs.GetRange(preLogIndex + 1, raftLogs.Count - preLogIndex - 1),
                            LeaderCommit = raft.commitIndex
                        });
                        if(preLogIndex != raft.lastIncludedIndex)
                        {
                            for(int j = GetSlicesIndexFromLogIndex(preLogIndex) + 1; j < raft.raftLogs.Count; j++)
                            {
                                
                            }
                        }
                        else
                        {
                            foreach(var log in raft.raftLogs)
                            {
                            }
                        }
                        int lastLogIndex = GetLastLogIndex();
                        Shared<AppendEntriesReply> reply = new(new AppendEntriesReply());

                        Thread t = new(() =>
                        {
                            reply.AddReference();
                            args.AddReference();
                            try
                            {
                                SendAppendEntries();
                            }
                            finally
                            {
                                reply.Release();
                                args.Release();
                            }
                        })
                        {
                            IsBackground = true
                        };
                        t.Start();
                    }
                    raft.lastResetHeartBeatTime = DateTime.Now;
                }
            }
        }

        public void GetPrevLogInfo(RaftCS raft, int i, ref int preLogIndex, ref int PrevLogTerm)
        {

        }

        public int GetSlicesIndexFromLogIndex(int logIndex)
        {
            return 0;
        }

        public bool SendAppendEntries()
        {
            return false;
        }
        */

    }

}
