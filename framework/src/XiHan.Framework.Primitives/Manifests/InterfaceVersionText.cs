// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 接口版本号的文本
/// </summary>
/// <remarks>
/// 文法 <c>[1-9][0-9]*</c> 且在 32 位有符号整数范围内（RFC-0002，ADR-0236）：不接受前导零、符号与空白，与清单 Schema 的 <c>^[1-9][0-9]*$</c> 一致
/// </remarks>
internal static class InterfaceVersionText
{
    /// <summary>
    /// 解析接口版本号
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, out int version)
    {
        version = 0;
        if (text.IsEmpty || text[0] is < '1' or > '9')
        {
            return false;
        }

        foreach (var c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }
}
