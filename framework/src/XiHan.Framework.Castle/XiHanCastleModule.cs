#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanCastleModule
// Guid:d4e5f6a7-b8c9-0123-def0-123456789abc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/05 05:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Castle.Extensions;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Castle;

/// <summary>
/// 曦寒框架 Castle 动态代理模块
/// </summary>
public class XiHanCastleModule : XiHanModule
{
    /// <summary>
    /// 服务配置后处理，在所有服务注册完成后为需要拦截的服务创建 Castle 代理
    /// </summary>
    /// <param name="context"></param>
    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddCastleDynamicProxy();
    }
}
