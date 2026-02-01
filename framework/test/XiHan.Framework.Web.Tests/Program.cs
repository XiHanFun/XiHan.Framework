#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:Program
// Guid:5fc893c0-6c6d-4706-bd52-da4c7ee6cfbf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/04 14:42:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
