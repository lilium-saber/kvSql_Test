using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace kvSql.ServiceDefaults.Rpc
{
    public class Config
    {
        public ServerConfig LocalServer { get; set; }
        public List<ConnectNode> ConnectNodes { get; set; }
        public int meNodeId { get; set; }
    }

    public class ServerConfig
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
    }

    public class ConnectNode
    {
        public ClientConfig Node { get; set; }
    }

    public class ClientConfig
    {
        public int id { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
    }

    [JsonSerializable(typeof(Config))]
    [JsonSerializable(typeof(ServerConfig))]
    [JsonSerializable(typeof(ConnectNode))]
    [JsonSerializable(typeof(ClientConfig))]
    public partial class ConfigJsonContext : JsonSerializerContext
    {
    }

    public static class ConfigLoader
    {
        public static Config LoadConfig(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize(json, ConfigJsonContext.Default.Config);
        }
    }
}
