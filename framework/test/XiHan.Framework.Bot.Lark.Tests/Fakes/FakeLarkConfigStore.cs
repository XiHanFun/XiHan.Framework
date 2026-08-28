// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Lark.Abstractions;
using XiHan.Framework.Bot.Lark.Options;

namespace XiHan.Framework.Bot.Lark.Tests.Fakes;

/// <summary>
/// 手写的飞书配置存储替身
/// </summary>
/// <remarks>
/// 除了返回预置配置，还记录调用次数与最近一次收到的取消令牌，
/// 用于验证 LarkBotProvider 「每次发送都重新取配置」以及「上下文取消令牌被透传」。
/// </remarks>
public sealed class FakeLarkConfigStore : ILarkConfigStore
{
    private readonly LarkOptions? _options;

    /// <summary>
    /// 构造替身
    /// </summary>
    /// <param name="options">要返回的配置；传 null 表示未配置</param>
    public FakeLarkConfigStore(LarkOptions? options)
    {
        _options = options;
    }

    /// <summary>
    /// 获取配置被调用的次数
    /// </summary>
    public int GetCallCount { get; private set; }

    /// <summary>
    /// 最近一次收到的取消令牌
    /// </summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>
    /// 获取当前生效配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>预置配置</returns>
    public Task<LarkOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        GetCallCount++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_options);
    }
}
