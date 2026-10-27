// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 活动执行结果的种类。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum ActivityOutcomeKind
{
    /// <summary>
    /// 完成，按出边继续。
    /// </summary>
    Completed = 0,

    /// <summary>
    /// 挂起，等待书签。
    /// </summary>
    Suspended = 1,

    /// <summary>
    /// 完成并结束实例（其余分支清理，子流程终止）。
    /// </summary>
    EndInstance = 2,

    /// <summary>
    /// 终止实例。
    /// </summary>
    Terminated = 3,
}
