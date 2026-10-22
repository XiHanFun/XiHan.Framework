// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// 应用发布消息的入口。主题不合法返回 <see cref="MessagingErrors.InvalidTopic"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IMessagePublisher
{
    /// <summary>
    /// 发布。
    /// </summary>
    ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken);
}
