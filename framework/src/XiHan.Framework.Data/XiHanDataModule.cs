#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDataModule
// Guid:a991cdf9-5a89-45db-b781-c9dfebb4d538
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/3 2:46:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data.SqlSugar.Extensions;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Domain;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Data;

/// <summary>
/// 曦寒数据访问模块
/// 提供基于SqlSugar的数据访问实现，集成领域驱动设计和工作单元模式
/// </summary>
[DependsOn(
    typeof(XiHanDomainModule),
    typeof(XiHanUowModule)
    )]
public class XiHanDataModule : XiHanModule
{
    /// <summary>
    /// 配置服务
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 配置SqlSugar选项
        Configure<XiHanSqlSugarCoreOptions>(services.GetConfiguration().GetSection("XiHanSqlSugarCore"));

        // 添加SqlSugar数据访问服务
        services.AddXiHanDataSqlSugar();
    }
}
