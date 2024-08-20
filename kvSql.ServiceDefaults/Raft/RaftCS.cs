using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using kvSql.ServiceDefaults.Raft;
using kvSql.ServiceDefaults.Rpc;

namespace kvSql.ServiceDefaults.Raft
{
    public class RaftCS
    {
        private readonly object meMute = new object();   //互斥锁
        private RaftState meState { get; set;}  //节点状态
        private List<Raft.RaftLog> raftLogs { get; set; }   //日志
        private Timer? timerSelect;  //选举定时器
        private Timer? timerHeartBeat;  //心跳定时器

        public readonly int meID;  //节点ID
        public readonly int selectTimeOut = 200;  //选举超时时间ms
        public readonly int heartBeatTimeOut = 100;   //心跳超时时间ms

        //leader
        private int nextIndex { get; set; }  //下一个要发送的日志的索引
        private int matchIndex { get; set; }  //已知的已经复制到的最高日志索引

        //all node
        private int commitIndex { get; set; }  //已知的已经提交的最高日志索引
        private int lastApplied { get; set; }  //已知的已经应用的最高日志索引
        private int term { get; set; }  //当前任期

        //rpc使用
        private readonly Dictionary<int, RpcClient> allNodes;
        private int leaderTerm { get; set; }    //已知领导者最新任期,开始为0
        private int votedFor { get; set; }  //已投票给谁
        private int leaderID { get; set; }  //领导者ID
        
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
                string solutionPath = "";
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
                string filePath = Path.Combine(solutionPath, relativePath);
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

        public void LeaderTimeOutTicker(RaftCS raft)
        {

        }

        public void SelectTimeOutTicker(RaftCS raft)
        {
            while(true)
            {
                var TimeNow = 0;
                var SleepTime = 0;
                lock(meMute)
                {
                    TimeNow = DateTime.Now.Millisecond;
                    SleepTime = new Random().Next(100, 300) + raft.lastResetSelectTime.Millisecond - TimeNow;
                }
                if(SleepTime > 1)
                {
                    Thread.Sleep(SleepTime);
                }

                if(raft.lastResetSelectTime.Millisecond - TimeNow > 0)
                {
                    continue;
                }
                DoSelect();
            }
        }

        public void WriteLogTicker(RaftCS raft)
        {

        }

        public void SendRaftRequestVote(RaftSendSelectMsg msg, Shared<IntWrapper> voteNum, RaftCS raft, int dstNodeID)
        {
            //调用RequestVote并等待回复


            var options = new JsonSerializerOptions
            {
                TypeInfoResolver = RaftRpcJsonContent.Default
            };
            string json = JsonSerializer.Serialize(msg, options);
            
        }

        public void DoSelect()
        {
            lock(meMute)
            {
                if(meState == RaftState.Leader)
                {
                    return;
                }
                Console.WriteLine("DoSelect");

                meState = RaftState.Candidate;
                term++;
                votedFor = meID;
                var voteNum = new Shared<IntWrapper>(new IntWrapper(1));

                //持久化

                lastResetSelectTime = DateTime.Now;
                //Rpc
                foreach(var node in allNodes)
                {
                    if(node.Key == meID)
                    {
                        continue;
                    }
                    var msg = new RaftSendSelectMsg
                    {
                        Term = term,
                        CandidateID = meID,
                        LastLogIndex = raftLogs.Count - 1,
                        LastLogTerm = raftLogs[^1].Term
                    };
                    //SendRaftRequestVote();
                    Thread t = new(() => 
                    {
                        var localVoteNum = voteNum.AddReference();
                        try
                        {
                            SendRaftRequestVote(msg, voteNum, this, node.Key);
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

    }

}
