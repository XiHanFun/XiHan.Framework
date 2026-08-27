// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.ObjectStorage.Options;
using XiHan.Framework.ObjectStorage.Providers;
using XiHan.Framework.ObjectStorage.Services;
using XiHan.Framework.ObjectStorage.Tests.Fakes;

namespace XiHan.Framework.ObjectStorage.Tests.Services;

/// <summary>
/// 默认文件存储提供程序管理器测试
/// </summary>
/// <remarks>
/// 管理器负责三件事：名称归一化（去空白、大小写不敏感、回落默认值）、按名从容器解析、解析结果缓存。
/// 这里用真实 <see cref="ServiceCollection"/> 装配，并且把 Provider 注册成 Transient，
/// 这样「两次 GetProvider 拿到同一实例」才真正证明缓存生效，而不是被 DI 的单例语义掩盖。
/// </remarks>
public class DefaultFileStorageProviderManagerTests
{
    /// <summary>
    /// 未指定名称时回落到配置里的默认提供程序
    /// </summary>
    [Fact]
    public void GetProvider_WithoutName_UsesConfiguredDefaultProvider()
    {
        using var serviceProvider = BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            new XiHanObjectStorageOptions { DefaultProvider = "Recording" },
            options => options.AddProvider("Recording", typeof(RecordingFileStorageProvider)));

        var provider = manager.GetProvider();

