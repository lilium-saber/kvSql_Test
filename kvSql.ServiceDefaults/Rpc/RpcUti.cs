using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace kvSql.ServiceDefaults.Rpc
{
    public class RpcRequest
    {
        public required string Method { get; set; }
        public object[]? Parameters { get; set; }
    }

    public class RpcResponse
    {
        public object? Result { get; set; }
        public string? Error { get; set; }
    }


    public class RpcRaftRequest
    {
        public int Index { get; set; }
        public required string MethodName { get; set; }
        public object[] Data { get; set; }
        public int Term { get; set; }
    }

    public class RpcRaftResponse
    {
        public object Result { get; set; }
        public int Term { get; set; }
    }

    [JsonSerializable(typeof(RpcRequest))]
    public partial class RpcRequestContext : JsonSerializerContext
    {
    }

    [JsonSerializable(typeof(RpcResponse))]
    public partial class RpcResponseContext : JsonSerializerContext
    {
    }

    public class Defer(Action action) : IDisposable
    {
        private readonly Action _action = action;

        public void Dispose()
        {
            _action();
        }
    }
}
