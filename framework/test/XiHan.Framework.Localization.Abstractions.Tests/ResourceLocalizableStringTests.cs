// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Localization.Abstractions.Tests.Fakes;

namespace XiHan.Framework.Localization.Abstractions.Tests;

/// <summary>
/// 资源本地化字符串测试
/// </summary>
/// <remarks>
/// 该类型有两条互斥的构造路径（按资源类型 / 按资源名），Localize 会据此选用工厂的不同重载。
/// 测试重点：两条路径互不串味、参数校验的异常类型正确、有无格式化参数走不同取值重载。
/// </remarks>
public class ResourceLocalizableStringTests
{
    /// <summary>
    /// 按资源类型构造时 ResourceType 被填充且 ResourceName 保持为空
    /// </summary>
    [Fact]
    public void Constructor_WithResourceType_LeavesResourceNameNull()
    {
        var sut = new ResourceLocalizableString(typeof(ResourceLocalizableStringTests), "Title");

        Assert.Equal(typeof(ResourceLocalizableStringTests), sut.ResourceType);
        Assert.Null(sut.ResourceName);
        Assert.Equal("Title", sut.Name);
    }

    /// <summary>
    /// 按资源名构造时 ResourceName 被填充且 ResourceType 保持为空
    /// </summary>
    [Fact]
    public void Constructor_WithResourceName_LeavesResourceTypeNull()
    {
        var sut = new ResourceLocalizableString("Errors", "Title");

        Assert.Equal("Errors", sut.ResourceName);
        Assert.Null(sut.ResourceType);
        Assert.Equal("Title", sut.Name);
    }

    /// <summary>
    /// 资源类型为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenResourceTypeNull_ThrowsArgumentNullException()
    {
        Type resourceType = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => new ResourceLocalizableString(resourceType, "Title"));

