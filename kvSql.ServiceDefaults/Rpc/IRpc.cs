using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kvSql.ServiceDefaults.Rpc
{
    public interface IRpcServer
    {
        public void RegisterMethod(string methodName, Func<object[], Task<object>> method);
    }

    public interface IRpcClient
    {
        public Task<object> CallAsync(string method, params object[] parameters);
    }
}
