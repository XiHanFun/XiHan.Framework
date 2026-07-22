// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
