#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanEventBusAbstractionsModule
// Guid:a1b2c3d4-e5f6-7g8h-9i0j-k1l2m3n4o5p6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/11 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 曦寒框架事件总线抽象模块
/// </summary>
[DependsOn(
    typeof(XiHanMultiTenancyAbstractionsModule)
)]
public class XiHanEventBusAbstractionsModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }
}
