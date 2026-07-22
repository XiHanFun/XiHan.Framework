// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Compilers;

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
