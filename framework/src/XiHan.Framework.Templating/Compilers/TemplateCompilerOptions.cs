// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 模板编译选项
/// </summary>
public record TemplateCompilerOptions
{
    /// <summary>
    /// 是否启用优化
    /// </summary>
    public bool EnableOptimization { get; init; } = true;

    /// <summary>
    /// 是否启用调试信息
    /// </summary>
    public bool EnableDebugInfo { get; init; } = false;

    /// <summary>
    /// 是否启用内联
    /// </summary>
    public bool EnableInlining { get; init; } = true;

    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool EnableCaching { get; init; } = true;

    /// <summary>
    /// 最大编译时间（毫秒）
    /// </summary>
    public int MaxCompilationTimeMs { get; init; } = 5000;

    /// <summary>
    /// 自定义函数
    /// </summary>
    public IDictionary<string, Delegate> CustomFunctions { get; init; } = new Dictionary<string, Delegate>();

    /// <summary>
    /// 导入的命名空间
    /// </summary>
    public ICollection<string> ImportedNamespaces { get; init; } = [];

    /// <summary>
    /// 默认选项
    /// </summary>
    public static TemplateCompilerOptions Default => new();

    /// <summary>
    /// 生产环境选项
    /// </summary>
    public static TemplateCompilerOptions Production => new()
    {
        EnableOptimization = true,
        EnableDebugInfo = false,
        EnableInlining = true,
        EnableCaching = true
    };

    /// <summary>
    /// 开发环境选项
    /// </summary>
    public static TemplateCompilerOptions Development => new()
    {
        EnableOptimization = false,
        EnableDebugInfo = true,
        EnableInlining = false,
        EnableCaching = false
    };
}
