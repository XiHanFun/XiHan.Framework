#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplatePrecompilationResult
// Guid:9d79a0ca-c293-4077-ac12-7868e71ed46a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:18:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
