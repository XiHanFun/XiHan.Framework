// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 清单激活事件的文法
/// </summary>
/// <remarks>
/// <c>onStartup</c>、<c>onOperation:&lt;模式&gt;</c> 或 <c>onEvent:&lt;模式&gt;</c>，模式至少一个字符（RFC-0002 解析规则 2 第 3 步，<c>spec/plugin/lifecycle.md</c> §3）：<c>onOperation:</c> 的模式由操作 ID 的字符加通配 <c>*</c> 组成，<c>onEvent:</c> 的模式由主题的字符加 <c>*</c> 组成，所以只有它可以带下划线（ADR-0276）。<see cref="Manifest.Activation"/> 与内核读清单时的形状检查共用这一处（ADR-0278）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static partial class ActivationEvent
{
    /// <summary>
    /// 是否合文法
    /// </summary>
    public static bool IsValid([NotNullWhen(true)] string? value) => value is not null && Pattern().IsMatch(value);

    /// <summary>
    /// 激活事件的文法
    /// </summary>
    /// <remarks>
    /// 两种模式的字符集只差一个下划线：操作 ID 里没有它，主题里有（RFC-0009）
    /// </remarks>
    [GeneratedRegex("^(onStartup|onOperation:[a-z0-9.*-]+|onEvent:[a-z0-9._*-]+)\\z")]
    private static partial Regex Pattern();
}
