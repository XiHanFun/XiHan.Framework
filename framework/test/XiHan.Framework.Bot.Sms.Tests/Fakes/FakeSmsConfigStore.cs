// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Options;

namespace XiHan.Framework.Bot.Sms.Tests.Fakes;

/// <summary>
/// 短信配置存储手写替身
/// </summary>
/// <remarks>
/// 用于验证解析器的「每次解析都重新读取配置 + 按指纹缓存」契约：
/// <see cref="Config"/> 可在两次解析之间改写，模拟应用层热更新配置。
/// </remarks>
internal sealed class FakeSmsConfigStore : ISmsConfigStore
{
    /// <summary>
    /// 构造未配置的替身
    /// </summary>
    public FakeSmsConfigStore()
    {
    }

    /// <summary>
    /// 构造返回指定配置的替身
    /// </summary>
    /// <param name="config">当前生效配置</param>
    public FakeSmsConfigStore(SmsChannelConfig? config)
    {
        Config = config;
    }

    /// <summary>
    /// 当前生效配置，可在解析之间改写
    /// </summary>
    public SmsChannelConfig? Config { get; set; }

    /// <summary>
    /// 被读取的次数
    /// </summary>
    public int GetCount { get; private set; }

    /// <summary>
    /// 最后一次收到的取消令牌
    /// </summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>
    /// 获取当前生效配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前生效配置</returns>
    public Task<SmsChannelConfig?> GetAsync(CancellationToken cancellationToken = default)
    {
        GetCount++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Config);
    }
}
