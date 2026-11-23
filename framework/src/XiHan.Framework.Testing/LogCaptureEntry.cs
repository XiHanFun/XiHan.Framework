// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Testing;

/// <summary>
/// <see cref="LogCapture"/> 收到的一条日志
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record LogCaptureEntry
{
    /// <summary>
    /// 级别
    /// </summary>
    public required LogLevel Level { get; init; }

    /// <summary>
    /// 类别，即创建日志器时给的名字
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// 格式化后的消息
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 随日志带的异常
    /// </summary>
    /// <remarks>
    /// 没有带异常时为 <see langword="null"/>
    /// </remarks>
    public Exception? Exception { get; init; }
}
