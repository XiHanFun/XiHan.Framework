// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 把收到的信封交给 <see cref="EnvelopeRecorder"/> 的处理器
/// </summary>
internal sealed class RecordingEnvelopeHandler(EnvelopeRecorder recorder) : IMessageHandler
{
    /// <inheritdoc />
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        recorder.Record(delivery.Envelope);
        return ValueTask.FromResult(Result.Success());
    }
}
