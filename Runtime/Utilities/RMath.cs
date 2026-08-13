namespace RuriKit
{
    public static class RMath
    {
        /// <summary>
        ///     将整数值规范化到指定范围内，并对齐到给定的步长。
        /// </summary>
        /// <param name="value">要规范化的值。</param>
        /// <param name="min">允许的最小值。</param>
        /// <param name="max">允许的最大值。</param>
        /// <param name="step">对齐步长。</param>
        /// <returns>
        ///     如果 <paramref name="value" /> 小于 <paramref name="min" /> 则返回 <paramref name="min" />；
        ///     如果大于 <paramref name="max" /> 则返回 <paramref name="max" />；
        ///     否则返回按 <paramref name="step" /> 向下对齐后的值。
        /// </returns>
        /// <exception cref="System.DivideByZeroException">
        ///     <paramref name="value" /> 位于指定范围内且 <paramref name="step" /> 为 0。
        /// </exception>
        public static int NormalizeInt(int value, int min, int max, int step)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value - (value - min) % step;
        }

        /// <summary>
        ///     对整数数组中的每个元素调用 <see cref="NormalizeInt(int, int, int, int)" />，返回规范化后的新数组。
        /// </summary>
        /// <param name="source">要规范化的源数组。如果为 <c>null</c> 则返回 <c>null</c>。</param>
        /// <param name="min">允许的最小值。</param>
        /// <param name="max">允许的最大值。</param>
        /// <param name="step">对齐步长。</param>
        /// <returns>
        ///     如果 <paramref name="source" /> 为 <c>null</c> 则返回 <c>null</c>；
        ///     否则返回每个元素都经过 <see cref="NormalizeInt(int, int, int, int)" /> 处理后的新数组。
        /// </returns>
        /// <exception cref="System.DivideByZeroException">
        ///     数组中存在位于指定范围内的元素，且 <paramref name="step" /> 为 0。
        /// </exception>
        public static int[] NormalizeArray(int[] source, int min, int max, int step)
        {
            if (source == null) return null;
            int[] result = new int[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = NormalizeInt(source[i], min, max, step);
            }
            return result;
        }
    }
}
