// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 空主机环境测试
/// </summary>
/// <remarks>
/// 这个类型是"服务注册阶段还拿不到真实 IWebHostEnvironment"时的兜底载体，
/// 关键契约是：它必须真的能当 IWebHostEnvironment/IHostEnvironment 用，
/// 且六个成员默认全空——调用方必须自己判空，不能指望它给出可用的路径。
/// </remarks>
public class EmptyHostingEnvironmentTests
{
    /// <summary>
    /// 新建实例的六个成员全部为空
    /// </summary>
    [Fact]
    public void Defaults_AllMembersAreUnset()
    {
        var environment = new EmptyHostingEnvironment();

        Assert.Null(environment.EnvironmentName);
        Assert.Null(environment.ApplicationName);
        Assert.Null(environment.WebRootPath);
        Assert.Null(environment.WebRootFileProvider);
        Assert.Null(environment.ContentRootPath);
        Assert.Null(environment.ContentRootFileProvider);
    }

    /// <summary>
    /// 六个成员都可写，写入后原样读回
    /// </summary>
    [Fact]
    public void Members_AreWritableAndReadBack()
    {
        var webRootFileProvider = new NullFileProvider();
        var contentRootFileProvider = new NullFileProvider();

        var environment = new EmptyHostingEnvironment
        {
            EnvironmentName = Environments.Staging,
            ApplicationName = "XiHan.Framework.Web.Core.Tests",
            WebRootPath = "/srv/app/wwwroot",
            WebRootFileProvider = webRootFileProvider,
            ContentRootPath = "/srv/app",
            ContentRootFileProvider = contentRootFileProvider
        };

        Assert.Equal(Environments.Staging, environment.EnvironmentName);
        Assert.Equal("XiHan.Framework.Web.Core.Tests", environment.ApplicationName);
        Assert.Equal("/srv/app/wwwroot", environment.WebRootPath);
        Assert.Same(webRootFileProvider, environment.WebRootFileProvider);
        Assert.Equal("/srv/app", environment.ContentRootPath);
        Assert.Same(contentRootFileProvider, environment.ContentRootFileProvider);
    }

    /// <summary>
    /// 能同时以 Web 主机环境和通用主机环境两种契约被消费
    /// </summary>
    [Fact]
    public void Instance_SatisfiesBothHostEnvironmentContracts()
    {
        IWebHostEnvironment webHostEnvironment = new EmptyHostingEnvironment
        {
            EnvironmentName = Environments.Production,
            ApplicationName = "XiHan.Framework.Web.Core.Tests",
            ContentRootPath = "/srv/app"
        };

        IHostEnvironment hostEnvironment = webHostEnvironment;

        Assert.Equal("Production", webHostEnvironment.EnvironmentName);
        Assert.Equal("Production", hostEnvironment.EnvironmentName);
        Assert.Equal("/srv/app", hostEnvironment.ContentRootPath);
    }
}
