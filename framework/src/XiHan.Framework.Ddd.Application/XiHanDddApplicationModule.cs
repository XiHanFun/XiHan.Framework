#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDddApplicationModule
// Guid:199bf01b-b287-4d07-b52a-074758a27803
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:32:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Ddd.Application.Contracts;
using XiHan.Framework.Ddd.Domain;

namespace XiHan.Framework.Ddd.Application;

/// <summary>
/// 曦寒框架领域驱动设计应用模块
/// </summary>
[DependsOn(
    typeof(XiHanDddApplicationContractsModule),
    typeof(XiHanDddDomainModule)
    )]
public class XiHanDddApplicationModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }
}
