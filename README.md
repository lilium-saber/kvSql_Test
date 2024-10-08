# kvSql
简单的KV数据库个人项目（以跳表为索引，跳表最大高度为15）
使用语言为C#、C#版本是12
使用 .NET core框架（可跨平台），.NET core版本为8.0

### 项目模块
- KV数据库——部分完成（泛型底层，但是对外接口使用了特定类型的实现）
- Raft——部分完成（简化了快照部分，暂时不能将日志压缩为快照。目前测试只有单机）
- Rpc——部分完成（通过tcp进行连接，使用tcp库，具备超时重传）

### 目前功能
- KV数据库支持泛型，目前为了便于运行固定了<string, string>与<string, long>两种使用类型。但是代码可以扩展至其他类型。long类型数据支持基本数学运算（加和减）。
- 数据库以json格式保存。
- 数据库支持读写锁。
- raft节点通过json读取设置（节点ip、节点名称以及服务器设置等）。
- raft节点类可以进行选举、心跳发送，根据不同情况更新相关定时器。
- 服务器可以让数据库更改命令添加到raft节点类的日志中，定时处理日志。
- 可以通过通信获取数据，但是要考虑日志处理速度是否进行到正确的数据部分。
- Rpc具备超时重传功能。

<!-- ### 跳表数据库读写效率
只有跳表数据库部分运行
|数据量(万条)|写耗时(ms)|读耗时(ms)|
|---|---|---|
|1|20|22|
|5|119|125|
|10|191|236|
|50|914|996|
|100|1792|2163|

（测试机使用）按照100万条计算（仅单独跳表数据库工作）：
- 平均每秒写约有558,035万条
- 平均每秒读约有462,320万条 -->

