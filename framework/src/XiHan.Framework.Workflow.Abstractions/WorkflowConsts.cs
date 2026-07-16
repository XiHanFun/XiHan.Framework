#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowConsts
// Guid:b4a7e913-6f28-4d0c-a5e1-48c9f723d6b1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:03:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 工作流通用常量
/// </summary>
public static class WorkflowConsts
{
    /// <summary>
    /// 实例执行锁资源键前缀（单实例单写者保证）
    /// </summary>
    public const string InstanceLockKeyPrefix = "xihan:workflow:lock:instance:";

    /// <summary>
    /// 出边条件求值时注入的活动结果变量名
    /// </summary>
    public const string OutcomeVariableName = "outcome";

    /// <summary>
    /// 节点失败续行时写入变量的错误信息键
    /// </summary>
    public const string LastErrorVariableName = "lastError";

    /// <summary>
    /// 节点超时恢复时注入恢复输入的标记键
    /// </summary>
    public const string TimeoutInputKey = "__timeout";

    /// <summary>
    /// 子流程回调输入：子实例标识
    /// </summary>
    public const string ChildInstanceIdInputKey = "childInstanceId";

    /// <summary>
    /// 子流程回调输入：子实例终态
    /// </summary>
    public const string ChildStatusInputKey = "childStatus";

    /// <summary>
    /// 子流程回调输入：子实例变量快照
    /// </summary>
    public const string ChildVariablesInputKey = "childVariables";

    /// <summary>
    /// 子流程回调输入：子实例故障信息（子实例启动失败或故障时提供）
    /// </summary>
    public const string ChildFaultMessageInputKey = "childFaultMessage";

    /// <summary>
    /// 节点私有状态键：当前波次已启动的子实例标识集合（引擎维护，用于隔离重试后旧波次子实例的回调）
    /// </summary>
    public const string ChildInstanceIdsStateKey = "__childInstanceIds";
}
