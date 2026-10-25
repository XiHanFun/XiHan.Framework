// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Publishing;

/// <summary>
/// 流能力（<c>messaging.stream@1</c>）在队列之上多出的操作：按时刻重放。Provider 同时实现 <see cref="IMessageBroker"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IMessageReplayer
{
    /// <summary>
    /// 用独立的消费位点把主题上自 <paramref name="since"/> 起的消息重新投递给当前订阅的处理器：经完整管道、按同样的重试与死信规则，不影响正常消费的位点。
    /// 主题不存在或该时刻之后没有消息时成功返回；代理不可达返回 <see cref="MessagingErrors.BrokerUnavailable"/>。重放是有意的重复投递，去重由处理器自理。
    /// </summary>
    ValueTask<Result> ReplayAsync(string topic, DateTimeOffset since, CancellationToken cancellationToken);
}
