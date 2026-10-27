// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 多租户子系统的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，Provider 装配时注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class MultiTenancyMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "multitenancy";

    /// <summary>
    /// 调用必须带租户但没有解析到。
    /// </summary>
    public const string TenantRequired = "multitenancy.tenant.required";

    /// <summary>
    /// 租户 ID 不合法。参数：<c>tenantId</c>。
    /// </summary>
    public const string InvalidTenantId = "multitenancy.tenant.invalid_id";

    /// <summary>
    /// 租户不存在。参数：<c>tenantId</c>。
    /// </summary>
    public const string TenantNotFound = "multitenancy.tenant.not_found";

    /// <summary>
    /// 租户已禁用。参数：<c>tenantId</c>。
    /// </summary>
    public const string TenantDisabled = "multitenancy.tenant.disabled";

    /// <summary>
    /// 租户存储容量已满。参数：<c>capacity</c>。
    /// </summary>
    public const string CapacityExceeded = "multitenancy.store.capacity_exceeded";

    /// <summary>
    /// 租户分片无法解析。参数：<c>tenantId</c>、<c>reason</c>。
    /// </summary>
    public const string ShardUnavailable = "multitenancy.shard.unavailable";

    /// <summary>
    /// 延迟装载的目录；首次访问时读嵌入的语言文件，之后复用同一实例。
    /// </summary>
    private static readonly Lazy<MessageCatalog> _catalog = new(static () => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(MultiTenancyMessages)));

    /// <summary>
    /// 目录实例；首次访问时从嵌入资源装载。
    /// </summary>
    public static MessageCatalog Catalog => _catalog.Value;
}
