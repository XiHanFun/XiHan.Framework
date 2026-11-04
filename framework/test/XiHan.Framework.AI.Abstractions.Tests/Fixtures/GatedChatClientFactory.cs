// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Tests.Fixtures;

/// <summary>
/// <c>Kind = gated</c> 的聊天工厂：按 Provider 的时限建 <see cref="GatedChatClient"/>
/// </summary>
internal sealed class GatedChatClientFactory : IChatClientFactory
{
    /// <summary>
    /// 种类名
    /// </summary>
    public const string KindName = "gated";

    /// <summary>
    /// 时间源
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 闸门
    /// </summary>
    private readonly ProviderGate _gate;

    /// <summary>
    /// 创建
    /// </summary>
    public GatedChatClientFactory(TimeProvider timeProvider, ProviderGate gate)
    {
        _timeProvider = timeProvider;
        _gate = gate;
    }

    /// <inheritdoc />
    public string Kind => KindName;

    /// <inheritdoc />
    public Result<IChatClient> Create(AIProviderOptions options)
        => Result.Success<IChatClient>(new GatedChatClient(_timeProvider, options.Timeout, _gate));
}
