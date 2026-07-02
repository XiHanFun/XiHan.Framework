#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultEmailConfigStore
// Guid:5f730fb0-f276-455a-83d6-c15697b1c5c0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

    /// <inheritdoc />
    public Task<EmailOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<EmailOptions?>(_options.CurrentValue);
    }
}
