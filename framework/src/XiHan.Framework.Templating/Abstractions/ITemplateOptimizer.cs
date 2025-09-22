#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateOptimizer
// Guid:0h5j9j4f-8i1k-9g4h-5j0j-9f4i1k8h5j0j
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Abstractions;

/// <summary>
/// 模板优化器接口
/// </summary>
public interface ITemplateOptimizer
{
    /// <summary>
    /// 优化模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="options">优化选项</param>
    /// <returns>优化结果</returns>
    TemplateOptimizationResult Optimize(string templateSource, TemplateOptimizationOptions? options = null);

    /// <summary>
    /// 异步优化模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="options">优化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>优化结果</returns>
    Task<TemplateOptimizationResult> OptimizeAsync(string templateSource, TemplateOptimizationOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 优化模板AST
    /// </summary>
    /// <param name="ast">模板抽象语法树</param>
    /// <param name="options">优化选项</param>
    /// <returns>优化后的AST</returns>
    ITemplateAst OptimizeAst(ITemplateAst ast, TemplateOptimizationOptions? options = null);

    /// <summary>
    /// 分析模板性能
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>性能分析报告</returns>
    TemplatePerformanceReport AnalyzePerformance(string templateSource);
}

/// <summary>
/// 模板预编译器
/// </summary>
public interface ITemplatePrecompiler
{
    /// <summary>
    /// 预编译模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="templateName">模板名称</param>
    /// <param name="options">预编译选项</param>
    /// <returns>预编译结果</returns>
    TemplatePrecompilationResult Precompile(string templateSource, string templateName, TemplatePrecompilationOptions? options = null);

    /// <summary>
    /// 异步预编译模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="templateName">模板名称</param>
    /// <param name="options">预编译选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>预编译结果</returns>
    Task<TemplatePrecompilationResult> PrecompileAsync(string templateSource, string templateName, TemplatePrecompilationOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量预编译模板
    /// </summary>
    /// <param name="templates">模板字典</param>
    /// <param name="options">预编译选项</param>
    /// <returns>预编译结果集合</returns>
    Task<IDictionary<string, TemplatePrecompilationResult>> PrecompileBatchAsync(IDictionary<string, string> templates, TemplatePrecompilationOptions? options = null);

    /// <summary>
    /// 预编译目录中的所有模板
    /// </summary>
    /// <param name="directory">模板目录</param>
    /// <param name="pattern">文件模式</param>
    /// <param name="options">预编译选项</param>
    /// <returns>预编译结果集合</returns>
    Task<IDictionary<string, TemplatePrecompilationResult>> PrecompileDirectoryAsync(string directory, string pattern = "*.html", TemplatePrecompilationOptions? options = null);

    /// <summary>
    /// 获取预编译结果
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <returns>预编译结果</returns>
    TemplatePrecompilationResult? GetPrecompiledTemplate(string templateName);

    /// <summary>
    /// 清空预编译缓存
    /// </summary>
    void ClearPrecompiledCache();
}

/// <summary>
/// 模板优化选项
/// </summary>
public record TemplateOptimizationOptions
{
    /// <summary>
    /// 是否启用常量折叠
    /// </summary>
    public bool EnableConstantFolding { get; init; } = true;

    /// <summary>
    /// 是否启用死代码消除
    /// </summary>
    public bool EnableDeadCodeElimination { get; init; } = true;

    /// <summary>
    /// 是否启用内联优化
    /// </summary>
    public bool EnableInlining { get; init; } = true;

    /// <summary>
    /// 是否启用循环优化
    /// </summary>
    public bool EnableLoopOptimization { get; init; } = true;

    /// <summary>
    /// 是否启用表达式优化
    /// </summary>
    public bool EnableExpressionOptimization { get; init; } = true;

    /// <summary>
    /// 是否启用字符串优化
    /// </summary>
    public bool EnableStringOptimization { get; init; } = true;

    /// <summary>
    /// 是否启用缓存优化
    /// </summary>
    public bool EnableCacheOptimization { get; init; } = true;

    /// <summary>
    /// 内联阈值
    /// </summary>
    public int InlineThreshold { get; init; } = 100;

    /// <summary>
    /// 最大优化级别
    /// </summary>
    public OptimizationLevel MaxOptimizationLevel { get; init; } = OptimizationLevel.Aggressive;

    /// <summary>
    /// 优化超时时间（毫秒）
    /// </summary>
    public int OptimizationTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// 默认优化选项
    /// </summary>
    public static TemplateOptimizationOptions Default => new();

    /// <summary>
    /// 保守优化选项
    /// </summary>
    public static TemplateOptimizationOptions Conservative => new()
    {
        EnableConstantFolding = true,
        EnableDeadCodeElimination = false,
        EnableInlining = false,
        EnableLoopOptimization = false,
        EnableExpressionOptimization = true,
        EnableStringOptimization = true,
        EnableCacheOptimization = true,
        MaxOptimizationLevel = OptimizationLevel.Basic
    };

