using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using kvSql.ServiceDefaults.Raft;

namespace kvSql.ServiceDefaults.Rpc
{
    public class RpcClient(string ipAddress, int port) : IRpcClient
    {
        private readonly string _ipAddress = ipAddress;
        private readonly int _port = port;
        private readonly int _maxRetry = 3;
        private readonly int _timeoutMilliseconds = 10000;

        public async Task<object> CallAsync(string method, params object[]? parameters)
        {
            for (int attempt = 0; attempt < _maxRetry; attempt++)
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(_timeoutMilliseconds); //超时计时器
                try
                {
                    await client.ConnectAsync(_ipAddress, _port);
                    using var networkStream = client.GetStream();
                    var rpcRequest = new RpcRequest
                    {
                        Method = method,
                        Parameters = parameters
                    };

                    var requestJson = JsonConvert.SerializeObject(rpcRequest);
                    var requestBytes = Encoding.UTF8.GetBytes(requestJson);
                    await networkStream.WriteAsync(requestBytes, cts.Token);

                    var buffer = new byte[1024];
                    var bytesRead = await networkStream.ReadAsync(buffer, cts.Token);
                    var responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var rpcResponse = JsonConvert.DeserializeObject<RpcResponse>(responseJson);

                    if (rpcResponse.Error != null)
                    {
                        throw new Exception(rpcResponse.Error);
                    }

                    return rpcResponse.Result;
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Attempt {attempt + 1} timed out.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Attempt {attempt + 1} failed: {ex.Message}");
                }

                if (attempt < _maxRetry - 1)
                {
                    await Task.Delay(1000); // 等待一段时间后重试
                }
            }

            throw new Exception("All retry attempts failed.");
        }
        public (bool, string?) RequestVote(string msg)
        {
            Console.WriteLine($"RequestVote {_ipAddress}:{_port} {msg}");
            if(msg == null)
            {
                return (false, null);
            }
            //如果通信失败，返回false
            string? reply;
            try
            {
                reply = CallAsync("RequestVote", msg).Result?.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine($"RequestVote {_ipAddress}:{_port} {e} failed");
                return (false, null);
            }
            return (true, reply);
        }

        public (bool, string?) HeartBeat(string msg)
        {
            if(msg == null)
            {
                return (false, null);
            }
            //如果通信失败，返回false
            string? reply;
            try
            {
                reply = CallAsync("HeartBeat", msg).Result?.ToString();
            }
            catch (Exception)
            {
                return (false, null);
            }
            return (true, reply);
        }

        public (bool, string?) HeartBeatLog(string msg)
        {
            if(msg == null)
            {
                return (false, null);
            }
            //如果通信失败，返回false
            string? reply;
            try
            {
                reply = CallAsync("HeartBeatLog", msg).Result?.ToString();
            }
            catch (Exception)
            {
                return (false, null);
            }
            return (true, reply);
        }
    }
}

