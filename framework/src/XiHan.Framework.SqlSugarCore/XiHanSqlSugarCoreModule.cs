#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSqlSugarCoreModule
// Guid:e44d96b1-65a3-4037-a01e-33390ed11e59
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025-05-06 上午 11:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Ddd.Domain;
using XiHan.Framework.SqlSugarCore.Options;

namespace XiHan.Framework.SqlSugarCore;

/// <summary>
/// 曦寒框架 SqlSugarCore 模块
/// </summary>
[DependsOn(
    typeof(XiHanDddDomainModule)
)]
public class XiHanSqlSugarCoreModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 注册SqlSugar选项
        context.Services.AddOptions<XiHanSqlSugarCoreOptions>();

        // 注册SqlSugar数据库上下文
        context.Services.AddTransient<ISqlSugarDbContext, SqlSugarDbContext>();
    }
}
