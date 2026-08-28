// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Sms.Abstractions;

namespace XiHan.Framework.Bot.Sms.Tests.Fakes;

/// <summary>
/// 短信网关解析器手写替身
/// </summary>
/// <remarks>
/// 直接回放预置客户端（可为 null，模拟未配置/已禁用），用于隔离验证 <c>SmsBotProvider</c> 的 fail-closed 分支。
/// </remarks>
internal sealed class FakeSmsGatewayResolver : ISmsGatewayResolver
{
    private readonly ISmsGatewayClient? _client;

    /// <summary>
    /// 构造替身
    /// </summary>
    /// <param name="client">预置客户端；null 表示未配置或已禁用</param>
    public FakeSmsGatewayResolver(ISmsGatewayClient? client)
    {
        _client = client;
    }

    /// <summary>
    /// 解析次数
    /// </summary>
    public int ResolveCount { get; private set; }

    /// <summary>
    /// 最后一次收到的取消令牌
    /// </summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>
    /// 回放预置客户端
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>预置客户端</returns>
    public Task<ISmsGatewayClient?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        ResolveCount++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_client);
    }
}
