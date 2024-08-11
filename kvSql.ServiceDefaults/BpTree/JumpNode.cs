﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kvSql.ServiceDefaults.BpTree
{
    internal class ValNode<TKey, TVal> where TKey : IComparable<TKey>
    {
        public TKey Keys { get; set; }
        public TVal? Values { get; set; }

        public ValNode(TKey keys, TVal values)
        {
            Keys = keys;
            Values = values;
        }
    }

    internal class JumpNode<TKey, TVal> where TKey : IComparable<TKey> 
    {
        public int Lever { get; set; }
        public ValNode<TKey, TVal> Val { get; set; }
        public JumpNode<TKey, TVal>[] Next { get; set; } //当前节点的各个层级的下一个节点，数组下标是层级，数组代表节点的层级高度

        public JumpNode(int lever, TKey key, TVal val)
        {
            Lever = lever;
            Val = new ValNode<TKey, TVal>(key, val);
            Next = new JumpNode<TKey, TVal>[lever + 1];
        }
    }

    public class JumpList<TKey, TVal> : IJumpNode<TKey, TVal> where TKey : IComparable<TKey>
    {
        private readonly int LeverMax = 5;
        private readonly JumpNode<TKey, TVal> head; //一定是指向层数满的节点
        private readonly Random random;

        public JumpList()
        {
            head = new JumpNode<TKey, TVal>(LeverMax, default(TKey), default(TVal));
            random = new Random();
        }

        private int RandomLever()
        {
            int lever = 0;
            while (random.Next(0, 2) == 1 && lever < LeverMax)
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

        public bool AddVal(TKey key, TVal val)
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

        public TVal? GetVal(TKey key)
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

        public bool DeteleKey(TKey key)
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
                return true;
            }
            else
            {
                return false;
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

    }
    /*
     * 如果使用cpp学习，简单编写scoket通信编程，并让C#调用在服务端、客户端以调用的形式使用这个scoket通信服务 
     */
}
