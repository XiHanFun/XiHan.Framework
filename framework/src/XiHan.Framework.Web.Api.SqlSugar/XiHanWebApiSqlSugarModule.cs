// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Data;

namespace XiHan.Framework.Web.Api.SqlSugar;

/// <summary>
/// 曦寒框架 Web API 幂等 SqlSugar 持久化模块
/// </summary>
/// <remarks>
/// 在应用模块中 DependsOn 本模块即启用，会把默认的进程内幂等存储换成 SqlSugar 存储。
/// </remarks>
[DependsOn(
    typeof(XiHanWebApiModule),
    typeof(XiHanDataModule)
)]
public class XiHanWebApiSqlSugarModule : XiHanModule
{
}
