// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Handlers;

/// <summary>
/// 消息处理器
/// </summary>
/// <remarks>
/// 返回失败或抛出异常都算投递失败
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessageHandler
{
    /// <summary>
    /// 处理
    /// </summary>
    /// <remarks>
    /// 取消与截止时间在 <see cref="InvocationContext.CancellationToken"/> 上，与操作的处理器、作业一样不另传令牌（ADR-0263）
    /// </remarks>
    ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context);
}
