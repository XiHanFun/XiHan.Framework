#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowLockTimeoutException
// Guid:5e28c7d0-9134-4a6f-b8e2-04d95c17f6a3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 13:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Exceptions;

/// <summary>
/// 实例锁获取超时异常（瞬态并发冲突，调用方可重试；须与书签已消费等永久性失败区分处理）
/// </summary>
public class WorkflowLockTimeoutException : WorkflowException
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    public WorkflowLockTimeoutException(string instanceId)
        : base($"获取实例 {instanceId} 的执行锁超时，实例正在被其他执行者处理")
    {
        InstanceId = instanceId;
    }

    /// <summary>
    /// 实例标识
    /// </summary>
    public string InstanceId { get; }
}
