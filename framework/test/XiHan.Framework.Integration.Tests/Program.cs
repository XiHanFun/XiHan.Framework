#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Program
// Guid:4a8c6758-712b-480a-9df5-67ff897b7a29
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/04 14:42:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Hosting;
using Serilog;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.Hosting;
using XiHan.Framework.Integration.Tests;

try
{
    var builder = Host.CreateApplicationBuilder(args);

    await builder.Services.AddApplicationAsync<XiHanTestsIntegrationModule>();

    var host = builder.Build();

    await host.InitializeAsync();

    await host.RunAsync();

    Log.Information("应用启动");
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用关闭");
}
finally
{
    await Log.CloseAndFlushAsync();
}
