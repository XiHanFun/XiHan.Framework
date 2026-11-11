// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 创建调用上下文时由调用方给出的值
/// </summary>
/// <remarks>
/// 都可省略：载体给出的追踪、绝对截止时间与 Baggage。成员经初始化器给出，以后加字段只加属性、不改 <see cref="App.CreateContext(IServiceProvider, InvocationContextOptions, CancellationToken)"/> 的参数表（ADR-0157）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record InvocationContextOptions
{
    /// <summary>
    /// 不带任何值的选项
    /// </summary>
    public static InvocationContextOptions None { get; } = new();

    /// <summary>
    /// 载体给出的 trace-id
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时并入宿主当前的 W3C Activity，也没有时生成新的（RFC-0003 规则 7）
    /// </remarks>
    public string? TraceId { get; init; }

    /// <summary>
    /// 载体给出的 parent-id
    /// </summary>
    /// <remarks>
    /// 只在给了 <see cref="TraceId"/> 时使用，缺省生成
    /// </remarks>
    public string? SpanId { get; init; }

    /// <summary>
    /// 载体给出的 trace-flags
    /// </summary>
    /// <remarks>
    /// 只在给了 <see cref="TraceId"/> 时使用，缺省按上游已采样处理
    /// </remarks>
    public ActivityTraceFlags? TraceFlags { get; init; }

    /// <summary>
    /// 绝对截止时间
    /// </summary>
    public DateTimeOffset? Deadline { get; init; }

    /// <summary>
    /// Baggage
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时为空；上下文构造时复制成只读副本
    /// </remarks>
    public IReadOnlyDictionary<string, string>? Baggage { get; init; }
}
