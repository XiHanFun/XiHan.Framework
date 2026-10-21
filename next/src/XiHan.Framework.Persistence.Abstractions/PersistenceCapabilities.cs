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
    /// <summary>关系型持久化：仓储、工作单元、迁移。</summary>
    public const string Relational = "persistence.relational";

    /// <summary>文档型持久化。</summary>
    public const string Document = "persistence.document";

    /// <summary>当前接口版本。</summary>
    public const int Version = 1;
}

/// <summary>
/// 持久化错误码；登记在 <c>spec/conventions/error-codes.md</c>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class PersistenceErrors
{
    /// <summary>实体不存在。</summary>
    public const string NotFound = "PERSISTENCE_NOTFOUND_001";

    /// <summary>乐观并发冲突。</summary>
    public const string Concurrency = "PERSISTENCE_CONFLICT_001";

    /// <summary>标识重复。</summary>
    public const string Duplicate = "PERSISTENCE_CONFLICT_002";

    /// <summary>多租户实体缺少当前租户。</summary>
    public const string TenantRequired = "PERSISTENCE_VALIDATION_001";

    /// <summary>工作单元状态不允许该操作。</summary>
    public const string InvalidUnitOfWorkState = "PERSISTENCE_VALIDATION_002";

    /// <summary>没有活动的工作单元。</summary>
    public const string NoActiveUnitOfWork = "PERSISTENCE_UNAVAILABLE_001";

    /// <summary>存储容量已满。</summary>
    public const string CapacityExceeded = "PERSISTENCE_UNAVAILABLE_002";

    /// <summary>存储不可用。</summary>
    public const string StoreUnavailable = "PERSISTENCE_UNAVAILABLE_003";
}
