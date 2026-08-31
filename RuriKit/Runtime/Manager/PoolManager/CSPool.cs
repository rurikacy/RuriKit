using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Pool;

namespace RuriKit
{
    /// <summary>
    ///     为不同泛型类型的纯 C# 对象池提供统一的清理操作。
    /// </summary>
    internal interface ICSPool
    {
        /// <summary>
        ///     销毁池中所有未借出的对象。
        /// </summary>
        void Clear();

        /// <summary>
        ///     释放对象池及其未借出的对象。
        /// </summary>
        void Dispose();
    }

    /// <summary>
    ///     封装指定引用类型的 Unity 对象池。
    /// </summary>
    /// <typeparam name="T">池中对象的类型。</typeparam>
    internal class CSPool<T> : ICSPool where T : class, new()
    {
        private readonly HashSet<T> _borrowedObjects = new(ReferenceEqualityComparer<T>.Instance);

        /// <summary>
        ///     当前封装的对象池。
        /// </summary>
        private readonly ObjectPool<T> _pool;

        /// <summary>
        ///     使用指定对象池创建封装实例。
        /// </summary>
        /// <param name="pool">要封装的对象池。</param>
        public CSPool(ObjectPool<T> pool)
        {
            _pool = pool;
        }

        /// <summary>
        ///     从对象池借出一个对象，并记录其借出状态。
        /// </summary>
        public T Get()
        {
            T obj = _pool.Get();
            _borrowedObjects.Add(obj);
            return obj;
        }

        /// <summary>
        ///     归还一个已借出的对象。
        /// </summary>
        /// <param name="obj">要归还的对象。</param>
        /// <returns>对象确实由此池借出并已归还时返回 <c>true</c>；重复归还或非池对象返回 <c>false</c>。</returns>
        public bool Release(T obj)
        {
            if (!_borrowedObjects.Remove(obj))
            {
                return false;
            }

            _pool.Release(obj);
            return true;
        }

        /// <summary>
        ///     销毁池中所有未借出的对象。
        /// </summary>
        public void Clear()
        {
            _pool.Clear();
        }

        /// <summary>
        ///     释放对象池及其未借出的对象，并清空借出状态记录。
        /// </summary>
        public void Dispose()
        {
            _pool.Dispose();
            _borrowedObjects.Clear();
        }
    }

    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new();

        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
