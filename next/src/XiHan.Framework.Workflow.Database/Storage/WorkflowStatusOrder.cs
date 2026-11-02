// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Instances;

namespace XiHan.Framework.Workflow.Database.Storage;

/// <summary>
/// 状态在生命周期里的序号：列表按状态排序时比较它。
/// </summary>
/// <remarks>
/// 顺序照 RFC-0021「列表排序」（ADR-0103，同 4.x 按状态枚举的整数值排）：定义草稿 0、已发布 1、已停用 2、已归档 3；实例运行中 1、已挂起 2、已完成 3、已取消 4、故障 5、已终止 6。显式列出而不是取枚举的整数值：枚举的数值是公开 API，排序的先后是另一份约定，新增状态时各自决定。内存实现按同样的序号比较；本实现把它存进行的 <c>StatusOrder</c> 列，随每次整行写入维护。
/// </remarks>
internal static class WorkflowStatusOrder
{
    /// <summary>
    /// 定义状态的序号。
    /// </summary>
    public static int Of(WorkflowDefinitionStatus status) => status switch
    {
        WorkflowDefinitionStatus.Draft => 0,
        WorkflowDefinitionStatus.Published => 1,
        WorkflowDefinitionStatus.Disabled => 2,
        WorkflowDefinitionStatus.Archived => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, $"The workflow definition status '{status}' has no lifecycle order."),
    };

    /// <summary>
    /// 实例状态的序号。
    /// </summary>
    public static int Of(WorkflowInstanceStatus status) => status switch
    {
        WorkflowInstanceStatus.Running => 1,
        WorkflowInstanceStatus.Suspended => 2,
        WorkflowInstanceStatus.Completed => 3,
        WorkflowInstanceStatus.Canceled => 4,
        WorkflowInstanceStatus.Faulted => 5,
        WorkflowInstanceStatus.Terminated => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, $"The workflow instance status '{status}' has no lifecycle order."),
    };
}
