// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.DingTalk.Abstractions;
using XiHan.Framework.Bot.DingTalk.Options;

namespace XiHan.Framework.Bot.DingTalk.Stores;

/// <summary>
/// 默认钉钉配置存储（基于选项）
/// </summary>
public class DefaultDingTalkConfigStore : IDingTalkConfigStore
{
    private readonly IOptionsMonitor<DingTalkOptions> _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">钉钉选项监视器</param>
    public DefaultDingTalkConfigStore(IOptionsMonitor<DingTalkOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// 获取当前生效配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>选项监视器的当前值</returns>
    public Task<DingTalkOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<DingTalkOptions?>(_options.CurrentValue);
    }
}
