using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace kvSql.ServiceDefaults.Raft
{
    public enum RaftState
    {
        Follower,
        Candidate,
        Leader
    }

    public enum RaftVoteState
    {
        Voted,//已投票
        Expire,//超时
        Normal//正常
    }

    public class RaftSendSelectMsg
    {
        public int Term { get; set; }
        public int CandidateID { get; set; }
        public int LastLogIndex { get; set; }
        public int LastLogTerm { get; set; }
    }

    public class RaftResponseSelectMsg
    {
        public int Term { get; set; }
        public RaftVoteState VoteState { get; set; }
        public bool GetVote { get; set; }
    }

    public class AppendEntriesArgs
    {
        public int Term { get; set; }
        public int LeaderID { get; set; }
        public int PrevLogIndex { get; set; }
        public int PrevLogTerm { get; set; }
        //public List<RaftLog> Entries { get; set; }
        public int LeaderCommit { get; set; }
    }

    public class AppendEntriesReply
    {

    }

    public class RaftHeartBeatMsg
    {
        public int Term { get; set; }
    }

    public class RaftResponseHeartBeatMsg
    {
        public int Term { get; set; }
        
    }

    public class RaftLog
    {
        public int Term { get; set; }
        public int Index { get; set; }
        public string Command { get; set; }
        public string CommandSteam { get; set; }
    }

    public class RaftLogJson
    {
        public int LastIndex { get; set; }
        public int LastTerm { get; set; }
        public List<RaftLog> Logs { get; set; }
    }

    [JsonSerializable(typeof(RaftSendSelectMsg))]
    public partial class RaftRpcSelectSendJsonContent : JsonSerializerContext
    {
    }

    [JsonSerializable(typeof(RaftResponseSelectMsg))]
    public partial class RaftRpcSelectResponseJsonContent : JsonSerializerContext
    {
    }

    [JsonSerializable(typeof(RaftLog))]
    [JsonSerializable(typeof(RaftLogJson))]
    public partial class RaftRpcLogJsonContent : JsonSerializerContext
    {
    }

    public class Shared<T>(T value) where T : class
    {
        private T? _value = value ?? throw new ArgumentNullException(nameof(value));
        private int _referenceCount = 1;

        public T? Value
        {
            get
            {
                return _referenceCount <= 0 ? throw new ObjectDisposedException(nameof(Shared<T>)) : _value;
            }
        }

        public Shared<T> AddReference()
        {
            Interlocked.Increment(ref _referenceCount);
            return this;
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _referenceCount) == 0)
            {
                Dispose();
            }
        }

        private void Dispose()
        {
            // 释放资源
            _value = null;
        }
    }

    public class IntWrapper(int value)
    {
        public int Value { get; set; } = value;
    }
}
