// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Threading.Extensions.DependencyInjection;

namespace XiHan.Framework.Threading;

/// <summary>
/// 曦寒框架线程模块
/// </summary>
public class XiHanThreadingModule : XiHanModule
{
    /// <summary>
    /// 配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddXiHanThreading();
    }
}
