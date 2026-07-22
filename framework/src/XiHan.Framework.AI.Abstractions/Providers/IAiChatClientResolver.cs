// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;

namespace XiHan.Framework.AI.Abstractions.Providers;

/// <summary>
/// 多 provider 解析：按名取已构建好的 <see cref="IChatClient"/>（含中间件管道）
/// </summary>
/// <remarks>
/// 实现从 <c>IAiProviderConfigStore</c> 读配置，用 OpenAI 兼容适配器构建 IChatClient 并套
/// UseFunctionInvocation/UseOpenTelemetry/UseDistributedCache 中间件，按 provider 名缓存复用。
/// </remarks>
public interface IAiChatClientResolver
{
    /// <summary>
    /// 解析指定 provider 的 IChatClient（null 取默认 provider）
    /// </summary>
    IChatClient Resolve(string? providerName = null);

    /// <summary>
    /// 使已缓存的 IChatClient 失效（下次 <see cref="Resolve"/> 按最新配置重建）
    /// </summary>
    /// <remarks>
    /// 配置源（如应用层 DB store）改动 provider 的 key/baseUrl/model 后调用，实现配置热切换；
    /// <paramref name="providerName"/> 为空则清空全部缓存，否则清指定 provider 及默认槽（默认可能指向它）。
    /// </remarks>
    void Invalidate(string? providerName = null);
}