        Assert.Equal("resourceType", exception.ParamName);
    }

    /// <summary>
    /// 资源名为空或纯空白时抛出 ArgumentException（不是 ArgumentNullException）
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenResourceNameBlank_ThrowsArgumentException(string? resourceName)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ResourceLocalizableString(resourceName!, "Title"));

        Assert.Equal("resourceName", exception.ParamName);
    }

    /// <summary>
    /// 按资源类型构造时资源键为空或纯空白必须抛出 ArgumentException
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ByResourceType_WhenNameBlank_ThrowsArgumentException(string? name)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new ResourceLocalizableString(typeof(ResourceLocalizableStringTests), name!));

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// 按资源名构造时资源键为空或纯空白必须抛出 ArgumentException
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ByResourceName_WhenNameBlank_ThrowsArgumentException(string? name)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ResourceLocalizableString("Errors", name!));

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// 资源名与资源键同时为空时，先报资源名
    /// </summary>
    [Fact]
    public void Constructor_WhenResourceNameAndNameBothBlank_ReportsResourceNameFirst()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ResourceLocalizableString(string.Empty, string.Empty));

        Assert.Equal("resourceName", exception.ParamName);
    }

    /// <summary>
    /// 不传格式化参数时 Arguments 为空数组而不是 null
    /// </summary>
    [Fact]
    public void Constructor_WithoutArguments_UsesEmptyArgumentArray()
    {
        var byType = new ResourceLocalizableString(typeof(ResourceLocalizableStringTests), "Title");
        var byName = new ResourceLocalizableString("Errors", "Title");

        Assert.Empty(byType.Arguments);
        Assert.Empty(byName.Arguments);
    }

    /// <summary>
    /// 显式传入 null 参数数组时归一为空数组
    /// </summary>
    [Fact]
    public void Constructor_WhenArgumentArrayNull_UsesEmptyArgumentArray()
    {
        object[] arguments = null!;

        var byType = new ResourceLocalizableString(typeof(ResourceLocalizableStringTests), "Title", arguments);
        var byName = new ResourceLocalizableString("Errors", "Title", arguments);

        Assert.Empty(byType.Arguments);
        Assert.Empty(byName.Arguments);
    }

    /// <summary>
    /// 格式化参数按传入顺序原样保留
    /// </summary>
    [Fact]
    public void Constructor_WithArguments_KeepsArgumentsInOrder()
    {
        var sut = new ResourceLocalizableString("Errors", "Greeting", "曦寒", 2025);

        Assert.Equal(2, sut.Arguments.Length);
        Assert.Equal("曦寒", (string)sut.Arguments[0]);
        Assert.Equal(2025, (int)sut.Arguments[1]);
    }

    /// <summary>
    /// 按资源类型构造时必须走工厂的类型重载
    /// </summary>
    [Fact]
    public void Localize_WithResourceType_CreatesLocalizerByType()
    {
        var localizer = new FakeStringLocalizer(new Dictionary<string, string> { ["Title"] = "标题" });
        var factory = new FakeStringLocalizerFactory(localizer);
        var sut = new ResourceLocalizableString(typeof(ResourceLocalizableStringTests), "Title");

        var localized = sut.Localize(factory);

        Assert.Equal(typeof(ResourceLocalizableStringTests), Assert.Single(factory.CreatedResourceTypes));
        Assert.Empty(factory.CreatedResourceNames);
        Assert.Equal("标题", localized.Value);
    }

    /// <summary>
    /// 按资源名构造时必须走工厂的字符串重载，且 baseName 为资源名
    /// </summary>
    [Fact]
    public void Localize_WithResourceName_CreatesLocalizerByBaseName()
    {
        var localizer = new FakeStringLocalizer(new Dictionary<string, string> { ["Title"] = "标题" });
        var factory = new FakeStringLocalizerFactory(localizer);
        var sut = new ResourceLocalizableString("Errors", "Title");

        var localized = sut.Localize(factory);

        var created = Assert.Single(factory.CreatedResourceNames);
        Assert.Equal("Errors", created.BaseName);
        Assert.Empty(factory.CreatedResourceTypes);
        Assert.Equal("标题", localized.Value);
    }

    /// <summary>
    /// 无格式化参数时走本地化器的无参索引器
    /// </summary>
    [Fact]
    public void Localize_WithoutArguments_UsesParameterlessIndexer()
    {
        var localizer = new FakeStringLocalizer(new Dictionary<string, string> { ["Title"] = "标题" });
        var factory = new FakeStringLocalizerFactory(localizer);
        var sut = new ResourceLocalizableString("Errors", "Title");

        var localized = sut.Localize(factory);

        Assert.Equal("Title", Assert.Single(localizer.RequestedNames));
        Assert.Empty(localizer.RequestedArguments);
        Assert.False(localized.ResourceNotFound);
    }

    /// <summary>
    /// 有格式化参数时走带参索引器并把参数原样透传
    /// </summary>
    [Fact]
    public void Localize_WithArguments_PassesArgumentsToIndexer()
    {
        var localizer = new FakeStringLocalizer(new Dictionary<string, string> { ["Greeting"] = "你好，{0}！" });
        var factory = new FakeStringLocalizerFactory(localizer);
        var sut = new ResourceLocalizableString("Errors", "Greeting", "曦寒");

        var localized = sut.Localize(factory);

        var arguments = Assert.Single(localizer.RequestedArguments);
        Assert.Single(arguments);
        Assert.Equal("曦寒", (string)arguments[0]);
        Assert.Equal("你好，曦寒！", localized.Value);
    }

    /// <summary>
    /// 资源缺失时保留 ResourceNotFound 标记并回落到资源键
    /// </summary>
    [Fact]
    public void Localize_WhenResourceMissing_KeepsResourceNotFoundFlag()
    {
        var factory = new FakeStringLocalizerFactory();
        var sut = new ResourceLocalizableString("Errors", "Missing.Key");

        var localized = sut.Localize(factory);

        Assert.True(localized.ResourceNotFound);
        Assert.Equal("Missing.Key", localized.Value);
    }

    /// <summary>
    /// 工厂为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Localize_WhenFactoryNull_ThrowsArgumentNullException()
    {
        var sut = new ResourceLocalizableString("Errors", "Title");

        var exception = Assert.Throws<ArgumentNullException>(() => sut.Localize(null!));

        Assert.Equal("stringLocalizerFactory", exception.ParamName);
    }

    /// <summary>
    /// 该类型必须可作为 ILocalizableString 使用
    /// </summary>
    [Fact]
    public void Type_ImplementsLocalizableStringContract()
    {
        var sut = new ResourceLocalizableString("Errors", "Title");

        Assert.IsAssignableFrom<ILocalizableString>(sut);
    }
}
