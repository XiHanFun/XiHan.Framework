// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectStorage.Tests.Fakes;

/// <summary>
/// 文件存储提供程序管理器替身
/// </summary>
/// <remarks>
/// 路由器用例只关心「路由器最终把哪个名字交给了管理器」，因此这里记录每次请求的名字，
/// 不复用真实管理器，避免把 DI 解析失败混进路由规则的断言里。
/// </remarks>
public class FakeFileStorageProviderManager : IFileStorageProviderManager
{
    private readonly Dictionary<string, IFileStorageProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 依次记录 GetProvider 收到的提供程序名称
    /// </summary>
    public List<string?> RequestedProviderNames { get; } = [];

    /// <summary>
    /// 登记一个可被解析的提供程序
    /// </summary>
    /// <param name="providerName">提供程序名称</param>
    /// <param name="provider">提供程序实例</param>
    public void Register(string providerName, IFileStorageProvider provider)
    {
        _providers[providerName] = provider;
    }

    /// <summary>
    /// 获取提供程序
    /// </summary>
    public IFileStorageProvider GetProvider(string? providerName = null)
    {
        RequestedProviderNames.Add(providerName);

        if (providerName is not null && _providers.TryGetValue(providerName, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"未注册对象存储提供程序：{providerName}");
    }

    /// <summary>
    /// 尝试获取提供程序
    /// </summary>
    public bool TryGetProvider(string? providerName, out IFileStorageProvider? provider)
    {
        provider = null;

        try
        {
            provider = GetProvider(providerName);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// 获取已注册的提供程序名称
    /// </summary>
    public IReadOnlyList<string> GetRegisteredProviderNames()
    {
        return _providers.Keys.ToArray();
    }
}
