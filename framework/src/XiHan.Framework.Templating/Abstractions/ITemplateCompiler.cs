#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateCompiler
// Guid:5c0f4e9a-3d6f-4b9c-0f5e-4a9d6f3c0f5e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;

namespace XiHan.Framework.Templating.Abstractions;

/// <summary>
/// 模板编译器接口
/// </summary>
/// <typeparam name="T">编译结果类型</typeparam>
public interface ITemplateCompiler<T>
{
    /// <summary>
    /// 编译模板
    /// </summary>
    /// <param name="ast">模板抽象语法树</param>
    /// <param name="options">编译选项</param>
    /// <returns>编译结果</returns>
    T Compile(ITemplateAst ast, TemplateCompilerOptions? options = null);

    /// <summary>
    /// 编译模板源码
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="options">编译选项</param>
    /// <returns>编译结果</returns>
    T Compile(string templateSource, TemplateCompilerOptions? options = null);

    /// <summary>
    /// 异步编译模板
    /// </summary>
    /// <param name="ast">模板抽象语法树</param>
    /// <param name="options">编译选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编译结果</returns>
    Task<T> CompileAsync(ITemplateAst ast, TemplateCompilerOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// 表达式树编译器
/// </summary>
public interface IExpressionTreeCompiler : ITemplateCompiler<Expression<Func<ITemplateContext, string>>>
{
    /// <summary>
    /// 编译表达式
    /// </summary>
    /// <param name="expression">模板表达式</param>
    /// <returns>表达式树</returns>
    Expression<Func<ITemplateContext, object?>> CompileExpression(ITemplateExpression expression);

    /// <summary>
    /// 编译条件表达式
    /// </summary>
    /// <param name="conditional">条件节点</param>
    /// <returns>条件表达式树</returns>
    Expression<Func<ITemplateContext, bool>> CompileConditional(ITemplateConditional conditional);
}

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

/// <summary>
/// 编译结果
/// </summary>
/// <typeparam name="T">结果类型</typeparam>
public record TemplateCompilerResult<T>
{
    /// <summary>
    /// 编译结果
    /// </summary>
    public T? Result { get; init; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 编译时间（毫秒）
    /// </summary>
    public long CompilationTimeMs { get; init; }

    /// <summary>
    /// 编译诊断信息
    /// </summary>
    public ICollection<TemplateCompilerDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// 成功结果
    /// </summary>
    /// <param name="result">编译结果</param>
    /// <param name="compilationTime">编译时间</param>
    /// <returns>编译结果</returns>
    public static TemplateCompilerResult<T> Success(T result, long compilationTime = 0)
        => new() { Result = result, IsSuccess = true, CompilationTimeMs = compilationTime };

    /// <summary>
    /// 失败结果
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="compilationTime">编译时间</param>
    /// <returns>编译结果</returns>
    public static TemplateCompilerResult<T> Failure(string errorMessage, long compilationTime = 0)
        => new() { IsSuccess = false, ErrorMessage = errorMessage, CompilationTimeMs = compilationTime };
}

/// <summary>
/// 编译诊断信息
/// </summary>
public record TemplateCompilerDiagnostic
{
    /// <summary>
    /// 诊断级别
    /// </summary>
    public TemplateDiagnosticLevel Level { get; init; }

    /// <summary>
    /// 诊断消息
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 源位置
    /// </summary>
    public TemplateSourceLocation? Location { get; init; }

    /// <summary>
    /// 诊断代码
    /// </summary>
    public string? Code { get; init; }
}

/// <summary>
/// 诊断级别
/// </summary>
public enum TemplateDiagnosticLevel
{
    /// <summary>
    /// 信息
    /// </summary>
    Info,

    /// <summary>
    /// 警告
    /// </summary>
    Warning,

    /// <summary>
    /// 错误
    /// </summary>
    Error
}

/// <summary>
/// 模板源位置
/// </summary>
public record TemplateSourceLocation
{
    /// <summary>
    /// 行号
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// 列号
    /// </summary>
    public int Column { get; init; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// 源码片段
    /// </summary>
    public string? SourceSnippet { get; init; }
}
