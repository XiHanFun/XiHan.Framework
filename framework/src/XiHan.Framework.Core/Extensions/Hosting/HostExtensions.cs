// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Threading;

namespace XiHan.Framework.Core.Extensions.Hosting;

/// <summary>
/// 主机扩展方法
/// </summary>
public static class HostExtensions
{
    /// <summary>
    /// 异步初始化应用程序
    /// </summary>
    /// <param name="host"></param>
    /// <returns></returns>
    public static async Task InitializeAsync(this IHost host)
    {
        var application = host.Services.GetRequiredService<IXiHanApplicationWithExternalServiceProvider>();
        var applicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        applicationLifetime.ApplicationStopping.Register(() => AsyncHelper.RunSync(() => application.ShutdownAsync()));
        applicationLifetime.ApplicationStopped.Register(application.Dispose);

        await application.InitializeAsync(host.Services);
    }
}
