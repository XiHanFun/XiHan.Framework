// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.ObjectStorage.Options;
using XiHan.Framework.ObjectStorage.Services;
using XiHan.Framework.ObjectStorage.Tests.Fakes;

namespace XiHan.Framework.ObjectStorage.Tests.Services;

/// <summary>
/// 默认文件存储路由器测试
/// </summary>
/// <remarks>
/// 路由优先级是这个类的全部价值：显式提供程序名 &gt; 路由键映射 &gt; 默认提供程序。
/// 另有两条容易被误解的边界：
/// 一是严格匹配只在「传了路由键但没命中映射」时才生效，不传路由键永远回落默认值；
/// 二是映射值为空白等同于未命中。这两条都单独立了用例。
/// </remarks>
public class DefaultFileStorageRouterTests
{
    private readonly FakeFileStorageProviderManager _manager = new();

    /// <summary>
    /// 显式提供程序名优先级最高，且两端空白被裁掉
    /// </summary>
    [Theory]
    [InlineData("MinIO")]
    [InlineData("  MinIO  ")]
    public void ResolveProviderName_WithExplicitProviderName_WinsOverEverythingElse(string providerName)
    {
        var options = new XiHanObjectStorageOptions { DefaultProvider = "Local" };
        options.RouteProviderMappings["avatar"] = "AliyunOSS";
        var router = CreateRouter(options);

        Assert.Equal("MinIO", router.ResolveProviderName("avatar", providerName));
    }

    /// <summary>
    /// 路由键命中映射时返回映射的提供程序
    /// </summary>
    [Fact]
    public void ResolveProviderName_WithMappedRouteKey_ReturnsMappedProvider()
    {
        var options = new XiHanObjectStorageOptions { DefaultProvider = "Local" };
        options.RouteProviderMappings["avatar"] = "MinIO";
        var router = CreateRouter(options);

        Assert.Equal("MinIO", router.ResolveProviderName("avatar"));
    }

    /// <summary>
    /// 路由键的大小写与两端空白都不影响命中
    /// </summary>
    [Theory]
    [InlineData("avatar")]
    [InlineData("Avatar")]
    [InlineData("AVATAR")]
    [InlineData("  avatar  ")]
    public void ResolveProviderName_RouteKeyMatchIsCaseInsensitiveAndTrimmed(string routeKey)
    {
        var options = new XiHanObjectStorageOptions { DefaultProvider = "Local" };
        options.RouteProviderMappings["avatar"] = "MinIO";
        var router = CreateRouter(options);

        Assert.Equal("MinIO", router.ResolveProviderName(routeKey));
    }

    /// <summary>
    /// 映射值两端的空白被裁掉
    /// </summary>
    [Fact]
    public void ResolveProviderName_WithPaddedMappedValue_ReturnsTrimmedProvider()
    {
        var options = new XiHanObjectStorageOptions { DefaultProvider = "Local" };
        options.RouteProviderMappings["avatar"] = "  MinIO  ";
        var router = CreateRouter(options);

        Assert.Equal("MinIO", router.ResolveProviderName("avatar"));
    }

    /// <summary>
    /// 不传路由键与提供程序名时回落到默认提供程序
    /// </summary>
    [Fact]
    public void ResolveProviderName_WithoutAnyHint_ReturnsDefaultProvider()
    {
        var router = CreateRouter(new XiHanObjectStorageOptions { DefaultProvider = "Local" });

        Assert.Equal("Local", router.ResolveProviderName());
    }

    /// <summary>
    /// 非严格模式下路由键未命中映射时回落到默认提供程序
    /// </summary>
    [Fact]
    public void ResolveProviderName_WhenRouteKeyUnmappedAndLooseMatch_ReturnsDefaultProvider()
    {
        var options = new XiHanObjectStorageOptions
        {
            DefaultProvider = "Local",
            StrictRouteMatch = false
        };
        var router = CreateRouter(options);

        Assert.Equal("Local", router.ResolveProviderName("attachment"));
    }

    /// <summary>
    /// 严格模式下路由键未命中映射时抛 InvalidOperationException 并回显路由键
    /// </summary>
    [Fact]
    public void ResolveProviderName_WhenRouteKeyUnmappedAndStrictMatch_Throws()
    {
        var options = new XiHanObjectStorageOptions
        {
            DefaultProvider = "Local",
            StrictRouteMatch = true
        };
        var router = CreateRouter(options);

        var exception = Assert.Throws<InvalidOperationException>(() => router.ResolveProviderName("attachment"));

        Assert.Contains("attachment", exception.Message);
    }

