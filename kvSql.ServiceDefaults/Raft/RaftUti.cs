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

    public class RaftSendSelectMsg
    {
        public int Term { get; set; }
        public int CandidateID { get; set; }
        public int LastLogIndex { get; set; }
        public int LastLogTerm { get; set; }
    }

    public class RaftResponseSelectMsg
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

    [JsonSerializable(typeof(RaftSendSelectMsg))]
    public partial class RaftRpcJsonContent : JsonSerializerContext
    {
    }

    public class Shared<T> where T : class
    {
        private T _value;
        private int _referenceCount;

        public Shared(T value)
        {
            _value = value ?? throw new ArgumentNullException(nameof(value));
            _referenceCount = 1;
        }

        public T Value
        {
            get
            {
                if (_referenceCount <= 0)
                    throw new ObjectDisposedException(nameof(Shared<T>));
                return _value;
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

    public class IntWrapper
    {
        public int Value { get; set; }

        public IntWrapper(int value)
        {
            Value = value;
        }
    }
}
