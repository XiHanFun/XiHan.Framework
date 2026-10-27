// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 持久化子系统的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class PersistenceCapabilities
{
    /// <summary>
    /// 关系型持久化：仓储、工作单元、迁移。
    /// </summary>
    public const string Relational = "persistence.relational";

    /// <summary>
    /// 文档型持久化。
    /// </summary>
    public const string Document = "persistence.document";

    /// <summary>
    /// 当前接口版本。
    /// </summary>
    public const int Version = 1;
}
