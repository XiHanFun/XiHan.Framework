// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Identifiers;

/// <summary>
/// 雪花标识的机器位来源：固定配置（<see cref="FixedWorkerIdSource"/>）或分布式锁租约（<see cref="LeasedWorkerIdSource"/>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IWorkerIdSource
{
    /// <summary>
    /// 当前可用的机器位；无值表示此刻没有机器位（租约尚未取得或已丢失），生成器据此拒绝生成而不是复用旧值。
    /// </summary>
    Maybe<int> Current { get; }
}