    /// <summary>
    /// 激进优化选项
    /// </summary>
    public static TemplateOptimizationOptions Aggressive => new()
    {
        EnableConstantFolding = true,
        EnableDeadCodeElimination = true,
        EnableInlining = true,
        EnableLoopOptimization = true,
        EnableExpressionOptimization = true,
        EnableStringOptimization = true,
        EnableCacheOptimization = true,
        InlineThreshold = 1000,
        MaxOptimizationLevel = OptimizationLevel.Aggressive
    };
}

/// <summary>
/// 模板预编译选项
/// </summary>
public record TemplatePrecompilationOptions
{
    /// <summary>
    /// 是否启用压缩
    /// </summary>
    public bool EnableCompression { get; init; } = true;

    /// <summary>
    /// 是否生成调试信息
    /// </summary>
    public bool GenerateDebugInfo { get; init; } = false;

    /// <summary>
    /// 是否生成源映射
    /// </summary>
    public bool GenerateSourceMap { get; init; } = false;

    /// <summary>
    /// 目标格式
    /// </summary>
    public PrecompilationTargetFormat TargetFormat { get; init; } = PrecompilationTargetFormat.Binary;

    /// <summary>
    /// 输出目录
    /// </summary>
    public string? OutputDirectory { get; init; }

    /// <summary>
    /// 是否保留原始模板
    /// </summary>
    public bool PreserveOriginalTemplate { get; init; } = false;

    /// <summary>
    /// 优化选项
    /// </summary>
    public TemplateOptimizationOptions? OptimizationOptions { get; init; }

    /// <summary>
    /// 默认预编译选项
    /// </summary>
    public static TemplatePrecompilationOptions Default => new();

    /// <summary>
    /// 生产环境预编译选项
    /// </summary>
    public static TemplatePrecompilationOptions Production => new()
    {
        EnableCompression = true,
        GenerateDebugInfo = false,
        GenerateSourceMap = false,
        TargetFormat = PrecompilationTargetFormat.Binary,
        PreserveOriginalTemplate = false,
        OptimizationOptions = TemplateOptimizationOptions.Aggressive
    };

    /// <summary>
    /// 开发环境预编译选项
    /// </summary>
    public static TemplatePrecompilationOptions Development => new()
    {
        EnableCompression = false,
        GenerateDebugInfo = true,
        GenerateSourceMap = true,
        TargetFormat = PrecompilationTargetFormat.Source,
        PreserveOriginalTemplate = true,
        OptimizationOptions = TemplateOptimizationOptions.Conservative
    };
}

/// <summary>
/// 优化级别
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// 无优化
    /// </summary>
    None = 0,

    /// <summary>
    /// 基础优化
    /// </summary>
    Basic = 1,

    /// <summary>
    /// 标准优化
    /// </summary>
    Standard = 2,

    /// <summary>
    /// 激进优化
    /// </summary>
    Aggressive = 3
}

/// <summary>
/// 预编译目标格式
/// </summary>
public enum PrecompilationTargetFormat
{
    /// <summary>
    /// 源代码格式
    /// </summary>
    Source,

    /// <summary>
    /// 二进制格式
    /// </summary>
    Binary,

    /// <summary>
    /// 字节码格式
    /// </summary>
    Bytecode,

    /// <summary>
    /// 程序集格式
    /// </summary>
    Assembly
}

/// <summary>
/// 模板优化结果
/// </summary>
public record TemplateOptimizationResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 优化后的模板
    /// </summary>
    public string? OptimizedTemplate { get; init; }

    /// <summary>
    /// 优化后的AST
    /// </summary>
    public ITemplateAst? OptimizedAst { get; init; }

    /// <summary>
    /// 优化统计
    /// </summary>
    public TemplateOptimizationStats Stats { get; init; } = new();

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 优化时间（毫秒）
    /// </summary>
    public long OptimizationTimeMs { get; init; }

    /// <summary>
    /// 优化建议
    /// </summary>
    public ICollection<string> Suggestions { get; init; } = [];
}

/// <summary>
/// 模板预编译结果
/// </summary>
public record TemplatePrecompilationResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 预编译数据
    /// </summary>
    public byte[]? CompiledData { get; init; }

    /// <summary>
    /// 源映射
    /// </summary>
    public string? SourceMap { get; init; }

    /// <summary>
    /// 调试信息
    /// </summary>
    public string? DebugInfo { get; init; }

    /// <summary>
    /// 预编译统计
    /// </summary>
    public TemplatePrecompilationStats Stats { get; init; } = new();

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 预编译时间（毫秒）
    /// </summary>
    public long PrecompilationTimeMs { get; init; }
}

