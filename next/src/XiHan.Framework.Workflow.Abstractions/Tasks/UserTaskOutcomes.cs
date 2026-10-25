// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 用户任务的内置结果；自定义结果也允许（须在节点的 <c>AllowedOutcomes</c> 里）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class UserTaskOutcomes
{
    /// <summary>通过。</summary>
    public const string Approved = "approved";

    /// <summary>拒绝。</summary>
    public const string Rejected = "rejected";

    /// <summary>超时。</summary>
    public const string Timeout = "timeout";
}
