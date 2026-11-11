// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 权限授予存储，Provider 实现它
/// </summary>
/// <remarks>
/// 只有批量读取：判定一次权限要读用户与它的全部角色，一次调用读完（批量优先）；单个主体的写法是扩展方法 <see cref="PermissionGrantStoreExtensions.GetGrantsAsync"/>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IPermissionGrantStore
{
    /// <summary>
    /// 取一组主体在租户下的有效授予
    /// </summary>
    /// <remarks>
    /// 结果与 <paramref name="subjects"/> 等长、顺序一致，每项是该主体的全局授予加该租户的授予；<paramref name="tenantId"/> 为 <see langword="null"/> 时只有全局授予；没有授予的主体是空集合；重复的主体各得一份相同的结果；空集合不访问后端，返回空列表。
    /// 存储不可达（连接、超时、事务失败）整批返回 <see cref="SecurityErrors.StoreUnavailable"/>，不能以空集合代替：空集合会被判定为不持有（403），故障必须以 503 结束
    /// </remarks>
    ValueTask<Result<IReadOnlyList<IReadOnlyCollection<string>>>> GetManyAsync(IReadOnlyList<GrantSubject> subjects, string? tenantId, CancellationToken cancellationToken);
}
