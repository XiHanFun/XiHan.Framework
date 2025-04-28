#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CurrentClient
// Guid:843c5dc5-9baf-4b88-a3bb-e39bcf070164
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/25 5:04:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
