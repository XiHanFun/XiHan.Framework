#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogActivity
// Guid:04e79c25-b1d8-4a63-9f50-c82e17d40b96
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 11:05:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;

namespace XiHan.Framework.Workflow.Activities.BuiltIn;

/// <summary>
/// 日志活动（输出运行日志，便于流程排障）
/// </summary>
/// <remarks>
/// 节点属性：<c>Message</c>（日志内容，支持模板）；<c>Level</c>（日志级别，默认 Information）。
/// </remarks>
[WorkflowActivity(WorkflowActivityTypes.Log, DisplayName = "日志", Category = "工具")]
public class LogActivity : WorkflowActivityBase
{
    private readonly ILogger<LogActivity> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public LogActivity(ILogger<LogActivity> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(ActivityExecutionContext context)
    {
        var message = await GetTemplatedStringAsync(context, "Message") ?? string.Empty;
        var levelText = GetProperty<string>(context, "Level");
        var level = Enum.TryParse<LogLevel>(levelText, ignoreCase: true, out var parsed) ? parsed : LogLevel.Information;

        _logger.Log(level, "[工作流 {InstanceId}/{NodeId}] {Message}", context.Instance.Id, context.Node.Id, message);
        return ActivityExecutionResult.Complete();
    }
}
