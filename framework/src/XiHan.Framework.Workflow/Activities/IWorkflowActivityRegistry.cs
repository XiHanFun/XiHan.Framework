// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Activities;

/// <summary>
/// 活动注册表（按活动类型编码解析活动描述符）
/// </summary>
public interface IWorkflowActivityRegistry
{
    /// <summary>
    /// 获取活动描述符（不存在抛出异常）
    /// </summary>
    /// <param name="activityType">活动类型编码</param>
    /// <returns>活动描述符</returns>
    WorkflowActivityDescriptor Get(string activityType);

    /// <summary>
    /// 尝试获取活动描述符
    /// </summary>
    /// <param name="activityType">活动类型编码</param>
    /// <param name="descriptor">活动描述符</param>
    /// <returns>存在返回 true</returns>
    bool TryGet(string activityType, out WorkflowActivityDescriptor descriptor);

    /// <summary>
    /// 获取全部活动描述符（设计器活动面板数据源）
    /// </summary>
    /// <returns>活动描述符列表</returns>
    IReadOnlyList<WorkflowActivityDescriptor> GetAll();
}
