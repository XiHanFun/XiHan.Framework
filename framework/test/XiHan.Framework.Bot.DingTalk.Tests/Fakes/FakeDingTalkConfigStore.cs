// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.DingTalk.Abstractions;
using XiHan.Framework.Bot.DingTalk.Options;

namespace XiHan.Framework.Bot.DingTalk.Tests.Fakes;

/// <summary>
/// 钉钉配置存储手写替身
/// </summary>
/// <remarks>
/// 本仓测试栈禁止引入 mock 框架，提供者的编排分支只能靠手写替身驱动。
/// 该替身额外记录调用次数与收到的取消令牌，用于验证提供者是否把上下文令牌原样透传给配置存储。
/// </remarks>
public sealed class FakeDingTalkConfigStore : IDingTalkConfigStore
{
    private readonly DingTalkOptions? _options;

    /// <summary>
    /// 构造替身
    /// </summary>
    /// <param name="options">要返回的配置；传 null 表示"未配置"</param>
    public FakeDingTalkConfigStore(DingTalkOptions? options)
    {
        _options = options;
    }

    /// <summary>
    /// GetAsync 被调用的次数
    /// </summary>
    public int GetCallCount { get; private set; }

    /// <summary>
    /// 最近一次收到的取消令牌
    /// </summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>
    /// 返回预置配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>预置配置</returns>
    public Task<DingTalkOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        GetCallCount++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_options);
    }
}
