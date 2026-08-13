using UnityEngine;
using UnityEngine.UI;

namespace RuriKit
{
    /// <summary>
    ///     为 Unity UI <see cref="Graphic" /> 组件提供扩展方法。
    /// </summary>
    public static class GraphicExtensions
    {
        /// <summary>
        ///     设置图形组件颜色的透明度，并将透明度限制在 0 到 1 之间。
        /// </summary>
        /// <param name="graphic">要设置透明度的图形组件。如果为 <c>null</c>，则不执行任何操作。</param>
        /// <param name="alpha">目标透明度。0 表示完全透明，1 表示完全不透明。</param>
        public static void SetAlpha(this Graphic graphic, float alpha)
        {
            if (!graphic) return;
            Color color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }

        /// <summary>
        ///     获取图形组件颜色的透明度。
        /// </summary>
        /// <param name="graphic">要获取透明度的图形组件。如果为 <c>null</c>，则返回 0。</param>
        /// <returns>当前透明度。0 表示完全透明，1 表示完全不透明。</returns>
        public static float GetAlpha(this Graphic graphic)
        {
            if (!graphic) return 0f;
            return graphic.color.a;
        }
    }
}