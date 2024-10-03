using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kvSql.ServiceDefaults.Rpc
{
    public interface IRpcServer
    {
        void RegisterMethod(string methodName, Func<object[], Task<object>> method);
        Task StartAsync();
    }

    public interface IRpcClient
    {
        Task<object> CallAsync(string method, params object[] parameters);
        (bool, string?) RequestVote(string msg);
        (bool, string?) HeartBeat(string msg);
        (bool, string?) HeartBeatLog(string msg);
    }
}
