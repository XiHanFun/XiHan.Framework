// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.WeCom.Options;

namespace XiHan.Framework.Bot.WeCom.Tests.Fakes;

/// <summary>
/// 可就地改值的企业微信选项监视器替身
/// </summary>
/// <remarks>
/// 默认配置存储只是 <c>IOptionsMonitor.CurrentValue</c> 的薄封装，
/// 用这个替身可以在不搭 DI 容器的情况下验证「热更新后读到新值」。
/// </remarks>
internal sealed class FakeWeComOptionsMonitor : IOptionsMonitor<WeComOptions>
{
    /// <summary>
    /// 创建替身
    /// </summary>
    /// <param name="current">初始配置</param>
    public FakeWeComOptionsMonitor(WeComOptions current)
    {
        CurrentValue = current;
    }

    /// <summary>
    /// 当前配置（可写，模拟热更新）
    /// </summary>
    public WeComOptions CurrentValue { get; set; }

    /// <inheritdoc />
    public WeComOptions Get(string? name)
    {
        return CurrentValue;
    }

    /// <inheritdoc />
    public IDisposable? OnChange(Action<WeComOptions, string?> listener)
    {
        return null;
    }
}
