// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Serialization;

/// <summary>
/// 值的序列化。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface ICacheSerializer
{
    /// <summary>
    /// 序列化。
    /// </summary>
    ReadOnlyMemory<byte> Serialize<T>(T value);

    /// <summary>
    /// 反序列化。
    /// </summary>
    /// <remarks>
    /// 数据不能还原为 <typeparamref name="T"/> 时抛出 <see cref="InvalidOperationException"/>。
    /// </remarks>
    T Deserialize<T>(ReadOnlyMemory<byte> data);
}