        Assert.IsType<RecordingFileStorageProvider>(provider);
        Assert.Equal("Recording", provider.ProviderName);
    }

    /// <summary>
    /// 提供程序名称的大小写与两端空白都不影响解析结果
    /// </summary>
    [Theory]
    [InlineData("Recording")]
    [InlineData("recording")]
    [InlineData("RECORDING")]
    [InlineData("  Recording  ")]
    public void GetProvider_WithNameVariants_ResolvesSameProviderType(string providerName)
    {
        using var serviceProvider = BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            new XiHanObjectStorageOptions { DefaultProvider = "Recording" },
            options => options.AddProvider("Recording", typeof(RecordingFileStorageProvider)));

        var provider = manager.GetProvider(providerName);

        Assert.IsType<RecordingFileStorageProvider>(provider);
    }

    /// <summary>
    /// 显式名称优先于配置里的默认提供程序
    /// </summary>
    [Fact]
    public void GetProvider_WithExplicitName_OverridesDefaultProvider()
    {
        using var serviceProvider = BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            new XiHanObjectStorageOptions { DefaultProvider = "Recording" },
            options =>
            {
                options.AddProvider("Recording", typeof(RecordingFileStorageProvider));
                options.AddProvider("Alternate", typeof(AlternateFileStorageProvider));
            });

        var provider = manager.GetProvider("Alternate");

        Assert.IsType<AlternateFileStorageProvider>(provider);
        Assert.Equal("Alternate", provider.ProviderName);
    }

    /// <summary>
    /// 同名多次解析复用缓存实例
    /// </summary>
    [Fact]
    public void GetProvider_CalledTwice_ReturnsCachedInstance()
    {
        using var serviceProvider = BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            new XiHanObjectStorageOptions { DefaultProvider = "Recording" },
            options => options.AddProvider("Recording", typeof(RecordingFileStorageProvider)));

        var first = manager.GetProvider("Recording");
        var second = manager.GetProvider("recording");

        Assert.Same(first, second);
    }

    /// <summary>
    /// 未注册的提供程序名称抛 InvalidOperationException 并回显名称
    /// </summary>
    [Fact]
    public void GetProvider_WithUnknownName_ThrowsWithProviderNameInMessage()
    {
        using var serviceProvider = BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            new XiHanObjectStorageOptions { DefaultProvider = "Recording" },
            options => options.AddProvider("Recording", typeof(RecordingFileStorageProvider)));

        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetProvider("Ftp"));

        Assert.Contains("Ftp", exception.Message);
    }

    /// <summary>
    /// 默认提供程序未配置且未指定名称时抛 InvalidOperationException
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetProvider_WhenDefaultProviderBlank_Throws(string defaultProvider)
    {
        using var serviceProvider = BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            new XiHanObjectStorageOptions { DefaultProvider = defaultProvider },
            options => options.AddProvider("Recording", typeof(RecordingFileStorageProvider)));

        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetProvider());

        Assert.Contains("默认提供程序未配置", exception.Message);
    }

    /// <summary>
    /// 注册表里有名字但容器里没登记实现类型时抛 InvalidOperationException
    /// </summary>
    [Fact]
    public void GetProvider_WhenTypeNotRegisteredInContainer_Throws()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            new XiHanObjectStorageOptions { DefaultProvider = "Recording" },
            options => options.AddProvider("Recording", typeof(RecordingFileStorageProvider)));

        Assert.Throws<InvalidOperationException>(() => manager.GetProvider());
    }

    /// <summary>
    /// 尝试获取已注册的提供程序返回 true 并给出实例
    /// </summary>
    [Fact]
    public void TryGetProvider_WhenRegistered_ReturnsTrueWithProvider()
    {
        using var serviceProvider = BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            new XiHanObjectStorageOptions { DefaultProvider = "Recording" },
            options => options.AddProvider("Recording", typeof(RecordingFileStorageProvider)));

        var succeeded = manager.TryGetProvider("Recording", out var provider);

        Assert.True(succeeded);
        Assert.NotNull(provider);
        Assert.Equal("Recording", provider!.ProviderName);
    }

    /// <summary>
    /// 尝试获取未注册的提供程序返回 false 且不抛异常
    /// </summary>
    [Fact]
    public void TryGetProvider_WhenUnknown_ReturnsFalseWithNull()
    {
        using var serviceProvider = BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            new XiHanObjectStorageOptions { DefaultProvider = "Recording" },
            options => options.AddProvider("Recording", typeof(RecordingFileStorageProvider)));

        var succeeded = manager.TryGetProvider("Ftp", out var provider);

        Assert.False(succeeded);
        Assert.Null(provider);
    }

    /// <summary>
    /// 名称为空时尝试获取会回落到默认提供程序
    /// </summary>
    [Fact]
    public void TryGetProvider_WithNullName_FallsBackToDefaultProvider()
    {
        using var serviceProvider = BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            new XiHanObjectStorageOptions { DefaultProvider = "Recording" },
            options => options.AddProvider("Recording", typeof(RecordingFileStorageProvider)));

        var succeeded = manager.TryGetProvider(null, out var provider);

        Assert.True(succeeded);
        Assert.NotNull(provider);
    }

    /// <summary>
    /// 已注册名称列表按大小写不敏感排序返回
    /// </summary>
    [Fact]
    public void GetRegisteredProviderNames_AreSortedIgnoringCase()
    {
        using var serviceProvider = BuildServiceProvider();
        var manager = CreateManager(
            serviceProvider,
            new XiHanObjectStorageOptions(),
            options =>
            {
                options.AddProvider("Zeta", typeof(RecordingFileStorageProvider));
                options.AddProvider("alpha", typeof(AlternateFileStorageProvider));
                options.AddProvider("Local", typeof(LocalFileStorageProvider));
            });

        var names = manager.GetRegisteredProviderNames();

        Assert.Equal(3, names.Count);
        Assert.Equal("alpha", names[0]);
        Assert.Equal("Local", names[1]);
        Assert.Equal("Zeta", names[2]);
    }

    /// <summary>
    /// 未注册任何提供程序时名称列表为空
    /// </summary>
    [Fact]
    public void GetRegisteredProviderNames_WhenNothingRegistered_IsEmpty()
    {
        using var serviceProvider = BuildServiceProvider();
        var manager = CreateManager(serviceProvider, new XiHanObjectStorageOptions(), _ => { });

        Assert.Empty(manager.GetRegisteredProviderNames());
    }

    /// <summary>
    /// 构造一个把两个替身 Provider 注册成 Transient 的容器
    /// </summary>
    private static ServiceProvider BuildServiceProvider()
    {
        return new ServiceCollection()
            .AddTransient<RecordingFileStorageProvider>()
            .AddTransient<AlternateFileStorageProvider>()
            .BuildServiceProvider();
    }

    /// <summary>
    /// 构造被测管理器
    /// </summary>
    private static DefaultFileStorageProviderManager CreateManager(
        IServiceProvider serviceProvider,
        XiHanObjectStorageOptions storageOptions,
        Action<XiHanObjectStorageProviderOptions> configureProviders)
    {
        var providerOptions = new XiHanObjectStorageProviderOptions();
        configureProviders(providerOptions);

        return new DefaultFileStorageProviderManager(
            serviceProvider,
            new OptionsWrapper<XiHanObjectStorageProviderOptions>(providerOptions),
            new OptionsWrapper<XiHanObjectStorageOptions>(storageOptions));
    }
}
