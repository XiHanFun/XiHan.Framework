// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Localization;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 资源本地化字符串
/// </summary>
public sealed class ResourceLocalizableString : ILocalizableString
{
    /// <summary>
    /// 初始化资源本地化字符串（按资源类型）
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
    /// 初始化资源本地化字符串（按资源名称）
    /// 资源名称对应 JSON 资源文件中的 resource 字段（如 Errors、ApiResponse）
    /// </summary>
    /// <param name="resourceName">资源名称</param>
    /// <param name="name">资源键</param>
    /// <param name="arguments">格式化参数</param>
    public ResourceLocalizableString(string resourceName, string name, params object[] arguments)
    {
        ResourceName = string.IsNullOrWhiteSpace(resourceName)
            ? throw new ArgumentException("资源名称不能为空。", nameof(resourceName))
            : resourceName;
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("资源键不能为空。", nameof(name))
            : name;
        Arguments = arguments ?? [];
    }

    /// <summary>
    /// 资源类型（按资源名称构造时为 null）
    /// </summary>
    public Type? ResourceType { get; }

    /// <summary>
    /// 资源名称（按资源类型构造时为 null）
    /// </summary>
    public string? ResourceName { get; }

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

        var localizer = ResourceType is not null
            ? stringLocalizerFactory.Create(ResourceType)
            : stringLocalizerFactory.Create(ResourceName!, ResourceName!);
        return Arguments.Length == 0
            ? localizer[Name]
            : localizer[Name, Arguments];
    }
}