    /// <summary>
    /// 严格模式只约束「传了路由键」的场景，不传路由键仍回落默认提供程序
    /// </summary>
    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveProviderName_WhenRouteKeyBlankAndStrictMatch_StillReturnsDefaultProvider(string? routeKey)
    {
        var options = new XiHanObjectStorageOptions
        {
            DefaultProvider = "Local",
            StrictRouteMatch = true
        };
        var router = CreateRouter(options);

        Assert.Equal("Local", router.ResolveProviderName(routeKey));
    }

    /// <summary>
    /// 映射值为空白等同于未命中，非严格模式下回落默认提供程序
    /// </summary>
    [Fact]
    public void ResolveProviderName_WhenMappedValueBlank_FallsBackToDefaultProvider()
    {
        var options = new XiHanObjectStorageOptions { DefaultProvider = "Local" };
        options.RouteProviderMappings["avatar"] = "   ";
        var router = CreateRouter(options);

        Assert.Equal("Local", router.ResolveProviderName("avatar"));
    }

    /// <summary>
    /// 映射值为空白且严格模式时按未命中处理抛异常
    /// </summary>
    [Fact]
    public void ResolveProviderName_WhenMappedValueBlankAndStrictMatch_Throws()
    {
        var options = new XiHanObjectStorageOptions
        {
            DefaultProvider = "Local",
            StrictRouteMatch = true
        };
        options.RouteProviderMappings["avatar"] = "   ";
        var router = CreateRouter(options);

        Assert.Throws<InvalidOperationException>(() => router.ResolveProviderName("avatar"));
    }

    /// <summary>
    /// 路由映射字典为空引用时不抛空引用异常，直接回落默认提供程序
    /// </summary>
    [Fact]
    public void ResolveProviderName_WhenMappingDictionaryNull_FallsBackToDefaultProvider()
    {
        var options = new XiHanObjectStorageOptions
        {
            DefaultProvider = "Local",
            RouteProviderMappings = null!
        };
        var router = CreateRouter(options);

        Assert.Equal("Local", router.ResolveProviderName("avatar"));
    }

    /// <summary>
    /// 路由后把解析出来的名称原样交给管理器，并返回管理器给出的实例
    /// </summary>
    [Fact]
    public void Route_WithRouteKey_AsksManagerForResolvedProviderName()
    {
        var expected = new AlternateFileStorageProvider();
        _manager.Register("MinIO", expected);
        var options = new XiHanObjectStorageOptions { DefaultProvider = "Local" };
        options.RouteProviderMappings["avatar"] = "MinIO";
        var router = CreateRouter(options);

        var provider = router.Route("avatar");

        Assert.Same(expected, provider);
        Assert.Equal(1, _manager.RequestedProviderNames.Count);
        Assert.Equal("MinIO", _manager.RequestedProviderNames[0]);
    }

    /// <summary>
    /// 不传任何提示时路由到默认提供程序
    /// </summary>
    [Fact]
    public void Route_WithoutAnyHint_AsksManagerForDefaultProvider()
    {
        var expected = new RecordingFileStorageProvider();
        _manager.Register("Local", expected);
        var router = CreateRouter(new XiHanObjectStorageOptions { DefaultProvider = "Local" });

        var provider = router.Route();

        Assert.Same(expected, provider);
        Assert.Equal("Local", _manager.RequestedProviderNames[0]);
    }

    /// <summary>
    /// 显式提供程序名会直接透传给管理器，跳过路由映射
    /// </summary>
    [Fact]
    public void Route_WithExplicitProviderName_BypassesRouteMappings()
    {
        var expected = new AlternateFileStorageProvider();
        _manager.Register("Alternate", expected);
        var options = new XiHanObjectStorageOptions { DefaultProvider = "Local" };
        options.RouteProviderMappings["avatar"] = "MinIO";
        var router = CreateRouter(options);

        var provider = router.Route("avatar", "Alternate");

        Assert.Same(expected, provider);
        Assert.Equal("Alternate", _manager.RequestedProviderNames[0]);
    }

    /// <summary>
    /// 构造被测路由器
    /// </summary>
    private DefaultFileStorageRouter CreateRouter(XiHanObjectStorageOptions options)
    {
        return new DefaultFileStorageRouter(_manager, new OptionsWrapper<XiHanObjectStorageOptions>(options));
    }
}
