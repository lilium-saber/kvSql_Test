using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kvSql.ServiceDefaults.Raft
{
    public enum RaftState
    {
        Follower,
        Candidate,
        Leader
    }

    public class RaftMsg
    {

    }

    public class RaftLog
    {
        public int index { get; set; }
        public int commandNum { get; set; }
        public string commandSteam { get; set; }
    }
}
