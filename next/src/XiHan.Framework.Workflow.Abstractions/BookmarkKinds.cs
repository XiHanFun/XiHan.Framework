// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 书签种类。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class BookmarkKinds
{
    /// <summary>用户任务；键是指派人。</summary>
    public const string UserTask = "UserTask";

    /// <summary>定时；到期恢复。</summary>
    public const string Timer = "Timer";

    /// <summary>信号；键是信号名。</summary>
    public const string Signal = "Signal";

    /// <summary>子流程回调；键是父节点实例 ID。</summary>
    public const string SubWorkflow = "SubWorkflow";

    /// <summary>重试；到期重试节点。</summary>
    public const string Retry = "Retry";

    /// <summary>节点超时；到期以超时恢复节点。</summary>
    public const string NodeTimeout = "NodeTimeout";
}
