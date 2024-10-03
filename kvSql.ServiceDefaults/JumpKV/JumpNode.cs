using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.IO;
using System.Threading.Tasks;

namespace kvSql.ServiceDefaults.JumpKV
{
    public class JumpList<TKey, TVal> : IJumpNode<TKey, TVal> where TKey : IComparable<TKey>
    {
        // private class JumpNode<TKey, TVal>(int lever, TKey key, TVal val) where TKey : IComparable<TKey> 
        // {
        //     internal int Lever { get; set; } = lever;
        //     internal ValNode<TKey, TVal> Val { get; set; } = new ValNode<TKey, TVal>(key, val);
        //     internal JumpNode<TKey, TVal>[] Next { get; set; } = new JumpNode<TKey, TVal>[lever + 1];
        // }
        // private class ValNode<TKey, TVal>(TKey keys, TVal values) where TKey : IComparable<TKey>
        // {
        //     internal TKey Keys { get; set; } = keys;
        //     internal TVal? Values { get; set; } = values;
        // }
        private class JumpNode<TKey, TVal> where TKey : IComparable<TKey>
        {
            internal int Lever { get; set; }
            internal ValNode<TKey, TVal> Val { get; set; }
            internal JumpNode<TKey, TVal>[] Next { get; set; }

            internal JumpNode(int lever, TKey key, TVal val)
            {
                Lever = lever;
                Val = new ValNode<TKey, TVal>(key, val);
                Next = new JumpNode<TKey, TVal>[lever + 1];
            }
        }

        private class ValNode<TKey, TVal> where TKey : IComparable<TKey>
        {
            internal TKey Keys { get; set; }
            internal TVal? Values { get; set; }

            internal ValNode(TKey keys, TVal values)
            {
                Keys = keys;
                Values = values;
            }
        }

        private readonly int LeverMax = 15;
        private readonly JumpNode<TKey, TVal> head; //一定是层数满的节点，本身无数据
        private readonly Random random;
        private readonly ReaderWriterLockSlim rwLock;

        public readonly string keyType;
        public readonly string valueType;
        public readonly string? jumpName;

        public JumpList()
        {
            head = new JumpNode<TKey, TVal>(LeverMax, default(TKey), default(TVal));
            random = new Random();

            keyType = typeof(TKey).Name;
            valueType = typeof(TVal).Name;
            jumpName = null;
            rwLock = new();
        }

        public JumpList(string s)
        {
            head = new JumpNode<TKey, TVal>(LeverMax, default(TKey), default(TVal));
            random = new Random();

            keyType = typeof(TKey).Name;
            valueType = typeof(TVal).Name;
            jumpName = s;
            rwLock = new();
        }

        private int RandomLever()
        {
            int lever = 0;
            while (random.Next(0, 2) >= 1 && lever < LeverMax)
            {
                lever++;
            }
            return lever;
        }

        public bool AddVal(object key, object val)
        {
            return AddVal((TKey)key, (TVal)val);
        }

        public object? GetVal(object key)
        {
            return GetVal((TKey)key);
        }

        public bool DeteleKey(object key)
        {
            return DeteleKey((TKey)key);
        }

        public bool ExistKey(object key)
        {
            return ExistKey((TKey)key);
        }

        public bool ChangeVal(object key, object val)
        {
            return ChangeVal((TKey)key, (TVal)val);
        }

        public async Task<string> KValMathPlusAsync(object key, object number)
        {
            return await KValMathPlusAsync((TKey)key, (TVal)number);
        }

