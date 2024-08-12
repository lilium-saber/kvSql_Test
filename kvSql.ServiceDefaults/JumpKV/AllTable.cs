using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kvSql.ServiceDefaults.JumpKV
{
    public class AllTable
    {
        private Dictionary<string, IJumpNode> tableNodes;

        public AllTable()
        {
            tableNodes = new Dictionary<string, IJumpNode>();
        }

        public Task<bool> AddTableNode<Tkey, Tvalue>(string s) where Tkey : IComparable<Tkey>
        {
            if (tableNodes.ContainsKey(s))
            {
                Console.WriteLine("this string has alive\n");
                return Task.FromResult(false);
            }
            else
            {
                tableNodes.Add(s, new JumpList<Tkey, Tvalue>(s));
                if(tableNodes.ContainsKey(s))
                {
                    return Task.FromResult(true);
                }
                else
                {
                    Console.WriteLine("add error\n");
                   return Task.FromResult(false);
                }
            }
        }

        public Task<bool> deleteTableNode(string s)
        {
            if (tableNodes.ContainsKey(s))
            {
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
                Console.WriteLine("TableNode is not found\n");
                return null;
            }
        }

    }
}
