// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using System.Globalization;
using XiHan.Framework.Localization.Middlewares;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.Localization.Tests.TestSupport;

namespace XiHan.Framework.Localization.Tests.Middlewares;

/// <summary>
/// 请求文化中间件测试
/// </summary>
/// <remarks>
/// 两条契约必须成立：
/// 1）解析优先级为「自定义请求头 &gt; Accept-Language（按 q 权重）&gt; 默认文化」，且只接受受支持文化；
/// 2）请求结束后必须还原线程文化——线程池会复用线程，残留的请求级文化会污染后续请求，
///    所以异常路径同样要还原。
/// </remarks>
public class XiHanRequestCultureMiddlewareTests
{
    /// <summary>
    /// HttpContext.Items 键是对外契约，不允许漂移
    /// </summary>
    [Fact]
    public void CultureItemKey_IsStableContractValue()
    {
        Assert.Equal("__XiHanCulture", XiHanRequestCultureMiddleware.CultureItemKey);
    }

    /// <summary>
    /// 自定义请求头中的受支持文化被直接采用，并同时设置文化与 UI 文化
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenHeaderCultureSupported_UsesHeaderCulture()
    {
        var result = await InvokeAsync(
            CreateOptions(),
            context => context.Request.Headers["X-Language"] = "en-US");

        Assert.Equal("en-US", result.CultureName);
        Assert.Equal("en-US", result.UiCultureName);
        Assert.Equal("en-US", result.ItemValue);
    }

    /// <summary>
    /// 请求头文化不在受支持列表时改用 Accept-Language
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenHeaderCultureUnsupported_FallsBackToAcceptLanguage()
    {
        var result = await InvokeAsync(
            CreateOptions(),
            context =>
            {
                context.Request.Headers["X-Language"] = "fr-FR";
                context.Request.Headers["Accept-Language"] = "en-US";
            });

        Assert.Equal("en-US", result.CultureName);
    }

    /// <summary>
    /// Accept-Language 按 q 权重从高到低挑选首个受支持文化
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AcceptLanguage_PicksHighestQualitySupportedCulture()
    {
        var options = CreateOptions();
        options.DefaultCulture = "ja-JP";

        var result = await InvokeAsync(
            options,
            context => context.Request.Headers["Accept-Language"] = "en-US;q=0.3, zh-CN;q=0.9");

        Assert.Equal("zh-CN", result.CultureName);
    }

    /// <summary>
    /// Accept-Language 中的通配符条目被忽略
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AcceptLanguage_IgnoresWildcardEntry()
    {
        var options = CreateOptions();
        options.DefaultCulture = "ja-JP";

        var result = await InvokeAsync(
            options,
            context => context.Request.Headers["Accept-Language"] = "*, en-US");

        Assert.Equal("en-US", result.CultureName);
    }

    /// <summary>
    /// Accept-Language 中不受支持的条目被跳过，继续看后续条目
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AcceptLanguage_SkipsUnsupportedEntries()
    {
        var options = CreateOptions();
        options.DefaultCulture = "ja-JP";

        var result = await InvokeAsync(
            options,
            context => context.Request.Headers["Accept-Language"] = "fr-FR;q=0.9, en-US;q=0.5");

        Assert.Equal("en-US", result.CultureName);
    }

    /// <summary>
    /// 请求头与 Accept-Language 都无法解析时使用默认文化
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenNothingResolved_UsesDefaultCulture()
    {
        var options = CreateOptions();
        options.DefaultCulture = "en-US";

        var result = await InvokeAsync(options, _ => { });

        Assert.Equal("en-US", result.CultureName);
        Assert.Equal("en-US", result.ItemValue);
    }

    /// <summary>
    /// 受支持文化列表为空时不做限制，任意合法文化都被接受
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenSupportedCulturesEmpty_AcceptsAnyValidCulture()
    {
        var options = CreateOptions();
        options.SupportedCultures = new List<string>();

        var result = await InvokeAsync(
            options,
            context => context.Request.Headers["X-Language"] = "fr-FR");

        Assert.Equal("fr-FR", result.CultureName);
    }

    /// <summary>
    /// 受支持文化列表为空但请求文化非法时仍回退默认文化
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenSupportedCulturesEmptyAndCultureInvalid_UsesDefaultCulture()
    {
        var options = CreateOptions();
        options.SupportedCultures = new List<string>();
        options.DefaultCulture = "en-US";

        var result = await InvokeAsync(
            options,
            context => context.Request.Headers["X-Language"] = "!!not-a-culture!!");

        Assert.Equal("en-US", result.CultureName);
    }

    /// <summary>
    /// 受支持文化匹配不区分大小写，并归一化为列表中登记的写法
    /// </summary>
    [Fact]
    public async Task InvokeAsync_MatchesSupportedCultureIgnoringCase()
    {
        var result = await InvokeAsync(
            CreateOptions(),
            context => context.Request.Headers["X-Language"] = "EN-us");

        Assert.Equal("en-US", result.CultureName);
    }

    /// <summary>
    /// 请求头名称两侧空白被裁剪后仍能匹配
    /// </summary>
    [Fact]
    public async Task InvokeAsync_TrimsHeaderValueBeforeMatching()
    {
        var result = await InvokeAsync(
            CreateOptions(),
            context => context.Request.Headers["X-Language"] = "  en-US  ");

        Assert.Equal("en-US", result.CultureName);
    }

