// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Security.Extensions;

namespace XiHan.Framework.Security.Clients;

/// <summary>
/// 当前客户端
/// </summary>
public class CurrentClient : ICurrentClient, ITransientDependency
{
    /// <summary>
    /// 当前主体访问器
    /// </summary>
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="principalAccessor"></param>
    public CurrentClient(ICurrentPrincipalAccessor principalAccessor)
    {
        _principalAccessor = principalAccessor;
    }

    /// <summary>
    /// 客户端标识
    /// </summary>
    public virtual string? Id => _principalAccessor.Principal.FindClientId();

    /// <summary>
    /// 是否已认证
    /// </summary>
    public virtual bool IsAuthenticated => Id is not null;
}
