// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using Error = XiHan.Framework.Primitives.Error;
using ProtoStatus = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace XiHan.Framework.Plugins.Protocol;

/// <summary>
/// <c>Host.Publish</c> 的批量分组：两种宿主共用。逐项校验主题与信封，不合法的直接写结果；通过的按主题分组（保持首次出现的主题顺序与组内顺序），
/// 宿主对每组调用一次 <see cref="IMessagePublisher.PublishManyAsync(string, IReadOnlyList{MessageEnvelope}, CancellationToken)"/> 后把逐项结果回填到原始位置。
/// </summary>
internal static class PluginPublishBatches
{
    /// <summary>
    /// 分组。<paramref name="unavailable"/> 非空表示宿主没有消息发布器：每一项都写该错误，不分组。
    /// </summary>
    /// <param name="envelopes">请求的信封。</param>
    /// <param name="statuses">与信封等长的结果数组；校验失败的位置在此写入。</param>
    /// <param name="unavailable">宿主没有消息发布器时的错误。</param>
    public static IReadOnlyList<(string Topic, IReadOnlyList<int> Indexes, IReadOnlyList<MessageEnvelope> Messages)> Collect(IReadOnlyList<Envelope> envelopes, ProtoStatus[] statuses, Error? unavailable)
    {
        ArgumentNullException.ThrowIfNull(envelopes);
        ArgumentNullException.ThrowIfNull(statuses);
        var groups = new Dictionary<string, (List<int> Indexes, List<MessageEnvelope> Messages)>(StringComparer.Ordinal);
        var topics = new List<string>();
        for (var index = 0; index < envelopes.Count; index++)
        {
            var envelope = envelopes[index];
            if (unavailable is not null)
            {
                statuses[index] = PluginErrorMapper.ToStatus(unavailable);
                continue;
            }

            if (!TopicName.IsValid(envelope.Topic))
            {
                statuses[index] = PluginErrorMapper.ToStatus(Error.Validation(MessagingErrors.InvalidTopic, MessagingMessages.InvalidTopic, MessageArguments.Of(("topic", envelope.Topic))));
                continue;
            }

            var converted = PluginEnvelopeMapper.FromProto(envelope);
            if (!converted.TryGetValue(out var message))
            {
                statuses[index] = PluginErrorMapper.ToStatus(converted.Error!);
                continue;
            }

            if (!groups.TryGetValue(envelope.Topic, out var group))
            {
                group = ([], []);
                groups[envelope.Topic] = group;
                topics.Add(envelope.Topic);
            }

            group.Indexes.Add(index);
            group.Messages.Add(message);
        }

        var batches = new List<(string, IReadOnlyList<int>, IReadOnlyList<MessageEnvelope>)>(topics.Count);
        foreach (var topic in topics)
        {
            var (indexes, messages) = groups[topic];
            batches.Add((topic, indexes, messages));
        }

        return batches;
    }

    /// <summary>
    /// 把一组的逐项结果回填到原始位置。
    /// </summary>
    public static void Scatter(ProtoStatus[] statuses, IReadOnlyList<int> indexes, IReadOnlyList<Result> results)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentNullException.ThrowIfNull(indexes);
        ArgumentNullException.ThrowIfNull(results);
        if (results.Count != indexes.Count)
        {
            throw new InvalidOperationException($"The publisher returned {results.Count} results for {indexes.Count} envelopes.");
        }

        for (var position = 0; position < indexes.Count; position++)
        {
            statuses[indexes[position]] = PluginErrorMapper.ToStatus(results[position]);
        }
    }
}
