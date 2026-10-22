// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Mcp;

/// <summary>
/// MCP tool 名的文法：字母、数字、下划线与连字符，1 到 64 个字符。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static partial class McpToolName
{
    /// <summary>
    /// 最大长度。
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// 是否合法。
    /// </summary>
    public static bool IsValid([NotNullWhen(true)] string? name) => name is not null && Pattern().IsMatch(name);

    /// <summary>
    /// 校验并原样返回；不合法时抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static string Validate(string name, string? paramName = null)
    {
        if (!IsValid(name))
        {
            throw new ArgumentException($"Tool name '{name}' must match ^[A-Za-z0-9_-]{{1,{MaxLength}}}$.", paramName ?? nameof(name));
        }

        return name;
    }

    /// <summary>
    /// tool 名的匹配。
    /// </summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex Pattern();
}
