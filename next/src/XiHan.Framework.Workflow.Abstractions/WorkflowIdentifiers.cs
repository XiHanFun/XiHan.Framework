// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 标识符规则：节点与转移 ID 的文法，以及引擎生成的实例、节点实例、书签 ID（UUID v7 的 32 位十六进制）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static partial class WorkflowIdentifiers
{
    /// <summary>
    /// 节点与转移 ID 的最大长度。
    /// </summary>
    public const int MaxElementIdLength = 64;

    /// <summary>
    /// 节点或转移 ID 是否合法：<c>^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$</c>。
    /// </summary>
    public static bool IsValidElementId([NotNullWhen(true)] string? value) => value is not null && ElementId().IsMatch(value);

    /// <summary>
    /// 生成一个新的实例、节点实例或书签 ID：UUID v7，按时间单调，32 位小写十六进制。
    /// </summary>
    public static string NewId() => Guid.CreateVersion7().ToString("N");

    /// <summary>
    /// 元素 ID 的匹配。
    /// </summary>
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex ElementId();
}
