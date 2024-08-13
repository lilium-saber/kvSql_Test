// See https://aka.ms/new-console-template for more information

using kvSql.ServiceDefaults;
using kvSql.ServiceDefaults.JumpKV;
using System.Diagnostics;

IKVDataBase kvDataBase = new AllTable();

bool kknd = await kvDataBase.AddTableNodeInt64("test");
Console.WriteLine($"kknd is {kknd}");

double writeMax = 1e6;
double readMax = 1e6;

Stopwatch writeStopWatch = Stopwatch.StartNew();
for (int i = 0; i < writeMax; i++)
{
    bool knd = await kvDataBase.CreateKVInt64("test", i.ToString(), i);
}
writeStopWatch.Stop();
Console.WriteLine($"写入了 {writeMax}条, 用了{writeStopWatch.ElapsedMilliseconds}ms");
Random random = new Random();
Stopwatch readStopWatch = Stopwatch.StartNew();
for(int i = 0; i < readMax; i++)
{
    int k = random.Next(0, (int)writeMax);
    long knd = await kvDataBase.GetKValInt64("test", k.ToString());
}
readStopWatch.Stop();
Console.WriteLine($"读了{readMax}条, 用了{readStopWatch.ElapsedMilliseconds}ms");

//await kvDataBase.SaveDataBaseInt64("test");

Console.WriteLine($"now delete and {await kvDataBase.DeleteTableNode("test")}\n");
