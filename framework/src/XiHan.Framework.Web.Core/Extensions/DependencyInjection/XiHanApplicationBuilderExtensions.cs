#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanApplicationBuilderExtensions
// Guid:7c005f32-f220-4e30-9088-22ffd55fb6ca
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/10 6:02:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Utils.System;
using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.Web.Core.Extensions.DependencyInjection;

/// <summary>
/// 曦寒应用程序构建器扩展
/// </summary>
public static class XiHanApplicationBuilderExtensions
{
    /// <summary>
    /// 初始化应用程序
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static async Task InitializeApplicationAsync(this IApplicationBuilder app)
    {
        _ = Guard.NotNull(app, nameof(app));

        app.ApplicationServices.GetRequiredService<ObjectAccessor<IApplicationBuilder>>().Value = app;
        var application = app.ApplicationServices.GetRequiredService<IXiHanApplicationWithExternalServiceProvider>();
        var applicationLifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();

        _ = applicationLifetime.ApplicationStopping.Register(() =>
        {
            AsyncHelper.RunSync(() => application.ShutdownAsync());
        });
        _ = applicationLifetime.ApplicationStopped.Register(application.Dispose);

        await application.InitializeAsync(app.ApplicationServices);
    }

    /// <summary>
    /// 初始化应用程序
    /// </summary>
    /// <param name="app"></param>
    public static void InitializeApplication(this IApplicationBuilder app)
    {
        _ = Guard.NotNull(app, nameof(app));

        app.ApplicationServices.GetRequiredService<ObjectAccessor<IApplicationBuilder>>().Value = app;
        var application = app.ApplicationServices.GetRequiredService<IXiHanApplicationWithExternalServiceProvider>();
        var applicationLifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();

        _ = applicationLifetime.ApplicationStopping.Register(application.Shutdown);
        _ = applicationLifetime.ApplicationStopped.Register(application.Dispose);

        application.Initialize(app.ApplicationServices);
    }
}
