// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Security.Abstractions.Permissions;

namespace XiHan.Framework.Security.Database.Grants;

/// <summary>
/// 两个存储共用的授予行查询
/// </summary>
/// <remarks>
/// 按一组主体取有效授予的规约、按自然键集合取回已有行、按全序分页读完
/// </remarks>
internal static class GrantRows
{
    /// <summary>
    /// 一组主体对一次调用生效的授予行
    /// </summary>
    /// <remarks>
    /// 范围按 <see cref="PermissionGrant.VisibleScopes"/>：<paramref name="tenantId"/> 为 <see langword="null"/>（或空）时只有平台行，否则是每个租户的行加该租户的行（ADR-0197）。主体种类、主体 ID 与范围各取集合做 <c>IN</c> 过滤，
    /// 主体两个集合的笛卡尔积可能多取回别的主体的行，调用方按完整的主体精确匹配
    /// </remarks>
    public static Specification<PermissionGrantRecord> EffectiveForAny(IReadOnlyCollection<GrantSubject> subjects, string? tenantId)
    {
        var kinds = subjects.Select(static s => s.Kind).Distinct(StringComparer.Ordinal).ToList();
        var ids = subjects.Select(static s => s.Id).Distinct(StringComparer.Ordinal).ToList();
        // 范围换成列值：平台是空字符串，条件里不写 "|| TenantId == null"，列本身非空
        var scopes = PermissionGrant.VisibleScopes(tenantId).Select(PermissionGrantRecord.TenantColumnOf).ToList();
        return new Specification<PermissionGrantRecord>(r => kinds.Contains(r.SubjectKind) && ids.Contains(r.SubjectId) && scopes.Contains(r.TenantId));
    }

    /// <summary>
    /// 按自然键集合一次查询
    /// </summary>
    /// <remarks>
    /// 主体种类、主体 ID、权限码与租户各取集合做 <c>IN</c> 过滤，再在内存里按完整的授予精确匹配，不逐项查询。四个 <c>IN</c> 的笛卡尔积可能多取回一些行，所以分页读完后再精确匹配
    /// </remarks>
    public static async Task<Dictionary<PermissionGrant, PermissionGrantRecord>> FindManyAsync(IRepository<PermissionGrantRecord, Guid> repository, IReadOnlyList<PermissionGrant> grants, CancellationToken cancellationToken)
    {
        var kinds = grants.Select(static g => g.Subject.Kind).Distinct(StringComparer.Ordinal).ToList();
        var ids = grants.Select(static g => g.Subject.Id).Distinct(StringComparer.Ordinal).ToList();
        var permissions = grants.Select(static g => g.Permission).Distinct(StringComparer.Ordinal).ToList();
        var tenants = grants.Select(static g => PermissionGrantRecord.TenantColumnOf(g.TenantId)).Distinct(StringComparer.Ordinal).ToList();
        var query = EntityQuery.All<PermissionGrantRecord>()
            .Where(new Specification<PermissionGrantRecord>(r => kinds.Contains(r.SubjectKind) && ids.Contains(r.SubjectId) && permissions.Contains(r.Permission) && tenants.Contains(r.TenantId)))
            .OrderBy(static r => r.Id);
        var wanted = new HashSet<PermissionGrant>(grants);
        var found = new Dictionary<PermissionGrant, PermissionGrantRecord>();
        foreach (var row in await ListAllAsync(repository, query, cancellationToken).ConfigureAwait(false))
        {
            var grant = row.ToGrant();
            if (wanted.Contains(grant))
            {
                found[grant] = row;
            }
        }

        return found;
    }

    /// <summary>
    /// 按查询的过滤与排序分页读完
    /// </summary>
    /// <remarks>
    /// 每页 <see cref="EntityQuery.DefaultMaxTake"/> 条，直到不足一页。查询的排序必须是全序，分页才不重不漏
    /// </remarks>
    public static async Task<List<PermissionGrantRecord>> ListAllAsync(IRepository<PermissionGrantRecord, Guid> repository, EntityQuery<PermissionGrantRecord> query, CancellationToken cancellationToken)
    {
        var rows = new List<PermissionGrantRecord>();
        var skip = 0;
        while (true)
        {
            var page = await repository.ListAsync(query.Page(skip, EntityQuery.DefaultMaxTake), cancellationToken).ConfigureAwait(false);
            rows.AddRange(page);
            if (page.Count < EntityQuery.DefaultMaxTake)
            {
                return rows;
            }

            skip += page.Count;
        }
    }
}
