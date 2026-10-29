// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tasks;

/// <summary>
/// 多个指派人时任务如何完成。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum UserTaskCompletionPolicy
{
    /// <summary>
    /// 任一指派人通过即完成（默认）。
    /// </summary>
    Any = 1,

    /// <summary>
    /// 全部指派人通过才完成。
    /// </summary>
    /// <remarks>
    /// 其间合并变量并保留其余书签。
    /// </remarks>
    All = 2,

    /// <summary>
    /// 按顺序逐个指派，前一个通过后才给下一个。
    /// </summary>
    Sequential = 3,
}
