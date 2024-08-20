using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using kvSql.ServiceDefaults.JumpKV;
using Newtonsoft.Json;

namespace kvSql.ServiceDefaults.Rpc
{
    public class RpcServer : IRpcServer
    {
        private readonly TcpListener _listener;
        private readonly Dictionary<string, Func<object[], Task<object>>> _methods;
        private readonly IKVDataBase _kvDataBase;

        public RpcServer(string ipAddress, int port)
        {
            _listener = new TcpListener(System.Net.IPAddress.Parse(ipAddress), port);
            _methods = new Dictionary<string, Func<object[], Task<object>>>();
            _kvDataBase = new AllTable();
            RpcServerInit();
        }

        public void RegisterMethod(string methodName, Func<object[], Task<object>> method)
        {
            _methods[methodName] = method;
        }

        public async Task StartAsync()
        {
            _listener.Start();

            Console.WriteLine("Rpc server started.");
            while(true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (var networkStream = client.GetStream())
            {
                var buffer = new byte[1024];
                var bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                var requestJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var rpcRequest = JsonConvert.DeserializeObject<RpcRequest>(requestJson);

                var rpcResponse = new RpcResponse();
                if (_methods.TryGetValue(rpcRequest.Method, out var method))
                {
                    try
                    {
                        var result = await method(rpcRequest.Parameters);
                        rpcResponse.Result = result;
                    }
                    catch (Exception ex)
                    {
                        rpcResponse.Error = ex.Message;
                    }
                }
                else
                {
                    rpcResponse.Error = "Method not found";
                }

                var responseJson = JsonConvert.SerializeObject(rpcResponse);
                var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                await networkStream.WriteAsync(responseBytes, 0, responseBytes.Length);
            }
        }

        private void RpcServerInit()
        {
            RegisterMethod("Add", async (parameters) =>
            {
                int a = (int)parameters[0];
                int b = (int)parameters[1];
                return a + b;
            });

            RegisterMethod("CreateKVAsync", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                string val = (string)parameters[2];
                return await _kvDataBase.CreateKVAsync(s, key, val);
            });

            RegisterMethod("AddTableNodeAsync", async (parameters) =>
            {
                string s = (string)parameters[0];
                return await _kvDataBase.AddTableNodeAsync(s);
            });

            RegisterMethod("GetKValAsync", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                return await _kvDataBase.GetKValAsync(s, key);
            });

            RegisterMethod("ChangeValAsync", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                string newVal = (string)parameters[2];
                return await _kvDataBase.ChangeValAsync(s, key, newVal);
            });

            RegisterMethod("SaveDataBaseAsync", async (parameters) =>
            {
                string s = (string)parameters[0];
                await _kvDataBase.SaveDataBaseAsync(s);
                return true;
            });

            RegisterMethod("CreateKVInt64Async", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                long val = (long)parameters[2];
                return await _kvDataBase.CreateKVInt64Async(s, key, val);
            });

            RegisterMethod("AddTableNodeInt64Async", async (parameters) =>
            {
                string s = (string)parameters[0];
                return await _kvDataBase.AddTableNodeInt64Async(s);
            });

            RegisterMethod("GetKValInt64Async", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                return await _kvDataBase.GetKValInt64Async(s, key);
            });

            RegisterMethod("ChangeValInt64Async", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                long newVal = (long)parameters[2];
                return await _kvDataBase.ChangeValInt64Async(s, key, newVal);
            });

            RegisterMethod("SaveDataBaseInt64Async", async (parameters) =>
            {
                string s = (string)parameters[0];
                await _kvDataBase.SaveDataBaseInt64Async(s);
                return true;
            });

            RegisterMethod("AddTableNode", async (parameters) =>
            {
                string s = (string)parameters[0];
                return await _kvDataBase.AddTableNode<string, string>(s);
            });

            RegisterMethod("DeleteTableNode", async (parameters) =>
            {
                string s = (string)parameters[0];
                return await _kvDataBase.DeleteTableNode(s);
            });

            RegisterMethod("GetKValGeneric", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                return await _kvDataBase.GetKValAsync<string, string>(s, key);
            });

            RegisterMethod("CreateKVGeneric", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                string val = (string)parameters[2];
                return await _kvDataBase.CreateKVAsync<string, string>(s, key, val);
            });

            RegisterMethod("ChangeValGeneric", async (parameters) =>
            {
                string s = (string)parameters[0];
                string key = (string)parameters[1];
                string newVal = (string)parameters[2];
                return await _kvDataBase.ChangeValAsync<string, string>(s, key, newVal);
            });
        }
    }
}
