// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Database.Tenants;

/// <summary>
/// 批量写入或删除里一项的结果。
/// </summary>
/// <param name="TenantId">租户 ID。</param>
/// <param name="Outcome">仓储的结果，或不存在的失败。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record TenantRecordResult(string TenantId, Result Outcome);
