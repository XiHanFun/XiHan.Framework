// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Abstractions.Runtime;

/// <summary>
/// 书签创建请求（活动挂起时声明等待点，标识与实例归属由引擎补齐）
/// </summary>
public class WorkflowBookmarkRequest
{
    /// <summary>
    /// 书签种类（见 <see cref="WorkflowBookmarkKinds"/>）
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 索引键（语义随种类而定）
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// 附加数据
    /// </summary>
    public Dictionary<string, object?> Payload { get; set; } = [];

    /// <summary>
    /// 到期时间
    /// </summary>
    public DateTime? DueTime { get; set; }

    /// <summary>
    /// 业务相关性标识
    /// </summary>
    public string? CorrelationId { get; set; }
}
