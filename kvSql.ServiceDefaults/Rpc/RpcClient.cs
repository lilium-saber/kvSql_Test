using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using kvSql.ServiceDefaults.Raft;

namespace kvSql.ServiceDefaults.Rpc
{
    public class RpcClient : IRpcClient
    {
        private readonly string _ipAddress;
        private readonly int _port;

        public RpcClient(string ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        public async Task<object> CallAsync(string method, params object[] parameters)
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(_ipAddress, _port);
                using (var networkStream = client.GetStream())
                {
                    var rpcRequest = new RpcRequest
                    {
                        Method = method,
                        Parameters = parameters
                    };

                    var requestJson = JsonConvert.SerializeObject(rpcRequest);
                    var requestBytes = Encoding.UTF8.GetBytes(requestJson);
                    await networkStream.WriteAsync(requestBytes, 0, requestBytes.Length);

                    var buffer = new byte[1024];
                    var bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                    var responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var rpcResponse = JsonConvert.DeserializeObject<RpcResponse>(responseJson);

                    if (rpcResponse.Error != null)
                    {
                        throw new Exception(rpcResponse.Error);
                    }

                    return rpcResponse.Result;
                }
            }
        }

        public (bool, string?) RequestVote(string msg)
        {
            if(msg == null)
            {
                return (false, null);
            }
            //如果通信失败，返回false
            string? reply;
            try
            {
                reply = CallAsync("RequestVote", msg).Result.ToString();
            }
            catch (Exception)
            {
                return (false, null);
            }
            return (true, reply);
        }
    }
}

