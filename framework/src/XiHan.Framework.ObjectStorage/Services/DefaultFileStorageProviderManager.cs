#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultFileStorageProviderManager
// Guid:d283a8c4-cb65-44e2-b60a-56fd1612edb9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 01:52:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.ObjectStorage.Options;

namespace XiHan.Framework.ObjectStorage.Services;

/// <summary>
/// 默认文件存储提供程序管理器
/// </summary>
public class DefaultFileStorageProviderManager : IFileStorageProviderManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly XiHanObjectStorageProviderOptions _providerOptions;
    private readonly IOptions<XiHanObjectStorageOptions> _storageOptions;
    private readonly ConcurrentDictionary<string, IFileStorageProvider> _providerCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="providerOptions">提供程序注册选项</param>
    /// <param name="storageOptions">对象存储选项</param>
    public DefaultFileStorageProviderManager(
        IServiceProvider serviceProvider,
        IOptions<XiHanObjectStorageProviderOptions> providerOptions,
        IOptions<XiHanObjectStorageOptions> storageOptions)
    {
        _serviceProvider = serviceProvider;
        _providerOptions = providerOptions.Value;
        _storageOptions = storageOptions;
    }

    /// <summary>
    /// 获取提供程序
    /// </summary>
    /// <param name="providerName"></param>
    /// <returns></returns>
    public IFileStorageProvider GetProvider(string? providerName = null)
    {
        var finalProviderName = ResolveProviderName(providerName);

        if (!_providerOptions.ProviderTypes.TryGetValue(finalProviderName, out var providerType))
        {
            throw new InvalidOperationException($"未注册对象存储提供程序：{finalProviderName}");
        }

        return _providerCache.GetOrAdd(
            finalProviderName,
            static (name, state) =>
            {
                var resolved = state.serviceProvider.GetRequiredService(state.providerType);
                if (resolved is not IFileStorageProvider provider)
                {
                    throw new InvalidOperationException($"对象存储提供程序类型无效：{state.providerType.FullName}");
                }

                return provider;
            },
            (serviceProvider: _serviceProvider, providerType));
    }

    /// <summary>
    /// 尝试获取提供程序
    /// </summary>
    /// <param name="providerName"></param>
    /// <param name="provider"></param>
    /// <returns></returns>
    public bool TryGetProvider(string? providerName, out IFileStorageProvider? provider)
    {
        provider = null;
        try
        {
            provider = GetProvider(providerName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取已注册提供程序名称
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<string> GetRegisteredProviderNames()
    {
        return _providerOptions.ProviderTypes.Keys
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 解析提供程序名称
    /// </summary>
    /// <param name="providerName"></param>
    /// <returns></returns>
    private string ResolveProviderName(string? providerName)
    {
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            return providerName.Trim();
        }

        var configuredDefaultProvider = _storageOptions.Value.DefaultProvider;
        if (!string.IsNullOrWhiteSpace(configuredDefaultProvider))
        {
            return configuredDefaultProvider.Trim();
        }

        throw new InvalidOperationException("对象存储默认提供程序未配置。");
    }
}
