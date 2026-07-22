// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Serilog;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;
using XiHan.Framework.Web.Tests;

try
{
    var builder = WebApplication.CreateBuilder(args);

    await builder.AddApplicationAsync<XiHanTestsWebModule>();

    var app = builder.Build();

    await app.InitializeApplicationAsync();

    await app.RunAsync();

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
