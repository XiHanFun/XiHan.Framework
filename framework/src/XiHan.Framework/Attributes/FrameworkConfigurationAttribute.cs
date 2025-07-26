#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FrameworkConfigurationAttribute
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5f2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Attributes;

/// <summary>
/// 框架配置特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public class FrameworkConfigurationAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="configurationKey">配置键</param>
    /// <param name="description">配置描述</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="isRequired">是否必需</param>
    public FrameworkConfigurationAttribute(string configurationKey, string description = "", object? defaultValue = null, bool isRequired = false)
    {
        ConfigurationKey = configurationKey;
        Description = description;
        DefaultValue = defaultValue;
        IsRequired = isRequired;
    }

    /// <summary>
    /// 配置键
    /// </summary>
    public string ConfigurationKey { get; }

    /// <summary>
    /// 配置描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; }

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool IsRequired { get; }
}
