using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kvSql.ServiceDefaults.JumpKV
{
    public interface IKVDataBase
    {
        public Task<bool> CreateKV(string s, string key, string val);

        public Task<bool> AddTableNode(string s);

        public Task<string> GetKVal(string s, string key);

        public Task<bool> ChangeVal(string s, string key, string newVal);

        public Task SaveDataBase(string s);

        public Task<bool> CreateKVInt64(string s, string key, long val);

        public Task<bool> AddTableNodeInt64(string s);

        public Task<long> GetKValInt64(string s, string key);

        public Task<bool> ChangeValInt64(string s, string key, long newVal);

        public Task SaveDataBaseInt64(string s);

        public Task<bool> AddTableNode<Tkey, Tvalue>(string s) where Tkey : IComparable<Tkey>;

        public Task<bool> DeleteTableNode(string s);

        public Task<Tvalue> GetKVal<Tkey, Tvalue>(string s, Tkey key) where Tkey : IComparable<Tkey>;

        public Task<bool> CreateKV<Tkey, Tval>(string s, Tkey key, Tval val) where Tkey : IComparable<Tkey>;

        public Task<bool> ChangeVal<Tkey, Tval>(string s, Tkey key, Tval newVal) where Tkey : IComparable<Tkey>;
    }

    public class AllTable : IKVDataBase
    {
        private Dictionary<string, IJumpNode> tableNodes;

        public AllTable()
        {
            tableNodes = new Dictionary<string, IJumpNode>();
        }

        public async Task<bool> AddTableNode<Tkey, Tvalue>(string s) where Tkey : IComparable<Tkey>
        {
            if (tableNodes.ContainsKey(s))
            {
                Console.WriteLine("this string has alive\n");
                return false;
            }
            else
            {
                tableNodes.Add(s, new JumpList<Tkey, Tvalue>(s));
                if(tableNodes.ContainsKey(s))
                {
                    await tableNodes[s].SaveJump();
                    return true;
                }
                else
                {
                   Console.WriteLine("add error\n");
                   return false;
                }
            }
        }

        public Task<bool> DeleteTableNode(string s)
        {
            if (tableNodes.ContainsKey(s))
            {
                string relativePath = Path.Combine("kvSql.ServiceDefaults", "DataFile", $"{s}.json");
                string solutionPath = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
                string filePath = Path.Combine(solutionPath, relativePath);
                if(File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                else
                {
                    Console.WriteLine($"File {s}.json is not found\n");
                }
                tableNodes.Remove(s);
                if (!tableNodes.ContainsKey(s))
                {
                    return Task.FromResult(true);
                }
                else
                {
                    Console.WriteLine("Delete error\n");
                    return Task.FromResult(false);
                }
            }
            else
            {
                Console.WriteLine("TableNode is not found befor delete\n");
                return Task.FromResult(true);
            }
        }

        public JumpList<Tkey, Tvalue> GetTableNode<Tkey, Tvalue>(string s) where Tkey : IComparable<Tkey>
        {
            if (tableNodes.ContainsKey(s))
            {
                return (JumpList<Tkey, Tvalue>)tableNodes[s];
            }
            else
            {
                Console.WriteLine("TableNode is not found\nget table_node fail\n");
                return null;
            }
        }

        public async Task<Tvalue> GetKVal<Tkey, Tvalue>(string s, Tkey key) where Tkey : IComparable<Tkey>
        {
            if (tableNodes.ContainsKey(s))
            {
                return await Task.FromResult(((JumpList<Tkey, Tvalue>)tableNodes[s]).GetVal(key));
            }
            else
            {
                Console.WriteLine("TableNode is not found\n get value fail\n");
                return await Task.FromResult(default(Tvalue));
            }
        }

        public async Task SaveDataBase(string s)
        {
            if (tableNodes.ContainsKey(s))
            {
                await ((JumpList<string, string>)tableNodes[s]).SaveJump();
            }
            else
            {
                Console.WriteLine("TableNode is not found\nsave fail\n");
            }
        }

        public async Task SaveDataBaseInt64(string s)
        {
            if (tableNodes.ContainsKey(s))
            {
                await ((JumpList<string, long>)tableNodes[s]).SaveJump();
            }
            else
            {
                Console.WriteLine("TableNode is not found\nsave fail\n");
            }
        }

        public async Task<bool> CreateKV<Tkey, Tval>(string s, Tkey key, Tval val) where Tkey : IComparable<Tkey>
        {
            if (tableNodes.ContainsKey(s))
            {
                return await Task.FromResult(((JumpList<Tkey, Tval>)tableNodes[s]).AddVal(key, val));
            }
            else
            {
                Console.WriteLine("TableNode is not found\nCreate KV\n");
                return false;
            }
        }

        public async Task<bool> ChangeVal<Tkey, Tval>(string s, Tkey key, Tval newVal) where Tkey : IComparable<Tkey>
        {
            if (tableNodes.ContainsKey(s))
            {
                return await Task.FromResult(((JumpList<Tkey, Tval>)tableNodes[s]).ChangeVal(key, newVal));
            }
            else
            {
                Console.WriteLine("TableNode is not found\nChange KV\n");
                return false;
            }
        }

        public async Task<bool> CreateKV(string s, string key, string val)
        {
            return await CreateKV<string, string>(s, key, val);
        }

        public async Task<bool> CreateKVInt64(string s, string key, long val)
        {
            return await CreateKV<string, long>(s, key, val);
        }

        public async Task<bool> AddTableNode(string s)
        {
            return await AddTableNode<string, string>(s);
        }

        public async Task<bool> AddTableNodeInt64(string s)
        {
            return await AddTableNode<string, long>(s);
        }

        public async Task<string> GetKVal(string s, string key)
        {
            return await GetKVal<string, string>(s, key);
        }

        public async Task<long> GetKValInt64(string s, string key)
        {
            return await GetKVal<string, long>(s, key);
        }

        public async Task<bool> ChangeVal(string s, string key, string newVal)
        {
            return await ChangeVal<string, string>(s, key, newVal);
        }

        public async Task<bool> ChangeValInt64(string s, string key, long newVal)
        {
            return await ChangeVal<string, long>(s, key, newVal);
        }
    }
}
