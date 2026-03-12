#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ResourceLocalizableString
// Guid:1c434253-f2e6-4f64-9654-2e3bcb4b6c77
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/13 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Localization;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 资源本地化字符串
/// </summary>
public sealed class ResourceLocalizableString : ILocalizableString
{
    /// <summary>
    /// 初始化资源本地化字符串
    /// </summary>
    /// <param name="resourceType">资源类型</param>
    /// <param name="name">资源键</param>
    /// <param name="arguments">格式化参数</param>
    public ResourceLocalizableString(Type resourceType, string name, params object[] arguments)
    {
        ResourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("资源键不能为空。", nameof(name))
            : name;
        Arguments = arguments ?? [];
    }

    /// <summary>
    /// 资源类型
    /// </summary>
    public Type ResourceType { get; }

    /// <summary>
    /// 资源键
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 格式化参数
    /// </summary>
    public object[] Arguments { get; }

    /// <summary>
    /// 执行本地化
    /// </summary>
    /// <param name="stringLocalizerFactory"></param>
    /// <returns></returns>
    public LocalizedString Localize(IStringLocalizerFactory stringLocalizerFactory)
    {
        ArgumentNullException.ThrowIfNull(stringLocalizerFactory);

        var localizer = stringLocalizerFactory.Create(ResourceType);
        return Arguments.Length == 0
            ? localizer[Name]
            : localizer[Name, Arguments];
    }
}
