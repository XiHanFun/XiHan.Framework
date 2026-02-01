#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SettingDefinition
// Guid:3a5d0e1e-0b0a-4d2f-9c7d-3d8f1a7b2c1a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/23 11:11:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Settings.Providers;

namespace XiHan.Framework.Settings.Definitions;

/// <summary>
/// 设置定义
/// </summary>
public class SettingDefinition
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name"></param>
    /// <param name="defaultValue"></param>
    /// <param name="displayName"></param>
    /// <param name="description"></param>
    /// <param name="group"></param>
    /// <param name="isVisibleToClients"></param>
    /// <param name="isEncrypted"></param>
    /// <param name="validator"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public SettingDefinition(
        string name,
        string? defaultValue = null,
        string displayName = "",
        string description = "",
        string group = "General",
        bool isVisibleToClients = false,
        bool isEncrypted = false,
        Func<string?, bool>? validator = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DefaultValue = defaultValue;
        DisplayName = displayName;
        Description = description;
        Group = group;
        IsVisibleToClients = isVisibleToClients;
        IsEncrypted = isEncrypted;
        Validator = validator;
        Providers = [];
    }

    /// <summary>
    /// 设置名称(唯一标识)
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 默认值
    /// </summary>
    public string? DefaultValue { get; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 描述信息
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 所属分组
    /// </summary>
    public string Group { get; }

    /// <summary>
    /// 是否对客户端可见
    /// </summary>
    public bool IsVisibleToClients { get; }

    /// <summary>
    /// 是否加密存储
    /// </summary>
    public bool IsEncrypted { get; }

    /// <summary>
    /// 设置值提供程序
    /// </summary>
    public List<ISettingValueProvider> Providers { get; }

    /// <summary>
    /// 自定义验证函数
    /// </summary>
    public Func<string?, bool>? Validator { get; }

    /// <summary>
    /// 添加设置值提供程序
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public SettingDefinition AddProvider(ISettingValueProvider provider)
    {
        Providers.Add(provider);
        return this;
    }
}
