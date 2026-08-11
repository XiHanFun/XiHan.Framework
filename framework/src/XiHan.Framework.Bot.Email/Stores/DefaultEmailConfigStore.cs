// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Email.Abstractions;
using XiHan.Framework.Bot.Email.Options;

namespace XiHan.Framework.Bot.Email.Stores;

/// <summary>
/// 默认邮件配置存储（基于选项）
/// </summary>
public class DefaultEmailConfigStore : IEmailConfigStore
{
    private readonly IOptionsMonitor<EmailOptions> _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">邮件选项监视器</param>
    public DefaultEmailConfigStore(IOptionsMonitor<EmailOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// 获取当前生效配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>选项监视器的当前值</returns>
    public Task<EmailOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<EmailOptions?>(_options.CurrentValue);
    }
}
