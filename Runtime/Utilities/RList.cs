using System.Collections;

namespace RuriKit
{
    public static class RList
    {
        /// <summary>
        ///     确定两个 <see cref="IList" /> 集合是否包含完全相同的元素（按顺序）。
        /// </summary>
        /// <param name="l1">要比较的第一个列表。</param>
        /// <param name="l2">要比较的第二个列表。</param>
        /// <returns>
        ///     如果两个列表都是 <c>null</c>，或者它们引用同一个对象，或者它们包含相同数量的元素，且每个对应位置的元素都相等，则为 <c>true</c>；否则为 <c>false</c>。
        /// </returns>
        public static bool AreArraysEqual(IList l1, IList l2)
        {
            if (ReferenceEquals(l1, l2)) return true;
            if (l1 == null || l2 == null) return false;
            if (l1.Count != l2.Count) return false;

            for (int i = 0; i < l1.Count; i++)
            {
                object leftVal = l1[i];
                object rightVal = l2[i];

                if (leftVal == null && rightVal == null) continue;
                if (leftVal == null || rightVal == null) return false;

                if (!leftVal.Equals(rightVal)) return false;
            }

            return true;
        }
    }
}