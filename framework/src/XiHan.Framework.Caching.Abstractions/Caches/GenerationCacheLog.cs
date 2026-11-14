// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// <see cref="GenerationCache{T}"/> 的日志
/// </summary>
/// <remarks>
/// 日志方法放在非泛型类里：源生成器不支持泛型类型上的日志方法
/// </remarks>
internal static partial class GenerationCacheLog
{
    /// <summary>
    /// 缓存后端故障，读取改为直接查源
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The cache backing region '{Region}' is unavailable; reads go to the source without caching until it recovers.")]
    public static partial void StoreUnavailable(ILogger logger, string region, Exception exception);
}
