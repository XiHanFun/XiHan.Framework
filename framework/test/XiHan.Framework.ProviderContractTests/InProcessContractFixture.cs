// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ProviderContractTests;

/// <summary>
/// 进程内提供方的契约夹具，全部客户端都是同一个提供方实例
/// </summary>
/// <typeparam name="TProvider">被测提供方实现的公开契约类型</typeparam>
public sealed class InProcessContractFixture<TProvider> : IProviderContractFixture<TProvider>
    where TProvider : class
{
    private readonly TProvider _provider;
    private readonly ManualClock? _clock;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="provider">被测提供方实例</param>
    /// <param name="capabilities">提供方声明的能力，不可包含 <see cref="ProviderCapabilities.ClaimExpiry"/></param>
    /// <param name="clock">提供方使用的时钟，声明 <see cref="ProviderCapabilities.ControllableTime"/> 时必填</param>
    /// <exception cref="ArgumentException">能力与时钟不匹配</exception>
    public InProcessContractFixture(TProvider provider, ProviderCapabilities capabilities, ManualClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (capabilities.HasFlag(ProviderCapabilities.ControllableTime) && clock is null)
        {
            throw new ArgumentException("声明可控时间时必须提供时钟。", nameof(clock));
        }

        if (capabilities.HasFlag(ProviderCapabilities.ClaimExpiry))
        {
            throw new ArgumentException("进程内夹具不支持让领取过期。", nameof(capabilities));
        }

        _provider = provider;
        _clock = clock;
        Capabilities = capabilities;
    }

    /// <summary>
    /// 提供方声明的能力
    /// </summary>
    public ProviderCapabilities Capabilities { get; }

    /// <summary>
    /// 提供方判断到期所用的当前 UTC 时间
    /// </summary>
    public DateTime UtcNow => _clock?.Now ?? DateTime.UtcNow;

    /// <summary>
    /// 返回同一个提供方实例
    /// </summary>
    /// <returns>提供方实例</returns>
    public Task<TProvider> CreateClientAsync()
    {
        return Task.FromResult(_provider);
    }

    /// <summary>
    /// 推进时钟
    /// </summary>
    /// <param name="duration">推进的时长</param>
    /// <exception cref="NotSupportedException">未提供时钟</exception>
    public void AdvanceTime(TimeSpan duration)
    {
        if (_clock is null)
        {
            throw new NotSupportedException("本夹具未声明可控时间。");
        }

        _clock.Advance(duration);
    }

    /// <summary>
    /// 不支持
    /// </summary>
    /// <returns>不返回</returns>
    /// <exception cref="NotSupportedException">始终抛出</exception>
    public Task ExpireClaimsAsync()
    {
        throw new NotSupportedException("进程内夹具不支持让领取过期。");
    }

    /// <summary>
    /// 进程内实例随夹具一起丢弃，无需清理
    /// </summary>
    /// <returns>已完成的任务</returns>
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
