using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kvSql.ServiceDefaults.JumpKV
{
    public class JsonNodeData<Tkey, Tval>
    {
        public int lever { get; set; }
        public Tkey key { get; set; }
        public Tval? val { get; set; }
        public List<Tkey> NextKeys { get; set; }
    }

    public class JsonListData<TKey, TVal>
    {
        public string keyType { get; set; }
        public string valueType { get; set; }
        public string? jumpName { get; set; }
        private readonly int leverMax = 5;
        public List<JsonNodeData<TKey, TVal>> jsonNodeDatas { get; set; }
    }
}