        public bool AddVal(TKey key, TVal val)
        {
            rwLock.EnterWriteLock();
            try
            {
                JumpNode<TKey, TVal>[] update = new JumpNode<TKey, TVal>[LeverMax + 1];
                JumpNode<TKey, TVal> p = head;
                for (int i = LeverMax; i >= 0; i--)
                {
                    while (p.Next[i] != null && p.Next[i].Val.Keys.CompareTo(key) < 0)
                    {
                        p = p.Next[i];
                    }
                    update[i] = p;
                }
                if (p.Next[0] != null && p.Next[0].Val.Keys.CompareTo(key) == 0)
                {
                    return false;
                }
                else
                {
                    int lever = RandomLever();
                    if (lever > LeverMax)
                    {
                        lever = LeverMax;
                    }
                    JumpNode<TKey, TVal> newNode = new JumpNode<TKey, TVal>(lever, key, val);
                    for (int i = 0; i <= lever; i++)
                    {
                        newNode.Next[i] = update[i].Next[i];
                        update[i].Next[i] = newNode;
                    }
                    return true;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
            
        }

        public TVal? GetVal(TKey key)
        {
            rwLock.EnterReadLock();
            try
            {
                JumpNode<TKey, TVal> p = head;
                for (int i = LeverMax; i >= 0; i--)
                {
                    while (p.Next[i] != null && p.Next[i].Val.Keys.CompareTo(key) < 0)
                    {
                        p = p.Next[i];
                    }
                }
                if (p.Next[0] != null && p.Next[0].Val.Keys.CompareTo(key) == 0)
                {
                    return p.Next[0].Val.Values;
                }
                else
                {
                    return default(TVal);
                }
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public bool DeteleKey(TKey key)
        {
            rwLock.EnterWriteLock();
            try
            {
                JumpNode<TKey, TVal>[] update = new JumpNode<TKey, TVal>[LeverMax + 1];
                JumpNode<TKey, TVal> p = head;
                for (int i = LeverMax; i >= 0; i--)
                {
                    while (p.Next[i] != null && p.Next[i].Val.Keys.CompareTo(key) < 0)
                    {
                        p = p.Next[i];
                    }
                    update[i] = p;
                }
                if (p.Next[0] != null && p.Next[0].Val.Keys.CompareTo(key) == 0)
                {
                    JumpNode<TKey, TVal> deleteNode = p.Next[0];
                    for (int i = 0; i <= deleteNode.Lever; i++)
                    {
                        update[i].Next[i] = deleteNode.Next[i];
                    }
                    deleteNode.Val = null;
                    deleteNode = null;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public bool ExistKey(TKey key)
        {
            JumpNode<TKey, TVal> p = head;
            for (int i = LeverMax; i >= 0; i--)
            {
                while (p.Next[i] != null && p.Next[i].Val.Keys.CompareTo(key) < 0)
                {
                    p = p.Next[i];
                }
            }
            if (p.Next[0] != null && p.Next[0].Val.Keys.CompareTo(key) == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool ChangeVal(TKey key, TVal val)
        {
            rwLock.EnterWriteLock();
            try
            {
                JumpNode<TKey, TVal> p = head;
                for (int i = LeverMax; i >= 0; i--)
                {
                    while (p.Next[i] != null && p.Next[i].Val.Keys.CompareTo(key) < 0)
                    {
                        p = p.Next[i];
                    }
                }
                if (p.Next[0] != null && p.Next[0].Val.Keys.CompareTo(key) == 0)
                {
                    p.Next[0].Val.Values = val;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public async Task SaveJumpAsync()
        {
            rwLock.EnterWriteLock();
            try
            {
                string json;
                string relativePath = Path.Combine("DataFile", $"{jumpName}.json");
                string solutionPath = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
                if (solutionPath == null)
                {
                    throw new InvalidOperationException("Solution path could not be determined.");
                }
                string filePath = Path.Combine(solutionPath, relativePath);
                if(!Directory.Exists(Path.GetDirectoryName(filePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                }
                var jsonListData = new JsonListData<TKey, TVal>
                {
                    keyType = keyType,
                    valueType = valueType,
                    jumpName = jumpName,
                    jsonNodeDatas = []
                };
                JumpNode<TKey, TVal> jumpNode = head;
                while (jumpNode.Next[0] != null)
                {
                    jumpNode = jumpNode.Next[0];
                    var jsonNodeData = new JsonNodeData<TKey, TVal>
                    {
                        lever = jumpNode.Lever,
                        key = jumpNode.Val.Keys,
                        val = jumpNode.Val.Values,
                        NextKeys = []
                    };
                    for (int i = 0; i <= jumpNode.Lever; i++)
                    {
                        if (jumpNode.Next[i] != null && jumpNode.Next[i].Val != null)
                        {
                            jsonNodeData.NextKeys.Add(jumpNode.Next[i].Val.Keys);
                        }
                    }
                    jsonListData.jsonNodeDatas.Add(jsonNodeData);
                }

                var options = new JsonSerializerOptions
                {
                    TypeInfoResolver = (valueType == "Int64") ? JsonListDataContextInt64.Default : JsonListDataContext.Default
                };
                json = JsonSerializer.Serialize(jsonListData, options);

                try
                {
                    await File.WriteAllTextAsync(filePath, json);
                    Console.WriteLine($"save as json, name is {jumpName}\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to save as json, {jumpName}\nmeg: {ex.Message}\n");
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public async Task<string> KValMathPlusAsync(TKey key, TVal number)
        {
            rwLock.EnterWriteLock();
            try
            {
                if (valueType == "Int32" || valueType == "Int64" || valueType == "Single" || valueType == "Double" || valueType == "Decimal" || valueType == "long" || valueType == "int")
                {
                    JumpNode<TKey, TVal> p = head;
                    for (int i = LeverMax; i >= 0; i--)
                    {
                        while (p.Next[i] != null && p.Next[i].Val.Keys.CompareTo(key) < 0)
                        {
                            p = p.Next[i];
                        }
                    }
                    if (p.Next[0] != null && p.Next[0].Val.Keys.CompareTo(key) == 0 && p.Next[0].Val != null)
                    {
                        try
                        {
                            dynamic currentValue = p.Next[0].Val.Values;
                            dynamic addValue = number;
                            p.Next[0].Val.Values = currentValue + addValue;
                            return await Task.FromResult("KV Math success\n");
                        }
                        catch (Exception ex)
                        {
                            return await Task.FromResult($"KV Math error: {ex.Message}\n");
                        }
                    }
                    else
                    {
                        return await Task.FromResult("Key not found\n");
                    }
                }
                else
                {
                    return await Task.FromResult("KV Math error: Unsupported value type\n");
                }
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }


    }
    /*
     * 如果使用cpp学习，简单编写rpc通信编程，并让C#调用在服务端、客户端以调用的形式使用这个scoket通信服务 
     */
}
