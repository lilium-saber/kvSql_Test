using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kvSql.ServiceDefaults.BpTree
{
    public interface IJumpNode
    {
        bool AddVal(object key, object val);

        bool DeteleKey(object key);

        bool ExistKey(object key);

        object? GetVal(object key);
    }

    public interface IJumpNode<Tkey, TVal> : IJumpNode where Tkey : IComparable<Tkey>
    {
        bool AddVal(Tkey key, TVal val);

        bool DeteleKey(Tkey key);

        bool ExistKey(Tkey key);

        TVal? GetVal(Tkey key);
    }
}
