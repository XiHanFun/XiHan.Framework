// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions;

/// <summary>
/// 内置活动类型编码常量
/// </summary>
public static class WorkflowActivityTypes
{
    /// <summary>
    /// 开始
    /// </summary>
    public const string Start = "Start";

    /// <summary>
    /// 结束
    /// </summary>
    public const string End = "End";

    /// <summary>
    /// 终止（强制结束整个实例）
    /// </summary>
    public const string Terminate = "Terminate";

    /// <summary>
    /// 设置变量
    /// </summary>
    public const string SetVariable = "SetVariable";

    /// <summary>
    /// 独占网关（条件分支）
    /// </summary>
    public const string Decision = "Decision";

    /// <summary>
    /// 并行网关（分支扇出）
    /// </summary>
    public const string Parallel = "Parallel";

    /// <summary>
    /// 汇聚网关（分支汇合）
    /// </summary>
    public const string Join = "Join";

    /// <summary>
    /// 延时等待
    /// </summary>
    public const string Delay = "Delay";

    /// <summary>
    /// 人工任务（审批）
    /// </summary>
    public const string UserTask = "UserTask";

    /// <summary>
    /// HTTP 请求
    /// </summary>
    public const string Http = "Http";

    /// <summary>
    /// C# 脚本
    /// </summary>
    public const string Script = "Script";

    /// <summary>
    /// 发布事件
    /// </summary>
    public const string PublishEvent = "PublishEvent";

    /// <summary>
    /// 等待信号
    /// </summary>
    public const string WaitSignal = "WaitSignal";

    /// <summary>
    /// 子流程
    /// </summary>
    public const string SubWorkflow = "SubWorkflow";

    /// <summary>
    /// 遍历（对集合逐项/并行执行子流程）
    /// </summary>
    public const string ForEach = "ForEach";

    /// <summary>
    /// 日志
    /// </summary>
    public const string Log = "Log";

    /// <summary>
    /// 抛出故障
    /// </summary>
    public const string Fault = "Fault";
}
