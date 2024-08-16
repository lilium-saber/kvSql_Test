using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#include <boost/serialization/string.hpp>
#include <boost/serialization/vector.hpp>
#include <chrono>
#include <cmath>
#include <iostream>
#include <memory>
#include <fstream>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

namespace kvSql.ServiceDefaults.Raft
{
    class Persister {
    private:
        std::mutex m_mtx;
        std::string m_raftState;
        std::string m_snapshot;
          /**
           * m_raftStateFileName: raftState文件名
           */
        const std::string m_raftStateFileName;
          /**
           * m_snapshotFileName: snapshot文件名
           */
        const std::string m_snapshotFileName;
          /**
           * 保存raftState的输出流
           */
        std::ofstream m_raftStateOutStream;
          /**
           * 保存snapshot的输出流
           */
        std::ofstream m_snapshotOutStream;
          /**
           * 保存raftStateSize的大小
           * 避免每次都读取文件来获取具体的大小
           */
        long long m_raftStateSize;

    public:
        void Save(std::string raftstate, std::string snapshot);
        std::string ReadSnapshot();
        void SaveRaftState(const std::string& data);
        long long RaftStateSize();
        std::string ReadRaftState();
        explicit Persister(int me);
          ~Persister();

    private:
        void clearRaftState();
        void clearSnapshot();
        void clearRaftStateAndSnapshot();
    };

    class ApplyMsg {
    public:
        bool CommandValid;
        std::string Command;
        int CommandIndex;
        bool SnapshotValid;
        std::string Snapshot;
        int SnapshotTerm;
        int SnapshotIndex;

    public:
         //两个valid最开始要赋予false！！
         ApplyMsg()
             : CommandValid(false),
               Command(),
               CommandIndex(-1),
               SnapshotValid(false),
               SnapshotTerm(-1),
               SnapshotIndex(-1) {
               };
     };

    class Raft
    {
    private:
        std::mutex m_mtx;
        std::vector<std::shared_ptr< RaftRpc >> m_peers;    //存储其他rpc通信节点
        std::shared_ptr<Persister> m_persister; //rpc数据持久化
        int m_me;   //当前节点id
        int m_currentTerm;  //当前任期，当前的term
        int m_votedFor; //记录当前给谁投票
        std::vector<mprrpc::LogEntry> m_logs;   //日志条目数组
        //  这两条所有节点维护
        int m_commitIndex;  //已知的最大的被提交的日志条目的索引值
        int m_lastApplied;  //报给应用层的最高的已经被应用的日志条目的索引值
        //  这两条只有leader维护
        std::vector<int> m_nextIndex;   //从1开始，对于每一个服务器，需要发送给他的下一个日志条目的索引值
        std::vector<int> m_matchIndex;
        enum Status
        {
            Follower,
            Candidate,
            Leader
        };
        Status m_status;

        std::shared_ptr<LockQueue<ApplyMsg>> applyChan; //client与raft之间的通道

        std::chrono::_V2::system_clock::time_point m_lastResetElectionTime; //选举超时时间

        std::chrono::_V2::system_clock::time_point m_lastResetHearBeatTime; //心跳超时时间，leader用

        // 用于快照，记录最后一个快照的index和term
        int m_lastSnapshotIncludeIndex;
        int m_lastSnapshotIncludeTerm;

    public:
        void AppendEntries1(const mprrpc::AppendEntriesArgs* args, mprrpc::AppendEntriesReply* reply); //日志同步 + 心跳 rpc ，重点关注
        void applierTicker();     //定期向状态机写入日志，非重点函数

        bool CondInstallSnapshot(int lastIncludedTerm, int lastIncludedIndex, std::string snapshot);    //快照相关，非重点
        void doElection();    //发起选举
        void doHeartBeat();    //leader定时发起心跳
        // 每隔一段时间检查睡眠时间内有没有重置定时器，没有则说明超时了
    // 如果有则设置合适睡眠时间：睡眠到重置时间+超时时间
        void electionTimeOutTicker();   //监控是否该发起选举了
        std::vector<ApplyMsg> getApplyLogs();
        int getNewCommandIndex();
        void getPrevLogInfo(int server, int* preIndex, int* preTerm);
        void GetState(int* term, bool* isLeader);  //看当前节点是否是leader
        void InstallSnapshot(const mprrpc::InstallSnapshotRequest* args, mprrpc::InstallSnapshotResponse* reply);
        void leaderHearBeatTicker(); //检查是否需要发起心跳（leader）
        void leaderSendSnapShot(int server);
        void leaderUpdateCommitIndex();  //leader更新commitIndex
        bool matchLog(int logIndex, int logTerm);  //对应Index的日志是否匹配，只需要Index和Term就可以知道是否匹配
        void persist();   //持久化
        void RequestVote(const mprrpc::RequestVoteArgs* args, mprrpc::RequestVoteReply* reply);    //变成candidate之后需要让其他结点给自己投票
        bool UpToDate(int index, int term);   //判断当前节点是否含有最新的日志
        int getLastLogIndex();
        void getLastLogIndexAndTerm(int* lastLogIndex, int* lastLogTerm);
        int getLogTermFromLogIndex(int logIndex);
        int GetRaftStateSize();
        int getSlicesIndexFromLogIndex(int logIndex);   //设计快照之后logIndex不能与在日志中的数组下标相等了，根据logIndex找到其在日志数组中的位置

        bool sendRequestVote(int server, std::shared_ptr<mprrpc::RequestVoteArgs> args, std::shared_ptr<mprrpc::RequestVoteReply> reply, std::shared_ptr<int> votedNum); // 请求其他结点的投票
        bool sendAppendEntries(int server, std::shared_ptr<mprrpc::AppendEntriesArgs> args, std::shared_ptr<mprrpc::AppendEntriesReply> reply, std::shared_ptr<int> appendNums);  //Leader发送心跳后，对心跳的回复进行对应的处理

        //rf.applyChan <- msg //不拿锁执行  可以单独创建一个线程执行，但是为了同意使用std:thread ，避免使用pthread_create，因此专门写一个函数来执行
        void pushMsgToKvServer(ApplyMsg msg);  //给上层的kvserver层发送消息
        void readPersist(std::string data);
        std::string persistData();
        void Start(Op command, int* newLogIndex, int* newLogTerm, bool* isLeader);   // 发布发来一个新日志
        // 即kv-server主动发起，请求raft（持久层）保存snapshot里面的数据，index是用来表示snapshot快照执行到了哪条命令
        void Snapshot(int index, std::string snapshot);

        void init(std::vector<std::shared_ptr< RaftRpc >> peers, int me, std::shared_ptr<Persister> persister, std::shared_ptr<LockQueue<ApplyMsg>> applyCh);
    }


    //初始化
    void Raft::init(std::vector<std::shared_ptr< RaftRpc >> peers, int me, std::shared_ptr<Persister> persister, std::shared_ptr<LockQueue<ApplyMsg>> applyCh)
	{
		
	}

}
