// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 强类型标识的公共契约。
/// </summary>
/// <remarks>
/// 协议适配器与生成器据此把标识编码为底层值。
/// </remarks>
/// <typeparam name="TValue">底层值类型（string、Guid、long 等）。</typeparam>
[ApiLevel(Stability.Stable, "5.0")]
public interface ITypedId<out TValue>
    where TValue : notnull
{
    /// <summary>
    /// 底层值。
    /// </summary>
    TValue Value { get; }
}
