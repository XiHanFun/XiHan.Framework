// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;
using XiHan.Framework.Upgrade.SqlSugar.Extensions.DependencyInjection;

namespace XiHan.Framework.Upgrade.SqlSugar;

/// <summary>
/// 曦寒框架升级模块 SqlSugar 持久化模块
/// </summary>
/// <remarks>
/// 在应用模块上 <c>[DependsOn(typeof(XiHanUpgradeSqlSugarModule))]</c> 即启用。
/// 本模块以 SqlSugar 存储替换 <see cref="XiHanUpgradeModule"/> 注册的内存实现。
/// </remarks>
[DependsOn(
    typeof(XiHanUpgradeModule),
    typeof(XiHanDataModule)
)]
public class XiHanUpgradeSqlSugarModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddXiHanUpgradeSqlSugar();
    }
}