    /// <summary>
    /// 使用配置中指定的请求头名称
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenCultureHeaderNameConfigured_ReadsThatHeader()
    {
        var options = CreateOptions();
        options.CultureHeaderName = "X-Culture";

        var result = await InvokeAsync(
            options,
            context =>
            {
                context.Request.Headers["X-Culture"] = "en-US";
                context.Request.Headers["X-Language"] = "zh-CN";
            });

        Assert.Equal("en-US", result.CultureName);
    }

    /// <summary>
    /// 请求头名称配置为空白时退回 X-Language
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenCultureHeaderNameBlank_FallsBackToXLanguageHeader()
    {
        var options = CreateOptions();
        options.CultureHeaderName = "   ";

        var result = await InvokeAsync(
            options,
            context => context.Request.Headers["X-Language"] = "en-US");

        Assert.Equal("en-US", result.CultureName);
    }

    /// <summary>
    /// 默认文化本身非法时退化为固定区域文化
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDefaultCultureInvalid_UsesInvariantCulture()
    {
        var options = CreateOptions();
        options.DefaultCulture = "!!not-a-culture!!";

        var result = await InvokeAsync(options, _ => { });

        Assert.Equal(CultureInfo.InvariantCulture.Name, result.CultureName);
        Assert.Equal(CultureInfo.InvariantCulture.Name, result.ItemValue);
    }

    /// <summary>
    /// 管道正常结束后还原线程文化
    /// </summary>
    [Fact]
    public async Task InvokeAsync_RestoresAmbientCultureAfterPipelineCompletes()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var ambient = CultureInfo.GetCultureInfo("ja-JP");
            CultureInfo.CurrentCulture = ambient;
            CultureInfo.CurrentUICulture = ambient;

            var result = await InvokeAsync(
                CreateOptions(),
                context => context.Request.Headers["X-Language"] = "en-US");

            Assert.Equal("en-US", result.CultureName);
            Assert.Equal("ja-JP", CultureInfo.CurrentCulture.Name);
            Assert.Equal("ja-JP", CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    /// <summary>
    /// 管道抛异常时同样还原线程文化
    /// </summary>
    [Fact]
    public async Task InvokeAsync_RestoresAmbientCultureWhenPipelineThrows()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var ambient = CultureInfo.GetCultureInfo("ja-JP");
            CultureInfo.CurrentCulture = ambient;
            CultureInfo.CurrentUICulture = ambient;

            var monitor = new TestOptionsMonitor<XiHanLocalizationOptions>(CreateOptions());
            var middleware = new XiHanRequestCultureMiddleware(
                _ => throw new InvalidOperationException("管道异常"),
                monitor);

            var context = new DefaultHttpContext();
            context.Request.Headers["X-Language"] = "en-US";

            await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

            Assert.Equal("ja-JP", CultureInfo.CurrentCulture.Name);
            Assert.Equal("ja-JP", CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    /// <summary>
    /// 解析结果始终写入 HttpContext.Items，即使后续管道抛异常
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WritesResolvedCultureToItemsBeforeInvokingPipeline()
    {
        var monitor = new TestOptionsMonitor<XiHanLocalizationOptions>(CreateOptions());
        var middleware = new XiHanRequestCultureMiddleware(
            _ => throw new InvalidOperationException("管道异常"),
            monitor);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Language"] = "en-US";

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Equal("en-US", context.Items[XiHanRequestCultureMiddleware.CultureItemKey] as string);
    }

    private static XiHanLocalizationOptions CreateOptions()
    {
        return new XiHanLocalizationOptions
        {
            DefaultCulture = "zh-CN",
            SupportedCultures = new List<string> { "zh-CN", "en-US" }
        };
    }

    private static async Task<CultureCaptureResult> InvokeAsync(
        XiHanLocalizationOptions options,
        Action<HttpContext> configureRequest)
    {
        var monitor = new TestOptionsMonitor<XiHanLocalizationOptions>(options);
        var capturedCulture = string.Empty;
        var capturedUiCulture = string.Empty;

        var middleware = new XiHanRequestCultureMiddleware(
            _ =>
            {
                capturedCulture = CultureInfo.CurrentCulture.Name;
                capturedUiCulture = CultureInfo.CurrentUICulture.Name;
                return Task.CompletedTask;
            },
            monitor);

        var context = new DefaultHttpContext();
        configureRequest(context);

        await middleware.InvokeAsync(context);

        var itemValue = context.Items[XiHanRequestCultureMiddleware.CultureItemKey] as string ?? string.Empty;
        return new CultureCaptureResult(itemValue, capturedCulture, capturedUiCulture);
    }

    /// <summary>
    /// 中间件执行结果快照
    /// </summary>
    /// <param name="ItemValue">写入 HttpContext.Items 的文化名</param>
    /// <param name="CultureName">管道内观察到的当前文化名</param>
    /// <param name="UiCultureName">管道内观察到的当前 UI 文化名</param>
    private sealed record CultureCaptureResult(string ItemValue, string CultureName, string UiCultureName);
}
