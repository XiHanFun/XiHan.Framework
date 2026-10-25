// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// AI 子系统的名字文法：Provider、提示词与工具名 <c>^[a-z0-9][a-z0-9._-]{0,63}$</c>（工具名另允许大写字母与下划线开头，见 <see cref="IsValidToolName"/>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static partial class AIIdentifiers
{
    /// <summary>
    /// 名字最大长度。
    /// </summary>
    public const int MaxNameLength = 64;

    /// <summary>
    /// Provider 或提示词名是否合法。
    /// </summary>
    public static bool IsValidName([NotNullWhen(true)] string? value) => value is not null && Name().IsMatch(value);

    /// <summary>
    /// 工具名是否合法：模型 API 接受的 <c>^[A-Za-z0-9_-]{1,64}$</c>。
    /// </summary>
    public static bool IsValidToolName([NotNullWhen(true)] string? value) => value is not null && ToolName().IsMatch(value);

    /// <summary>
    /// 校验 Provider 或提示词名，不合法抛 <see cref="ArgumentException"/>。
    /// </summary>
    public static string ValidateName(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);
        return IsValidName(value) ? value : throw new ArgumentException($"'{value}' is not a valid AI name; expected '^[a-z0-9][a-z0-9._-]{{0,63}}$'.", parameterName);
    }

    /// <summary>
    /// 名字文法。
    /// </summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}$")]
    private static partial Regex Name();

    /// <summary>
    /// 工具名文法。
    /// </summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex ToolName();
}
