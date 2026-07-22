// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// 第三方登录存储接口，业务层实现数据库持久化
/// </summary>
public interface IExternalLoginStore
{
    /// <summary>
    /// 根据提供商和提供商用户标识查找关联的内部用户ID
    /// </summary>
    /// <param name="provider">提供商名称</param>
    /// <param name="providerKey">提供商用户标识</param>
    /// <param name="tenantId">租户ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns>内部用户ID，未绑定返回 null</returns>
    Task<long?> FindUserIdAsync(string provider, string providerKey, long? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建第三方登录绑定记录
    /// </summary>
    /// <param name="userId">内部用户ID</param>
    /// <param name="info">第三方登录信息</param>
    /// <param name="tenantId">租户ID</param>
    /// <param name="cancellationToken"></param>
    Task CreateAsync(long userId, ExternalLoginInfo info, long? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除第三方登录绑定记录
    /// </summary>
    /// <param name="userId">内部用户ID</param>
    /// <param name="provider">提供商名称</param>
    /// <param name="cancellationToken"></param>
    Task RemoveAsync(long userId, string provider, CancellationToken cancellationToken = default);
}
