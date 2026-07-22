// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
