// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Testing;

/// <summary>
/// <see cref="SpanCapture"/> 的采样器被询问的一次创建
/// </summary>
/// <remarks>
/// 采样器只看得到创建时给的名字、种类、链路与标签，之后才设的标签不在这里：测组件把采样要用的标签在创建时就交给了源
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record SpanSamplingRequest
{
    /// <summary>
    /// 源的名字
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// 要创建的 Span 的名字
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 要创建的 Span 的种类
    /// </summary>
    public required ActivityKind Kind { get; init; }

    /// <summary>
    /// 所在链路的 trace-id，32 个小写十六进制字符
    /// </summary>
    /// <remarks>
    /// 有父级时是父级的，链路的根是为它新生成的
    /// </remarks>
    public required string TraceId { get; init; }

    /// <summary>
    /// 创建时给的标签，按给出的顺序
    /// </summary>
    public required IReadOnlyList<KeyValuePair<string, object?>> Tags { get; init; }
}
