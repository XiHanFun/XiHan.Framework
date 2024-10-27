#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HostExtensions
// Guid:4f030580-c6f5-4bae-8bb2-becab00adafc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 2:47:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Utils.System.Threading;

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
        applicationLifetime.ApplicationStopped.Register(() => application.Dispose());

        await application.InitializeAsync(host.Services);
    }
}