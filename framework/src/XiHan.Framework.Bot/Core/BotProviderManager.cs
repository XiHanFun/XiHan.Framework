#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotProviderManager
// Guid:529f138c-57a9-4b7b-adf4-d7aa888cc145
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:44:27
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Core;

/// <summary>
/// 提供者管理器
/// </summary>
public class BotProviderManager
{
    private readonly IReadOnlyList<IBotProvider> _providers;
    private readonly XiHanBotOptions _options;

    /// <summary>
    /// 创建管理器
    /// </summary>
    public BotProviderManager(IEnumerable<IBotProvider> providers, IOptions<XiHanBotOptions> options)
    {
        _providers = providers.ToArray();
        _options = options.Value;
    }

    /// <summary>
    /// 获取所有提供者
    /// </summary>
    public IReadOnlyList<IBotProvider> GetAllProviders()
    {
        return _providers;
    }

    /// <summary>
    /// 按渠道或提供者名称解析出提供者列表
    /// </summary>
    public IReadOnlyList<IBotProvider> ResolveProviders(IReadOnlyList<string>? channels)
    {
        if (channels is null || channels.Count == 0)
        {
            return _providers;
        }

        var providerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var channel in channels)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                continue;
            }

            var normalized = channel.Trim();
            if (_options.Channels.TryGetValue(normalized, out var botChannel))
            {
                if (botChannel.Providers is null)
                {
                    continue;
                }

                foreach (var providerName in botChannel.Providers)
                {
                    if (!string.IsNullOrWhiteSpace(providerName))
                    {
                        providerNames.Add(providerName.Trim());
                    }
                }
            }
            else
            {
                providerNames.Add(normalized);
            }
        }

        if (providerNames.Count == 0)
        {
            return [];
        }

        return _providers.Where(provider => providerNames.Contains(provider.Name)).ToArray();
    }
}
