// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Handlers;

/// <summary>
/// 消息处理器。返回失败或抛出异常都算投递失败。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessageHandler
{
    /// <summary>
    /// 处理。
    /// </summary>
    ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken);
}
