// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Diagnostic;

/// <summary>
/// 诊断日志级别。
/// </summary>
public enum DiagnosticLevel
{
    Trace, Debug, Information, Warning, XiHanError, Critical
}

/// <summary>
/// 诊断日志接口。框架内置轻量实现，无需外部依赖。
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

/// <summary>
/// 诊断指标接口。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IDiagnosticMeter
{
    /// <summary>
    /// 记录一个计数器增量。
    /// </summary>
    void Increment(string name, long value = 1, params (string Key, object? Value)[] tags);

    /// <summary>
    /// 记录一个测量值。
    /// </summary>
    void Record(string name, double value, params (string Key, object? Value)[] tags);
}

/// <summary>
/// 分布式追踪接口。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IDiagnosticTracer
{
    /// <summary>
    /// 开始一个新的 Span。
    /// </summary>
    IDisposable StartSpan(string name, string? parentTraceId = null);

    /// <summary>
    /// 当前追踪 ID。
    /// </summary>
    string? CurrentTraceId { get; }
}

/// <summary>
/// 审计事件接收器。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IAuditSink
{
    /// <summary>
    /// 写入审计事件。
    /// </summary>
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// 审计事件模型。
/// </summary>
public sealed record AuditEvent(
    string EventType,
    string? UserId,
    string? TenantId,
    string? ResourceType,
    string? ResourceId,
    string? Action,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, object?>? Metadata = null
);
