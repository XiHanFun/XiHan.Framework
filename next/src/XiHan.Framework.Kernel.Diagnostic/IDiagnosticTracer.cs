// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Diagnostic;

/// <summary>
/// 分布式追踪接口。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IDiagnosticTracer
{
    /// <summary>
    /// 当前追踪 ID。
    /// </summary>
    string? CurrentTraceId { get; }

    /// <summary>
    /// 开始一个新的 Span。
    /// </summary>
    IDisposable StartSpan(string name, string? parentTraceId = null);
}
