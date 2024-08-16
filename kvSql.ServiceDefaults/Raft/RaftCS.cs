using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using kvSql.ServiceDefaults.Raft;

namespace kvSql.ServiceDefaults.Raft
{
    public class RaftCS
    {
        private Mutex meMute;   //互斥锁
        private RaftState meState { get; set;}  //节点状态
        private List<Raft.RaftLog> raftLogs { get; set; }   //日志

        public readonly int meID;  //节点ID
        public readonly int selectTimeOut = 150;  //选举超时时间ms
        public readonly int heartBeatTimeOut = 100;   //心跳超时时间ms

        //leader
        private int nextIndex { get; set; }  //下一个要发送的日志的索引
        private int matchIndex { get; set; }  //已知的已经复制到的最高日志索引

        //follower
        private int commitIndex { get; set; }  //已知的已经提交的最高日志索引
        private int lastApplied { get; set; }  //已知的已经应用的最高日志索引

        //rpc使用
        private int leaderTerm { get; set; }    //已知领导者最新任期,开始为0
        private int votedFor { get; set; }  //已投票给谁
        private int leaderID { get; set; }  //领导者ID


        public RaftCS(int id)
        {
            meID = id;
            meState = RaftState.Follower;
            
        }
    }
}
