// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Identifiers;

/// <summary>
/// 基于时间有序的 UUID v7 标识生成器。
/// </summary>
/// <param name="timeProvider">时间源。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class GuidV7IdGenerator(TimeProvider timeProvider) : IIdGenerator<Guid>
{
    /// <summary>
    /// 提供时间戳部分的时间源；注入可拨动的时钟即可得到确定的标识顺序。
    /// </summary>
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public Guid NewId() => Guid.CreateVersion7(_timeProvider.GetUtcNow());
}
