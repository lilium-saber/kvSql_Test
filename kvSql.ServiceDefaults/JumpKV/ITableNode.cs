using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kvSql.ServiceDefaults.JumpKV
{
    public interface IJumpNode
    {
        bool AddVal(object key, object val);

        bool DeteleKey(object key);

        bool ExistKey(object key);

        bool ChangeVal(object key, object val);

        object? GetVal(object key);

        Task SaveJump();
    }

    public interface IJumpNode<Tkey, TVal> : IJumpNode where Tkey : IComparable<Tkey>
    {
        bool AddVal(Tkey key, TVal val);

        bool DeteleKey(Tkey key);

        bool ExistKey(Tkey key);

        bool ChangeVal(Tkey key, TVal val);

        TVal? GetVal(Tkey key);
    }
}
