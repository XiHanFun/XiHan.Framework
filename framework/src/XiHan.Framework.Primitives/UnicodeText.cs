// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 文本的 Unicode 判定
/// </summary>
/// <remarks>
/// 规范里「孤立的代理项」的 .NET 判定（RFC-0001「孤立的代理项」，ADR-0321）：标识、Baggage 的值与运营方配置的前缀后缀都用它拒收编不成 UTF-8 的文本
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class UnicodeText
{
    /// <summary>
    /// 文本里是否有孤立的代理项
    /// </summary>
    /// <remarks>
    /// 孤立的代理项是不在一对之中的 U+D800–U+DFFF 码元：单独的高代理项、单独的低代理项、颠倒的一对、末尾没有低代理项跟着的高代理项。高代理项紧跟低代理项是平面以外的一个字符，不算。宽松的编码（<see cref="System.Text.Encoding.UTF8"/>、<see cref="Uri.EscapeDataString(string)"/>、JSON 写出器）把孤立的代理项换成 U+FFFD，两个只差在它上面的文本编出相同的字节
    /// </remarks>
    public static bool HasLoneSurrogate(ReadOnlySpan<char> text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            // 成对的跳过这一对；其余的代理项，不论高低、在哪里，都是孤立的
            if (char.IsSurrogate(text[index]) && (!char.IsHighSurrogate(text[index]) || ++index == text.Length || !char.IsLowSurrogate(text[index])))
            {
                return true;
            }
        }

        return false;
    }
}
