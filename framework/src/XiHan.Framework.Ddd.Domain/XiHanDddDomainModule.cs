#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDddDomainModule
// Guid:d8e2aab0-7d8f-4a66-bfbc-fe461151dc3b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:33:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Ddd.Domain.Shared;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Ddd.Domain;

/// <summary>
/// 曦寒框架领域驱动设计领域模块
/// </summary>
[DependsOn(
    typeof(XiHanUowModule),
    typeof(XiHanDddDomainSharedModule)
    )]
public class XiHanDddDomainModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }
}
