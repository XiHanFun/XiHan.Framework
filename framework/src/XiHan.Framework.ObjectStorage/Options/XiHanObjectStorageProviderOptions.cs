#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanObjectStorageProviderOptions
// Guid:e2d934dd-6a40-4e49-9233-28f1505014fd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 01:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
