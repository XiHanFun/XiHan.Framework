#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHttpModule
// Guid:bd5cd31c-c791-42d9-a48c-56ead4293941
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 03:35:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Http.Extensions.DependencyInjection;

namespace XiHan.Framework.Http;

/// <summary>
/// 曦寒框架网络请求模块
/// </summary>
public class XiHanHttpModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 使用扩展方法添加服务
        services.AddXiHanHttpModule(config);
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // 使用构建器初始化应用
        StringHttpExtensions.Initialize(context.ServiceProvider);
    }
}
