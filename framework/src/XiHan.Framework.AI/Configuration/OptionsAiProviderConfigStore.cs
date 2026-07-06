#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OptionsAiProviderConfigStore
// Guid:b2c3d4e5-f6a7-4b03-9d03-1a1b1c1d1e03
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 12:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.AI.Abstractions.Configuration;

namespace XiHan.Framework.AI.Configuration;

/// <summary>
/// 默认 provider 配置源:读 <see cref="IOptionsMonitor{XiHanAiOptions}"/>（appsettings 兜底）
/// </summary>
/// <remarks>
/// 框架默认实现,来源为 Options。应用层(BasicApp.Saas)用 SysAiProvider 实体 + DataProtection 的
/// DB 实现经 TryAddSingleton 覆盖本实现(store 化 + 多租户 + 热切换),对上层透明。
/// </remarks>
public sealed class OptionsAiProviderConfigStore : IAiProviderConfigStore
{
    private readonly IOptionsMonitor<XiHanAiOptions> _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    public OptionsAiProviderConfigStore(IOptionsMonitor<XiHanAiOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public Task<AiProviderOptions?> GetAsync(string? providerName = null, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var name = string.IsNullOrWhiteSpace(providerName) ? options.DefaultProvider : providerName;
        if (string.IsNullOrWhiteSpace(name) || !options.Providers.TryGetValue(name, out var provider))
        {
            return Task.FromResult<AiProviderOptions?>(null);
        }

        // 键即 provider 名:未显式填 Provider 字段时回填,便于工厂/日志识别
        if (string.IsNullOrWhiteSpace(provider.Provider))
        {
            provider.Provider = name;
        }

        return Task.FromResult<AiProviderOptions?>(provider);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AiProviderOptions>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var list = options.Providers
            .Select(kv =>
            {
                if (string.IsNullOrWhiteSpace(kv.Value.Provider))
                {
                    kv.Value.Provider = kv.Key;
                }

                return kv.Value;
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<AiProviderOptions>>(list);
    }
}
