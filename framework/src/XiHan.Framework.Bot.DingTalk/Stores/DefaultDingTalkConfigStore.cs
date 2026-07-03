#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultDingTalkConfigStore
// Guid:a5407cff-5162-4433-b315-c72b99909ea5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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

    /// <inheritdoc />
    public Task<DingTalkOptions?> GetAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<DingTalkOptions?>(_options.CurrentValue);
    }
}
