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

namespace kvSql.ServiceDefaults.Raft
{
    public class RaftCS
    {
        private List<RaftLog> raftLogs { get; set; }   //日志，添加快照会清空

        public RaftState meState { get; set;}  //节点状态
        public readonly object meMute = new();   //互斥锁
        public readonly int meID;  //节点ID
        public readonly int selectTimeOut = 200;  //选举超时时间ms
        public readonly int heartBeatTimeOut = 200;   //心跳超时时间ms

        //leader
        private List<int> nextIndex { get; set; }  //下一个要发送的日志的索引，每个服务器单独设置
        private List<int> matchIndex { get; set; }  //已知的已经复制到的最高日志索引

        //all node
        private int commitIndex { get; set; }  //已知的已经提交的最高日志索引
        private int lastApplied { get; set; }  //已知的已经应用的最高日志索引
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


        public RaftCS()
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
                foreach(var connectNode in connectNodes)
                {
                    var client = new RpcClient(connectNode.Node.IpAddress, connectNode.Node.Port);
                    allNodes.Add(connectNode.Node.id, client);
                }

                meID = config.meNodeId;
                term = 0;
                commitIndex = 0;
                lastApplied = 0;
                meState = RaftState.Follower;
                raftLogs = [];
                votedFor = -1;
                nextIndex = [];
                matchIndex = [];
                lastIncludedIndex = 0;
                lastIncludedTerm = 0;
                leaderID = -1;
                lastResetHeartBeatTime = DateTime.Now;
                lastResetSelectTime = DateTime.Now;
                //
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
                    suitableSleepTime = new Random().Next(100, heartBeatTimeOut) + raft.lastResetHeartBeatTime.Millisecond - TimeNow.Millisecond;
                }

                if(suitableSleepTime < 1)
                {
                    suitableSleepTime = 1;
                }
                Thread.Sleep(suitableSleepTime);
                if(raft.lastResetHeartBeatTime.Millisecond - TimeNow.Millisecond > 0)
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
                var TimeNow = DateTime.Now.Millisecond;
                var SleepTime = 0;
                lock(meMute)
                {
                    SleepTime = new Random().Next(100, selectTimeOut) + raft.lastResetSelectTime.Millisecond - TimeNow;
                }
                if(SleepTime > 1)
                {
                    Thread.Sleep(SleepTime);
                }

                if(raft.lastResetSelectTime.Millisecond - TimeNow > 0)
                {
                    continue;
                }
                DoSelect(raft);
            }
        }

        public void WriteLogTicker(RaftCS raft)
        {

        }

        public bool SendRaftRequestVote(RaftSendSelectMsg msg, Shared<IntWrapper> voteNum, RaftCS raft, int dstNodeID)
        {
            //调用RequestVote并等待回复
            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = RaftRpcSelectSendJsonContent.Default
            };
            string json = JsonSerializer.Serialize(msg, options);
            (bool ok, string? replyJson)= allNodes[dstNodeID].RequestVote(json);
            if(!ok || replyJson == null || replyJson == "null")
            {
                return false;
            }
            options = new JsonSerializerOptions
            {
                TypeInfoResolver = RaftRpcSelectResponseJsonContent.Default
            };
            RaftResponseSelectMsg reply = JsonSerializer.Deserialize<RaftResponseSelectMsg>(replyJson, options);
            
            Persist();
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

                voteNum.Value.Value++;
                if(voteNum.Value.Value >= ((raft.allNodes.Count / 2) + 1))
                {
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
