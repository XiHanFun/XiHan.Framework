#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDomainService
// Guid:6b27cbb6-af0c-47e6-9cb2-5242c9c3dddf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 5:54:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Domain.Services.Abstracts;

/// <summary>
/// 领域服务接口
/// </summary>
public interface IDomainService : ITransientDependency
{
}
