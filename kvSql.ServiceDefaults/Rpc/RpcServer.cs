using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace kvSql.ServiceDefaults.Rpc
{
    public class RpcServer
    {
        private readonly TcpListener _listener;
        private readonly Dictionary<string, Func<object[], object>> _methods;

        public RpcServer(string ipAddress, int port)
        {
            _listener = new TcpListener(System.Net.IPAddress.Parse(ipAddress), port);
            _methods = new Dictionary<string, Func<object[], object>>();
            _methods["Add"] = this.Add;
        }

        public void RegisterMethod(string methodName, Func<object[], object> method)
        {
            _methods[methodName] = method;
        }

        public object Add(object[] parameters)
        {
            int a = Convert.ToInt32(parameters[0]);
            int b = Convert.ToInt32(parameters[1]);
            return a + b;
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
                        var result = method(rpcRequest.Parameters);
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
    }
}
