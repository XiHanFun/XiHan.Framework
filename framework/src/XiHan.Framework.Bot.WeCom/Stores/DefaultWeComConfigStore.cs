#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultWeComConfigStore
// Guid:29cabf5a-cc82-41fa-8dbb-8289619f1d59
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.WeCom.Abstractions;
using XiHan.Framework.Bot.WeCom.Options;

namespace XiHan.Framework.Bot.WeCom.Stores;

/// <summary>
/// 默认企业微信配置存储（基于选项）
/// </summary>
public class DefaultWeComConfigStore : IWeComConfigStore
{
    private readonly IOptionsMonitor<WeComOptions> _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">企业微信选项监视器</param>
    public DefaultWeComConfigStore(IOptionsMonitor<WeComOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public Task<WeComOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<WeComOptions?>(_options.CurrentValue);
    }
}
