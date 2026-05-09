// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Diagnostic;

/// <summary>
/// 诊断日志级别。
/// </summary>
public enum DiagnosticLevel
{
    /// <summary>
    /// 跟踪级别，最详细。
    /// </summary>
    Trace,

    /// <summary>
    /// 调试信息。
    /// </summary>
    Debug,

    /// <summary>
    /// 常规信息。
    /// </summary>
    Information,

    /// <summary>
    /// 警告，非预期但可恢复。
    /// </summary>
    Warning,

    /// <summary>
    /// 错误，需要关注。
    /// </summary>
    Error,

    /// <summary>
    /// 严重错误，系统级故障。
    /// </summary>
    Critical
}

/// <summary>
/// 诊断日志接口。
/// 框架内置轻量实现，无需外部依赖。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public interface IDiagnosticLogger
{
    /// <summary>
    /// 是否启用指定级别的日志。
    /// </summary>
    bool IsEnabled(DiagnosticLevel level);

    /// <summary>
    /// 写入日志。
    /// </summary>
    void Log(DiagnosticLevel level, string message, Exception? exception = null, params (string Key, object? Value)[] properties);
}