/// <summary>
/// 模板优化统计
/// </summary>
public record TemplateOptimizationStats
{
    /// <summary>
    /// 原始大小（字节）
    /// </summary>
    public int OriginalSize { get; init; }

    /// <summary>
    /// 优化后大小（字节）
    /// </summary>
    public int OptimizedSize { get; init; }

    /// <summary>
    /// 压缩比例
    /// </summary>
    public double CompressionRatio => OriginalSize > 0 ? (double)OptimizedSize / OriginalSize : 0;

    /// <summary>
    /// 移除的死代码行数
    /// </summary>
    public int DeadCodeLinesRemoved { get; init; }

    /// <summary>
    /// 内联的表达式数量
    /// </summary>
    public int InlinedExpressions { get; init; }

    /// <summary>
    /// 优化的循环数量
    /// </summary>
    public int OptimizedLoops { get; init; }

    /// <summary>
    /// 常量折叠次数
    /// </summary>
    public int ConstantFolds { get; init; }

    /// <summary>
    /// 应用的优化次数
    /// </summary>
    public int OptimizationsApplied { get; init; }
}

/// <summary>
/// 模板预编译统计
/// </summary>
public record TemplatePrecompilationStats
{
    /// <summary>
    /// 原始大小（字节）
    /// </summary>
    public int OriginalSize { get; init; }

    /// <summary>
    /// 编译后大小（字节）
    /// </summary>
    public int CompiledSize { get; init; }

    /// <summary>
    /// 压缩比例
    /// </summary>
    public double CompressionRatio => OriginalSize > 0 ? (double)CompiledSize / OriginalSize : 0;

    /// <summary>
    /// 依赖数量
    /// </summary>
    public int DependencyCount { get; init; }

    /// <summary>
    /// 生成的指令数
    /// </summary>
    public int InstructionCount { get; init; }

    /// <summary>
    /// 缓存键
    /// </summary>
    public string? CacheKey { get; init; }
}

/// <summary>
/// 模板性能报告
/// </summary>
public record TemplatePerformanceReport
{
    /// <summary>
    /// 模板复杂度
    /// </summary>
    public int ComplexityScore { get; init; }

    /// <summary>
    /// 预估渲染时间（毫秒）
    /// </summary>
    public double EstimatedRenderTimeMs { get; init; }

    /// <summary>
    /// 内存使用预估（字节）
    /// </summary>
    public long EstimatedMemoryUsage { get; init; }

    /// <summary>
    /// 性能瓶颈
    /// </summary>
    public ICollection<PerformanceBottleneck> Bottlenecks { get; init; } = [];

    /// <summary>
    /// 优化建议
    /// </summary>
    public ICollection<string> OptimizationSuggestions { get; init; } = [];

    /// <summary>
    /// 性能评级
    /// </summary>
    public PerformanceRating Rating { get; init; }
}

/// <summary>
/// 性能瓶颈
/// </summary>
public record PerformanceBottleneck
{
    /// <summary>
    /// 瓶颈类型
    /// </summary>
    public BottleneckType Type { get; init; }

    /// <summary>
    /// 瓶颈描述
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 影响程度
    /// </summary>
    public ImpactLevel Impact { get; init; }

    /// <summary>
    /// 瓶颈位置
    /// </summary>
    public TemplateSourceLocation? Location { get; init; }

    /// <summary>
    /// 修复建议
    /// </summary>
    public string? Suggestion { get; init; }
}

/// <summary>
/// 瓶颈类型
/// </summary>
public enum BottleneckType
{
    /// <summary>
    /// 复杂表达式
    /// </summary>
    ComplexExpression,

    /// <summary>
    /// 嵌套循环
    /// </summary>
    NestedLoop,

    /// <summary>
    /// 大量字符串连接
    /// </summary>
    StringConcatenation,

    /// <summary>
    /// 频繁反射调用
    /// </summary>
    FrequentReflection,

    /// <summary>
    /// 未缓存的计算
    /// </summary>
    UncachedComputation,

    /// <summary>
    /// 大对象分配
    /// </summary>
    LargeObjectAllocation
}

/// <summary>
/// 影响程度
/// </summary>
public enum ImpactLevel
{
    /// <summary>
    /// 低影响
    /// </summary>
    Low,

    /// <summary>
    /// 中等影响
    /// </summary>
    Medium,

    /// <summary>
    /// 高影响
    /// </summary>
    High,

    /// <summary>
    /// 严重影响
    /// </summary>
    Critical
}

/// <summary>
/// 性能评级
/// </summary>
public enum PerformanceRating
{
    /// <summary>
    /// 优秀
    /// </summary>
    Excellent,

    /// <summary>
    /// 良好
    /// </summary>
    Good,

    /// <summary>
    /// 一般
    /// </summary>
    Fair,

    /// <summary>
    /// 较差
    /// </summary>
    Poor,

    /// <summary>
    /// 很差
    /// </summary>
    VeryPoor
}
