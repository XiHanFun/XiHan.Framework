#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FrameworkModuleAttribute
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5f1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Attributes;

/// <summary>
/// 框架模块特性
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class FrameworkModuleAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="moduleName">模块名称</param>
    /// <param name="description">模块描述</param>
    /// <param name="version">模块版本</param>
    /// <param name="dependencies">依赖模块</param>
    /// <param name="isRequired">是否必需</param>
    public FrameworkModuleAttribute(string moduleName, string description = "", string version = "1.0.0", string[]? dependencies = null, bool isRequired = false)
    {
        ModuleName = moduleName;
        Description = description;
        Version = version;
        Dependencies = dependencies ?? [];
        IsRequired = isRequired;
    }

    /// <summary>
    /// 模块名称
    /// </summary>
    public string ModuleName { get; }

    /// <summary>
    /// 模块描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 模块版本
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// 依赖模块
    /// </summary>
    public string[] Dependencies { get; }

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool IsRequired { get; }
}
