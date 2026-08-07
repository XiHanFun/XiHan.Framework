// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Lark.Abstractions;
using XiHan.Framework.Bot.Lark.Options;

namespace XiHan.Framework.Bot.Lark.Stores;

/// <summary>
/// 默认飞书配置存储（基于选项）
/// </summary>
public class DefaultLarkConfigStore : ILarkConfigStore
{
    private readonly IOptionsMonitor<LarkOptions> _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">飞书选项监视器</param>
    public DefaultLarkConfigStore(IOptionsMonitor<LarkOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// 获取当前生效配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>选项监视器的当前值</returns>
    public Task<LarkOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<LarkOptions?>(_options.CurrentValue);
    }
}
