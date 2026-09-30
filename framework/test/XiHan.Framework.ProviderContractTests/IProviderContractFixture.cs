// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ProviderContractTests;

/// <summary>
/// 提供方契约测试夹具
/// </summary>
/// <remarks>
/// 每个契约用例创建一个夹具并在结束时释放；同一夹具创建的客户端共享底层存储，释放夹具即清理本夹具使用的存储。
/// </remarks>
/// <typeparam name="TProvider">被测提供方实现的公开契约类型</typeparam>
public interface IProviderContractFixture<TProvider> : IAsyncDisposable
    where TProvider : class
{
    /// <summary>
    /// 提供方声明的能力
    /// </summary>
    ProviderCapabilities Capabilities { get; }

    /// <summary>
    /// 提供方判断到期所用的当前 UTC 时间
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// 创建一个客户端
    /// </summary>
    /// <returns>连到本夹具底层存储的提供方实例</returns>
    Task<TProvider> CreateClientAsync();

    /// <summary>
    /// 推进时间
    /// </summary>
    /// <param name="duration">推进的时长</param>
    /// <exception cref="NotSupportedException">未声明 <see cref="ProviderCapabilities.ControllableTime"/></exception>
    void AdvanceTime(TimeSpan duration);

    /// <summary>
    /// 让当前全部领取立即过期
    /// </summary>
    /// <returns>任务</returns>
    /// <exception cref="NotSupportedException">未声明 <see cref="ProviderCapabilities.ClaimExpiry"/></exception>
    Task ExpireClaimsAsync();
}
