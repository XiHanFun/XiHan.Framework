// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectStorage.Options;

/// <summary>
/// 对象存储提供程序注册选项
/// </summary>
public class XiHanObjectStorageProviderOptions
{
    private readonly Dictionary<string, Type> _providerTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 已注册提供程序类型
    /// </summary>
    public IReadOnlyDictionary<string, Type> ProviderTypes => _providerTypes;

    /// <summary>
    /// 注册提供程序
    /// </summary>
    /// <param name="providerName">提供程序名称</param>
    /// <param name="providerType">提供程序类型</param>
    public void AddProvider(string providerName, Type providerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentNullException.ThrowIfNull(providerType);

        if (!typeof(IFileStorageProvider).IsAssignableFrom(providerType))
        {
            throw new InvalidOperationException(
                $"类型 {providerType.FullName} 未实现 {nameof(IFileStorageProvider)}。");
        }

        _providerTypes[providerName.Trim()] = providerType;
    }
}
