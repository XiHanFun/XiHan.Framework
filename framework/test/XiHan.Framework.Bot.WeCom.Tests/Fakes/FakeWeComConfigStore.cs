// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.WeCom.Abstractions;
using XiHan.Framework.Bot.WeCom.Options;

namespace XiHan.Framework.Bot.WeCom.Tests.Fakes;

/// <summary>
/// 可控的企业微信配置存储替身
/// </summary>
/// <remarks>
/// 用于验证提供者对「未配置 / 已禁用 / 缺 Key」的短路分支，以及取消令牌是否被透传下去。
/// </remarks>
internal sealed class FakeWeComConfigStore : IWeComConfigStore
{
    /// <summary>
    /// 创建替身
    /// </summary>
    /// <param name="options">要返回的配置；null 表示未配置</param>
    public FakeWeComConfigStore(WeComOptions? options)
    {
        Options = options;
    }

    /// <summary>
    /// 当前返回的配置
    /// </summary>
    public WeComOptions? Options { get; set; }

    /// <summary>
    /// 被调用次数
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// 最后一次收到的取消令牌
    /// </summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <inheritdoc />
    public Task<WeComOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Options);
    }
}
