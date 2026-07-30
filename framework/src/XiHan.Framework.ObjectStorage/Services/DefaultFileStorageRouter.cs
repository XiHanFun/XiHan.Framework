// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.ObjectStorage.Options;

namespace XiHan.Framework.ObjectStorage.Services;

/// <summary>
/// 默认文件存储路由器
/// </summary>
public class DefaultFileStorageRouter : IFileStorageRouter
{
    private readonly IFileStorageProviderManager _providerManager;
    private readonly IOptions<XiHanObjectStorageOptions> _storageOptions;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="providerManager">提供程序管理器</param>
    /// <param name="storageOptions">对象存储配置</param>
    public DefaultFileStorageRouter(
        IFileStorageProviderManager providerManager,
        IOptions<XiHanObjectStorageOptions> storageOptions)
    {
        _providerManager = providerManager;
        _storageOptions = storageOptions;
    }

    /// <summary>
    /// 解析提供程序名称
    /// </summary>
    /// <param name="routeKey"></param>
    /// <param name="providerName"></param>
    /// <returns></returns>
    public string ResolveProviderName(string? routeKey = null, string? providerName = null)
    {
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            return providerName.Trim();
        }

        var options = _storageOptions.Value;
        var mappings = options.RouteProviderMappings ?? [];

        if (!string.IsNullOrWhiteSpace(routeKey))
        {
            var normalizedRouteKey = routeKey.Trim();
            if (mappings.TryGetValue(normalizedRouteKey, out var mappedProvider)
                && !string.IsNullOrWhiteSpace(mappedProvider))
            {
                return mappedProvider.Trim();
            }

            if (options.StrictRouteMatch)
            {
                throw new InvalidOperationException($"未找到对象存储路由映射：{normalizedRouteKey}");
            }
        }

        return options.DefaultProvider;
    }

    /// <summary>
    /// 路由并获取提供程序
    /// </summary>
    /// <param name="routeKey"></param>
    /// <param name="providerName"></param>
    /// <returns></returns>
    public IFileStorageProvider Route(string? routeKey = null, string? providerName = null)
    {
        var resolvedProviderName = ResolveProviderName(routeKey, providerName);
        return _providerManager.GetProvider(resolvedProviderName);
    }
}
