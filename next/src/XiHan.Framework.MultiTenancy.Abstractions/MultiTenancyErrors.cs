// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 多租户错误码。
/// </summary>
/// <remarks>
/// 登记在 <c>spec/conventions/error-codes.md</c>。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class MultiTenancyErrors
{
    /// <summary>
    /// 租户 ID 不合法。
    /// </summary>
    public const string InvalidTenantId = "MULTITENANCY_VALIDATION_001";

    /// <summary>
    /// 调用必须带租户但没有解析到。
    /// </summary>
    public const string MissingTenant = "MULTITENANCY_VALIDATION_002";

    /// <summary>
    /// 租户不存在。
    /// </summary>
    public const string NotFound = "MULTITENANCY_NOTFOUND_001";

    /// <summary>
    /// 租户已禁用。
    /// </summary>
    public const string Disabled = "MULTITENANCY_DENIED_001";

    /// <summary>
    /// 租户存储容量已满。
    /// </summary>
    public const string CapacityExceeded = "MULTITENANCY_UNAVAILABLE_001";

    /// <summary>
    /// 租户分片无法解析（租户存储不可达）。
    /// </summary>
    public const string ShardUnavailable = "MULTITENANCY_UNAVAILABLE_002";
}
