using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace kvSql.ServiceDefaults.Rpc
{
    public class Config
    {
        public ServerConfig Server { get; set; }
        public ClientConfig node1 { get; set; }
    }

    public class ServerConfig
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
    }

    public class ClientConfig
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
    }

    [JsonSerializable(typeof(Config))]
    [JsonSerializable(typeof(ServerConfig))]
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


