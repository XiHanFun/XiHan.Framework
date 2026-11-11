// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 授予的管理：批量授予、批量撤销与分页列出
/// </summary>
/// <remarks>
/// 可选接口，与只读的 <see cref="IPermissionGrantStore"/> 分开：只供判定的存储不必实现它。逐项结果与入参同序（批量优先第 4 条）；空集合不访问后端，返回空列表；取消在进入后端前检查。写入后同一实例上的判定立即看到新授予，持久化实现在调用方的事务提交后才对别的调用可见（ADR-0158）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IPermissionGrantAdministration
{
    /// <summary>
    /// 授予，按顺序逐项返回
    /// </summary>
    /// <remarks>
    /// 已存在的授予返回成功且不重复写，同一批里重复的项同样成功。容量用尽返回 <see cref="SecurityErrors.CapacityExceeded"/>；租户专属授予的租户是空白属于编程错误，抛出 <see cref="ArgumentException"/>
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> GrantAsync(IReadOnlyList<PermissionGrant> grants, CancellationToken cancellationToken);

    /// <summary>
    /// 撤销，按顺序逐项返回
    /// </summary>
    /// <remarks>
    /// 不存在返回 <see cref="SecurityErrors.GrantNotFound"/>，同一批里同一授予第二次同样不存在
    /// </remarks>
    ValueTask<IReadOnlyList<Result>> RevokeAsync(IReadOnlyList<PermissionGrant> grants, CancellationToken cancellationToken);

    /// <summary>
    /// 按主体与租户分页列出授予
    /// </summary>
    /// <remarks>
    /// 按主体种类、主体 ID、权限码、租户（全局在前）的序数顺序；先经 <see cref="PermissionGrantQuery.Validate"/> 核对分页，越界返回 <see cref="SecurityErrors.GrantQueryInvalid"/>、不访问后端
    /// </remarks>
    ValueTask<Result<PermissionGrantPage>> ListAsync(PermissionGrantQuery query, CancellationToken cancellationToken);
}
