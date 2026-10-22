// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 多租户子系统的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class MultiTenancyCapabilities
{
    /// <summary>租户上下文：解析、校验与存储。</summary>
    public const string Context = "multitenancy.context";

    /// <summary>租户分片（预留）。</summary>
    public const string Sharding = "multitenancy.sharding";

    /// <summary>当前接口版本。</summary>
    public const int Version = 1;
}
