// See https://aka.ms/new-console-template for more information

using kvSql.ServiceDefaults;
using kvSql.ServiceDefaults.JumpKV;
using kvSql.ServiceDefaults.Rpc;
using System.Diagnostics;


async Task StartServer()
{
    string relativePath = Path.Combine($"RaftSetting.json");
    string solutionPath = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
    string filePath = Path.Combine(solutionPath, relativePath);
    var config = ConfigLoader.LoadConfig(filePath);
    var server = new RpcServer(config.LocalServer.IpAddress, config.LocalServer.Port);

    await server.StartAsync();
}

async Task StartClient()
{
    string relativePath = Path.Combine($"RaftSetting.json");
    string solutionPath = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
    string filePath = Path.Combine(solutionPath, relativePath);
    var config = ConfigLoader.LoadConfig(filePath);
    //var client = new RpcClient(config.LocalClient.IpAddress, config.LocalClient.Port);
    var connectNodes = config.ConnectNodes;
    foreach(var connectNode in connectNodes)
    {
        var client = new RpcClient(connectNode.Node.IpAddress, connectNode.Node.Port);
        var result = await client.CallAsync("AddTableNodeInt64Async", "test");
        Console.WriteLine($"Result: {result}");
        for(int i = 0; i < 1e3; i++)
        {
            Random random = new();
            int k = random.Next(0, (int)1e4);
            var result1 = await client.CallAsync("CreateKVInt64Async", "test", k.ToString(), k);
            Console.WriteLine($"Result set: {result1}");
        }
        Thread.Sleep(30000);
        for(int i = 0; i < 1e4; i++)
        {
            Random random = new();
            int k = random.Next(0, (int)1e4);
            var result1 = await client.CallAsync("GetKValInt64Async", "test", k.ToString());
            Console.WriteLine($"Result get: {result1} {k}");
        }
    }
}

async Task MainAsync()
{
    var serverTask = StartServer();

    // 等待服务端启动
    await Task.Delay(2000);
    Console.WriteLine("delay end");
    await StartClient();

    // 等待服务端任务完成（通常不会发生，因为服务器会一直运行）
    await serverTask;
}

await MainAsync();
/*
async Task StartServer()
{
    string relativePath = Path.Combine($"RaftSetting.json");
    string solutionPath = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
    string filePath = Path.Combine(solutionPath, relativePath);
    var config = ConfigLoader.LoadConfig(filePath);
    var server = new RpcServer(config.LocalServer.IpAddress, config.LocalServer.Port);

    await server.StartAsync();
}

async Task StartClient()
{
    string relativePath = Path.Combine($"RaftSetting.json");
    string solutionPath = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
    string filePath = Path.Combine(solutionPath, relativePath);
    var config = ConfigLoader.LoadConfig(filePath);
    //var client = new RpcClient(config.LocalClient.IpAddress, config.LocalClient.Port);
    var connectNodes = config.ConnectNodes;
    foreach(var connectNode in connectNodes)
    {
        var client = new RpcClient(connectNode.Node.IpAddress, connectNode.Node.Port);
        var result = await client.CallAsync("Add", 5, 3);
        Console.WriteLine($"Result: {result}");
    }
}

async Task MainAsync()
{
    var serverTask = StartServer();

    // 等待服务端启动
    await Task.Delay(2000);
    Console.WriteLine("delay end");
    await StartClient();

    // 等待服务端任务完成（通常不会发生，因为服务器会一直运行）
    await serverTask;
}

await MainAsync();
*/

/*
IKVDataBase kvDataBase = new AllTable();

bool kknd = await kvDataBase.AddTableNodeInt64Async("test");
Console.WriteLine($"kknd is {kknd}");

double writeMax = 1e6;
double readMax = 1e6;

Stopwatch writeStopWatch = Stopwatch.StartNew();
for (int i = 0; i < writeMax; i++)
{
    bool knd = await kvDataBase.CreateKVInt64Async("test", i.ToString(), i);
}
writeStopWatch.Stop();
Console.WriteLine($"写入了 {writeMax}条, 用了{writeStopWatch.ElapsedMilliseconds}ms");
Random random = new Random();
Stopwatch readStopWatch = Stopwatch.StartNew();
for(int i = 0; i < readMax; i++)
{
    int k = random.Next(0, (int)writeMax);
    long knd = await kvDataBase.GetKValInt64Async("test", k.ToString());
}
readStopWatch.Stop();
Console.WriteLine($"读了{readMax}条, 用了{readStopWatch.ElapsedMilliseconds}ms");

//await kvDataBase.SaveDataBaseInt64("test");

Console.WriteLine($"now delete and {await kvDataBase.DeleteTableNode("test")}\n");
*/