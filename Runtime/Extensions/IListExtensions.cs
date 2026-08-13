using System.Collections.Generic;
using UnityEngine;

namespace RuriKit
{
    /// <summary>
    ///     为 <see cref="IList{T}" /> 泛型列表提供随机选取和打乱等扩展方法。
    /// </summary>
    public static class IListExtensions
    {
        /// <summary>
        ///     从指定列表中随机选取一个元素。
        /// </summary>
        /// <typeparam name="T">列表元素的类型。</typeparam>
        /// <param name="list">要从中随机选取元素的列表。</param>
        /// <returns>
        ///     如果列表为 <c>null</c> 或为空，则返回 <typeparamref name="T" /> 的默认值；否则返回列表中随机位置的一个元素。
        /// </returns>
        public static T PickRandom<T>(this IList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>
        ///     使用 Fisher-Yates 算法原地随机打乱指定列表中的元素顺序。
        /// </summary>
        /// <typeparam name="T">列表元素的类型。</typeparam>
        /// <param name="list">要打乱的列表。如果为 <c>null</c> 或元素少于两个，则不执行任何操作。</param>
        public static void Shuffle<T>(this IList<T> list)
        {
            if (list == null || list.Count < 2) return;

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}