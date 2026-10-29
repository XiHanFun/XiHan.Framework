// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Assignees;

/// <summary>
/// 指派规则的解析器。
/// </summary>
/// <remarks>
/// 按规则类型登记（<c>AddAssigneeProvider&lt;T&gt;()</c>，同类型只能一个）。只读，在实例锁内运行；数据不可用时返回失败，不得以空列表掩盖。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IAssigneeProvider
{
    /// <summary>
    /// 规则类型。
    /// </summary>
    string RuleType { get; }

    /// <summary>
    /// 校验规则参数（发布定义时）。
    /// </summary>
    Result Validate(AssigneeRule rule);

    /// <summary>
    /// 解析出指派人。
    /// </summary>
    ValueTask<Result<IReadOnlyList<string>>> ResolveAsync(AssigneeResolution resolution, CancellationToken cancellationToken);
}
