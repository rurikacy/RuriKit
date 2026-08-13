using System.Text;
using TMPro;

namespace RuriKit
{
    /// <summary>
    ///     为 TextMeshPro 文本组件提供精灵数字显示等扩展方法。
    /// </summary>
    public static class TextMeshProExtensions
    {
        private static readonly StringBuilder _stringBuilder = new();

        /// <summary>
        ///     将整数的每个字符转换为对应索引的 TextMesh Pro 精灵标签，并设置为文本内容。
        /// </summary>
        /// <param name="tmp">要设置内容的 TextMesh Pro 文本组件。如果为 <c>null</c>，则不执行任何操作。</param>
        /// <param name="number">要转换为精灵序列的整数。</param>
        public static void SetNumberToSprites(this TMP_Text tmp, int number)
        {
            if (!tmp) return;
            string numberStr = number.ToString();
            _stringBuilder.Clear();
            for (int i = 0; i < numberStr.Length; i++)
            {
                _stringBuilder.Append($"<sprite={numberStr[i]}>");
            }
            tmp.text = _stringBuilder.ToString();
        }
    }
}