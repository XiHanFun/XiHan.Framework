// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Abstractions.Instances;

/// <summary>
/// 交给扩展代码的节点实例只读视图
/// </summary>
/// <remarks>
/// 跟随引擎正在修改的节点实例；活动的私有状态经上下文的 <c>State</c> 读写，输入与输出由引擎记录，都不在这里
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowNodeInstanceView
{
    /// <summary>
    /// 被查看的节点实例
    /// </summary>
    private readonly WorkflowNodeInstance _nodeInstance;

    /// <summary>
    /// 由引擎创建
    /// </summary>
    internal WorkflowNodeInstanceView(WorkflowNodeInstance nodeInstance)
    {
        _nodeInstance = nodeInstance;
    }

    /// <summary>
    /// 节点实例 ID
    /// </summary>
    public string Id => _nodeInstance.Id;

    /// <summary>
    /// 所属实例 ID
    /// </summary>
    public string InstanceId => _nodeInstance.InstanceId;

    /// <summary>
    /// 节点 ID
    /// </summary>
    public string NodeId => _nodeInstance.NodeId;

    /// <summary>
    /// 活动类型
    /// </summary>
    public ActivityType ActivityType => _nodeInstance.ActivityType;

    /// <summary>
    /// 状态
    /// </summary>
    public WorkflowNodeInstanceStatus Status => _nodeInstance.Status;

    /// <summary>
    /// 第几次尝试，从 1 起
    /// </summary>
    public int Attempt => _nodeInstance.Attempt;

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTimeOffset StartedAt => _nodeInstance.StartedAt;

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTimeOffset? EndedAt => _nodeInstance.EndedAt;

    /// <summary>
    /// 故障
    /// </summary>
    public WorkflowFault? Fault => _nodeInstance.Fault;

    /// <summary>
    /// 补偿时间
    /// </summary>
    public DateTimeOffset? CompensatedAt => _nodeInstance.CompensatedAt;
}
