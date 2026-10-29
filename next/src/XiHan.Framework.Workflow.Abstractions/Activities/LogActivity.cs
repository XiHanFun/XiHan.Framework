// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Activities;

/// <summary>
/// 写日志。
/// </summary>
/// <remarks>
/// <c>Message</c>（模板）按 <c>Level</c>（默认 Information）写到 <c>XiHan.Framework.Workflow.Abstractions.Activities.LogActivity</c> 日志类别。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class LogActivity : IActivity
{
    /// <summary>
    /// 属性：消息模板。
    /// </summary>
    public const string MessageProperty = "Message";

    /// <summary>
    /// 属性：级别。
    /// </summary>
    public const string LevelProperty = "Level";

    /// <inheritdoc />
    public ValueTask<Result<ActivityOutcome>> ExecuteAsync(ActivityContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rendered = context.RenderProperty(MessageProperty);
        if (!rendered.TryGetValue(out var message))
        {
            return ValueTask.FromResult(Result.Failure<ActivityOutcome>(rendered.Error!));
        }

        var level = context.Property(LevelProperty, LogLevel.Information);
        if (context.Services.GetService<ILoggerFactory>() is { } factory)
        {
            var logger = factory.CreateLogger<LogActivity>();
            if (logger.IsEnabled(level))
            {
                LogMessage(logger, level, context.Instance.Id, context.Node.Id, message);
            }
        }

        return ValueTask.FromResult(Result.Success(ActivityOutcome.Complete()));
    }

    /// <summary>
    /// 节点日志。
    /// </summary>
    [LoggerMessage(Message = "Workflow instance '{InstanceId}' node '{NodeId}': {Message}")]
    private static partial void LogMessage(ILogger logger, LogLevel level, string instanceId, string nodeId, string message);
}
