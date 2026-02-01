#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IClientScopeServiceProviderAccessor
// Guid:876cb4c1-bf12-41da-9e4a-ee58157b6771
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/27 21:56:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 客户端作用域服务提供访问器
/// </summary>
public interface IClientScopeServiceProviderAccessor
{
    /// <summary>
    /// 服务提供器
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}
