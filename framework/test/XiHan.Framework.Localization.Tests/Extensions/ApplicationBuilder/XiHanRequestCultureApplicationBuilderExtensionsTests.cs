// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.Extensions.ApplicationBuilder;
using XiHan.Framework.Localization.Middlewares;
using XiHan.Framework.Localization.Options;

namespace XiHan.Framework.Localization.Tests.Extensions.ApplicationBuilder;

/// <summary>
/// 请求文化中间件应用扩展测试
/// </summary>
/// <remarks>
/// 中间件是约定式（构造函数注入 IOptionsMonitor），只有真正走一遍 UseMiddleware 组装 + 执行管道，
/// 才能证明它能被容器正确激活；单独 new 中间件测不出装配问题。
/// </remarks>
public class XiHanRequestCultureApplicationBuilderExtensionsTests
{
    /// <summary>
    /// 应用构建器为空时抛参数空异常
    /// </summary>
    [Fact]
    public void UseXiHanRequestCulture_WhenAppNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = XiHanRequestCultureApplicationBuilderExtensions.UseXiHanRequestCulture(null!);
        });
    }

    /// <summary>
    /// 扩展方法返回同一个应用构建器，支持链式调用
    /// </summary>
    [Fact]
    public void UseXiHanRequestCulture_ReturnsSameApplicationBuilder()
    {
        using var provider = BuildProvider();
        var app = new Microsoft.AspNetCore.Builder.ApplicationBuilder(provider);

        var result = app.UseXiHanRequestCulture();

        Assert.Same(app, result);
    }

    /// <summary>
    /// 注册后中间件真正参与管道执行，并把解析结果传递给后续中间件
    /// </summary>
    [Fact]
    public async Task UseXiHanRequestCulture_RegistersMiddlewareIntoPipeline()
    {
        using var provider = BuildProvider();
        var app = new Microsoft.AspNetCore.Builder.ApplicationBuilder(provider);

        string? observedCulture = null;
        app.UseXiHanRequestCulture();
        app.Run(context =>
        {
            observedCulture = context.Items[XiHanRequestCultureMiddleware.CultureItemKey] as string;
            return Task.CompletedTask;
        });

        var pipeline = app.Build();
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.Request.Headers["X-Language"] = "en-US";

        await pipeline(httpContext);

        Assert.Equal("en-US", observedCulture);
    }

    /// <summary>
    /// 未携带任何文化信息时管道内拿到的是配置的默认文化
    /// </summary>
    [Fact]
    public async Task UseXiHanRequestCulture_WhenRequestHasNoCultureHint_UsesConfiguredDefaultCulture()
    {
        using var provider = BuildProvider();
        var app = new Microsoft.AspNetCore.Builder.ApplicationBuilder(provider);

        string? observedCulture = null;
        app.UseXiHanRequestCulture();
        app.Run(context =>
        {
            observedCulture = context.Items[XiHanRequestCultureMiddleware.CultureItemKey] as string;
            return Task.CompletedTask;
        });

        var pipeline = app.Build();
        await pipeline(new DefaultHttpContext { RequestServices = provider });

        Assert.Equal("zh-CN", observedCulture);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions<XiHanLocalizationOptions>().Configure(options =>
        {
            options.DefaultCulture = "zh-CN";
            options.SupportedCultures = ["zh-CN", "en-US"];
        });

        return services.BuildServiceProvider();
    }
}
