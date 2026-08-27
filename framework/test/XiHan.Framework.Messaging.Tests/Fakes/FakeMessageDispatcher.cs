// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Models;

namespace XiHan.Framework.Messaging.Tests;

/// <summary>
/// 手写的消息调度器替身
/// </summary>
/// <remarks>
/// 仅用于验证 <c>TryAddSingleton</c> 的「已注册则不覆盖」语义，不承载任何分发逻辑。
/// </remarks>
internal sealed class FakeMessageDispatcher : IMessageDispatcher
{
    /// <summary>
    /// 分发消息到指定通道
    /// </summary>
    /// <param name="envelope">消息信封</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>空结果集合</returns>
    public Task<IReadOnlyList<MessageSendResult>> DispatchAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<MessageSendResult>>(Array.Empty<MessageSendResult>());
    }
}
