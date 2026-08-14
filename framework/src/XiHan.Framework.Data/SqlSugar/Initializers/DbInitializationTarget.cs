// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 初始化目标库
/// </summary>
[Flags]
public enum DbInitializationTarget
{
    /// <summary>
    /// 平台库：连接配置里声明的静态连接
    /// </summary>
    Platform = 1 << 0,

    /// <summary>
    /// 租户独立库：ConfigId 以 <c>XiHanSqlSugarCoreOptions.TenantConfigIdPrefix</c> 开头的运行时连接
    /// </summary>
    Tenant = 1 << 1,

    /// <summary>
    /// 平台库与租户独立库
    /// </summary>
    All = Platform | Tenant
}
