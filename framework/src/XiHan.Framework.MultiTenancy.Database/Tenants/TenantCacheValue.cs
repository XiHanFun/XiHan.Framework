// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions.Tenants;

namespace XiHan.Framework.MultiTenancy.Database.Tenants;

/// <summary>
/// 一次按 ID 查找的结果，作为缓存值
/// </summary>
/// <remarks>
/// 不存在的租户也缓存（<see cref="Tenant"/> 为 <see langword="null"/>），避免反复查不存在的 ID
/// </remarks>
/// <param name="Tenant">租户；不存在为 <see langword="null"/></param>
internal sealed record TenantCacheValue(TenantInfo? Tenant);
