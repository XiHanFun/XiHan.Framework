#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDevToolsModule
// Guid:9501f529-5342-4214-99ec-f8660ceb1b59
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/08/07 00:57:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.DevTools.CommandLine;

namespace XiHan.Framework.DevTools;

/// <summary>
/// 曦寒框架开发工具模块
/// </summary>
/// <remarks>
/// 从空壳升级为一等模块：接入模块系统，并把命令行框架 <see cref="CommandApp"/> 注册进 DI。
/// 从容器解析出的 <see cref="CommandApp"/> 会经服务提供者构造命令实例（支持构造函数注入），
/// 不再是脱离 DI 的反射直建。
/// </remarks>
public class XiHanDevToolsModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 注册 DI 感知的命令行应用；消费方解析后 AddCommand/DiscoverCommands 再 RunAsync，
        // 命令实例即经 ActivatorUtilities 构造
        context.Services.AddTransient(sp => new CommandApp(sp));
    }
}
