// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// JSON 文本没有通过读取检查的原因
/// </summary>
/// <remarks>
/// <see cref="JsonText.FindProblem"/> 找到的第一个问题（RFC-0001「JSON 文本」）。种类与路径都是必填的初始化成员，没有位置构造与解构：以后加字段只加可选的初始化成员，不破坏已有的构造处（ADR-0142、ADR-0291）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record JsonTextProblem
{
    /// <summary>
    /// 问题的种类
    /// </summary>
    public required JsonTextProblemKind Kind { get; init; }

    /// <summary>
    /// 问题的位置
    /// </summary>
    /// <remarks>
    /// <see cref="JsonTextProblemKind.DuplicateMember"/> 时是第一处重复（第二次出现的成员）的路径，写法同 RFC-0011 规则 2（<c>$.a.b</c>、<c>$[0].a</c>、<c>$['a.b']</c>）；其余种类都是语法错误，一律是 <c>$</c>，因为各语言的解析器报告语法错误的位置各不相同
    /// </remarks>
    public required string Path { get; init; }
}
