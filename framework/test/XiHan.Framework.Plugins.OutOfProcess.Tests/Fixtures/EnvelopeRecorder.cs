// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Messaging.Abstractions.Envelopes;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 记下宿主侧处理器收到的信封
/// </summary>
internal sealed class EnvelopeRecorder
{
    /// <summary>
    /// 收到的信封
    /// </summary>
    private readonly ConcurrentQueue<MessageEnvelope> _received = new();

    /// <summary>
    /// 收到的信封快照
    /// </summary>
    public IReadOnlyList<MessageEnvelope> Received => _received.ToArray();

    /// <summary>
    /// 记一条
    /// </summary>
    public void Record(MessageEnvelope envelope) => _received.Enqueue(envelope);

    /// <summary>
    /// 等到收到 ID 为 <paramref name="id"/> 的信封
    /// </summary>
    /// <remarks>
    /// 超时为 <see langword="null"/>
    /// </remarks>
    public async Task<MessageEnvelope?> WaitAsync(string id, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_received.FirstOrDefault(e => e.Id == id) is { } envelope)
            {
                return envelope;
            }

            await Task.Delay(50);
        }

        return _received.FirstOrDefault(e => e.Id == id);
    }
}
