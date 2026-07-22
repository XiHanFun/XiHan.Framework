// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Threading;
using XiHan.Framework.Utils.Diagnostics;

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
        Guard.NotNull(app, nameof(app));

        app.ApplicationServices.GetRequiredService<ObjectAccessor<IApplicationBuilder>>().Value = app;
        var application = app.ApplicationServices.GetRequiredService<IXiHanApplicationWithExternalServiceProvider>();
        var applicationLifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();

        applicationLifetime.ApplicationStopping.Register(() =>
        {
            AsyncHelper.RunSync(() => application.ShutdownAsync());
        });
        applicationLifetime.ApplicationStopped.Register(application.Dispose);

        await application.InitializeAsync(app.ApplicationServices);
    }

    /// <summary>
    /// 初始化应用程序
    /// </summary>
    /// <param name="app"></param>
    public static void InitializeApplication(this IApplicationBuilder app)
    {
        Guard.NotNull(app, nameof(app));

        app.ApplicationServices.GetRequiredService<ObjectAccessor<IApplicationBuilder>>().Value = app;
        var application = app.ApplicationServices.GetRequiredService<IXiHanApplicationWithExternalServiceProvider>();
        var applicationLifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();

        applicationLifetime.ApplicationStopping.Register(application.Shutdown);
        applicationLifetime.ApplicationStopped.Register(application.Dispose);

        application.Initialize(app.ApplicationServices);
    }
}
