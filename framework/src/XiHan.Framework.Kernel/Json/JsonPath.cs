// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Globalization;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// JSON 值的路径写法
/// </summary>
/// <remarks>
/// 照 System.Text.Json 的实测（RFC-0011 规则 2，ADR-0235）：从 <c>$</c> 起，成员写 <c>.名字</c>，数组下标写 <c>[i]</c>；成员名含 <c>.</c>、空格、<c>'</c>、<c>/</c>、<c>"</c>、<c>[</c>、<c>]</c>、<c>(</c>、<c>)</c>、<c>\</c>、制表符、换行、回车、换页、退格、U+0085、U+2028 或 U+2029 时写 <c>['名字']</c>，名字原样不转义
/// </remarks>
internal static class JsonPath
{
    /// <summary>
    /// 根的路径
    /// </summary>
    public const string Root = "$";

    /// <summary>
    /// 要写成方括号形式的成员名字符
    /// </summary>
    private static readonly SearchValues<char> _bracketed = SearchValues.Create(".' /\"[]()\\\t\n\r\f\b\u0085\u2028\u2029");

    /// <summary>
    /// 成员的路径
    /// </summary>
    public static string Member(string parent, string name)
        => name.AsSpan().ContainsAny(_bracketed) ? $"{parent}['{name}']" : $"{parent}.{name}";

    /// <summary>
    /// 数组项的路径
    /// </summary>
    public static string Index(string parent, int index)
        => $"{parent}[{index.ToString(CultureInfo.InvariantCulture)}]";
}
