#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateSecurityOptions
// Guid:020bbf4c-769a-4e89-acce-cca729923300
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:01:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Security;

/// <summary>
/// 模板安全选项
/// </summary>
public record TemplateSecurityOptions
{
    /// <summary>
    /// 最大模板大小（字节）
    /// </summary>
    public int MaxTemplateSize { get; init; } = 1024 * 1024; // 1MB

    /// <summary>
    /// 最大表达式深度
    /// </summary>
    public int MaxExpressionDepth { get; init; } = 10;

    /// <summary>
    /// 最大循环次数
    /// </summary>
    public int MaxLoopIterations { get; init; } = 10000;

    /// <summary>
    /// 最大包含文件数
    /// </summary>
    public int MaxIncludeFiles { get; init; } = 100;

    /// <summary>
    /// 是否允许文件访问
    /// </summary>
    public bool AllowFileAccess { get; init; } = false;

    /// <summary>
    /// 是否允许网络访问
    /// </summary>
    public bool AllowNetworkAccess { get; init; } = false;

    /// <summary>
    /// 是否允许反射
    /// </summary>
    public bool AllowReflection { get; init; } = false;

    /// <summary>
    /// 是否允许类型实例化
    /// </summary>
    public bool AllowTypeInstantiation { get; init; } = false;

    /// <summary>
    /// 允许的命名空间
    /// </summary>
    public ICollection<string> AllowedNamespaces { get; init; } = [];

    /// <summary>
    /// 禁止的命名空间
    /// </summary>
    public ICollection<string> ForbiddenNamespaces { get; init; } = [];

    /// <summary>
    /// 允许的类型
    /// </summary>
    public ICollection<Type> AllowedTypes { get; init; } = [];

    /// <summary>
    /// 禁止的类型
    /// </summary>
    public ICollection<Type> ForbiddenTypes { get; init; } = [];

    /// <summary>
    /// 允许的方法名
    /// </summary>
    public ICollection<string> AllowedMethods { get; init; } = [];

    /// <summary>
    /// 禁止的方法名
    /// </summary>
    public ICollection<string> ForbiddenMethods { get; init; } = [];

    /// <summary>
    /// 默认安全选项
    /// </summary>
    public static TemplateSecurityOptions Default => new();

    /// <summary>
    /// 严格安全选项
    /// </summary>
    public static TemplateSecurityOptions Strict => new()
    {
        AllowFileAccess = false,
        AllowNetworkAccess = false,
        AllowReflection = false,
        AllowTypeInstantiation = false,
        MaxTemplateSize = 100 * 1024, // 100KB
        MaxExpressionDepth = 5,
        MaxLoopIterations = 1000,
        MaxIncludeFiles = 10
    };

    /// <summary>
    /// 宽松安全选项
    /// </summary>
    public static TemplateSecurityOptions Relaxed => new()
    {
        AllowFileAccess = true,
        AllowNetworkAccess = false,
        AllowReflection = true,
        AllowTypeInstantiation = true,
        MaxTemplateSize = 5 * 1024 * 1024, // 5MB
        MaxExpressionDepth = 20,
        MaxLoopIterations = 100000,
        MaxIncludeFiles = 1000
    };
}
