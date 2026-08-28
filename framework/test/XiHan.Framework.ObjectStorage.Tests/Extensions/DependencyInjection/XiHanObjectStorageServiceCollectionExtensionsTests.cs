// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.ObjectStorage.Constants;
using XiHan.Framework.ObjectStorage.Extensions.DependencyInjection;
using XiHan.Framework.ObjectStorage.Options;
using XiHan.Framework.ObjectStorage.Providers;
using XiHan.Framework.ObjectStorage.Services;
using XiHan.Framework.ObjectStorage.Tests.Fakes;

namespace XiHan.Framework.ObjectStorage.Tests.Extensions.DependencyInjection;

/// <summary>
/// 对象存储服务注册扩展测试
/// </summary>
/// <remarks>
/// 这组用例覆盖装配阶段的三件事：核心服务的生命周期与幂等、Provider 名称到实现类型的映射表、
/// 以及「按配置自动注册启用的 Provider」这条链路（含不认识的名字要早失败）。
/// 除了最后一条端到端用例，其余都只检查注册结果、不真正解析 Provider 实例，
/// 避免云端 SDK 客户端在装配断言里被意外创建。
/// </remarks>
public sealed class XiHanObjectStorageServiceCollectionExtensionsTests : IDisposable
{
    private readonly string _root;

    /// <summary>
    /// 构造函数：为当前用例准备独占的临时根目录
    /// </summary>
    public XiHanObjectStorageServiceCollectionExtensionsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    /// 核心服务按单例注册且指向默认实现
    /// </summary>
    [Fact]
    public void AddXiHanObjectStorage_RegistersManagerAndRouterAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanObjectStorage();

        var managerDescriptor = services.Single(descriptor => descriptor.ServiceType == typeof(IFileStorageProviderManager));
        var routerDescriptor = services.Single(descriptor => descriptor.ServiceType == typeof(IFileStorageRouter));

        Assert.Equal(typeof(DefaultFileStorageProviderManager), managerDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, managerDescriptor.Lifetime);
        Assert.Equal(typeof(DefaultFileStorageRouter), routerDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, routerDescriptor.Lifetime);
    }

    /// <summary>
    /// 重复注册核心服务不会产生重复描述符
    /// </summary>
    [Fact]
    public void AddXiHanObjectStorage_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection();

        services.AddXiHanObjectStorage();
        services.AddXiHanObjectStorage();

        Assert.Equal(1, services.Count(descriptor => descriptor.ServiceType == typeof(IFileStorageProviderManager)));
        Assert.Equal(1, services.Count(descriptor => descriptor.ServiceType == typeof(IFileStorageRouter)));
    }

    /// <summary>
    /// 代码方式配置的选项能被解析出来
    /// </summary>
    [Fact]
    public void AddXiHanObjectStorage_WithConfigureDelegate_AppliesOptions()
    {
        var services = new ServiceCollection();

        services.AddXiHanObjectStorage(options =>
        {
            options.DefaultProvider = ObjectStorageProviderNames.Minio;
            options.StrictRouteMatch = true;
        });

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<XiHanObjectStorageOptions>>().Value;

        Assert.Equal(ObjectStorageProviderNames.Minio, options.DefaultProvider);
        Assert.True(options.StrictRouteMatch);
    }

    /// <summary>
    /// 不传配置委托时选项保持默认值
    /// </summary>
    [Fact]
    public void AddXiHanObjectStorage_WithoutConfigureDelegate_KeepsDefaults()
    {
        var services = new ServiceCollection();

        services.AddXiHanObjectStorage();

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<XiHanObjectStorageOptions>>().Value;

        Assert.Equal(ObjectStorageProviderNames.Local, options.DefaultProvider);
        Assert.False(options.StrictRouteMatch);
    }

    /// <summary>
    /// 配置对象为空时抛 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddXiHanObjectStorage_WithNullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddXiHanObjectStorage((IConfiguration)null!));
    }

    /// <summary>
    /// 注册自定义提供程序时名称两端空白被裁掉
    /// </summary>
    [Fact]
    public void AddFileStorageProvider_TrimsProviderNameAndMapsType()
    {
        var services = new ServiceCollection();
        services.AddXiHanObjectStorage();

        services.AddFileStorageProvider<RecordingFileStorageProvider>("  Custom  ");

        using var serviceProvider = services.BuildServiceProvider();
        var providerTypes = serviceProvider.GetRequiredService<IOptions<XiHanObjectStorageProviderOptions>>().Value.ProviderTypes;

        Assert.True(providerTypes.ContainsKey("Custom"));
        Assert.Equal(typeof(RecordingFileStorageProvider), providerTypes["Custom"]);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RecordingFileStorageProvider));
    }

    /// <summary>
    /// 注册自定义提供程序时名称为空或纯空白抛 ArgumentException
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddFileStorageProvider_WithBlankName_ThrowsArgumentException(string providerName)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddFileStorageProvider<RecordingFileStorageProvider>(providerName));
    }

    /// <summary>
    /// 注册自定义提供程序时名称为 null 抛 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddFileStorageProvider_WithNullName_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddFileStorageProvider<RecordingFileStorageProvider>(null!));
    }

    /// <summary>
    /// 四个内置注册方法把常量名称映射到对应实现类型
    /// </summary>
    /// <remarks>
    /// 名称与实现类型的对应关系是配置文件与运行期解析之间的桥，写错会在启动后才暴露，
    /// 这里只做注册断言、不解析实例，避免创建真实的云端 SDK 客户端。
    /// </remarks>
    [Fact]
    public void AddBuiltInProviders_MapProviderNamesToImplementationTypes()
    {
        var services = new ServiceCollection();
        services.AddXiHanObjectStorage();

        services.AddLocalFileStorageProvider();
        services.AddMinioFileStorageProvider();
        services.AddAliyunOssFileStorageProvider();
        services.AddTencentCosFileStorageProvider();

        using var serviceProvider = services.BuildServiceProvider();
        var providerTypes = serviceProvider.GetRequiredService<IOptions<XiHanObjectStorageProviderOptions>>().Value.ProviderTypes;

        Assert.Equal(typeof(LocalFileStorageProvider), providerTypes[ObjectStorageProviderNames.Local]);
        Assert.Equal(typeof(MinioFileStorageProvider), providerTypes[ObjectStorageProviderNames.Minio]);
        Assert.Equal(typeof(AliyunOssStorageProvider), providerTypes[ObjectStorageProviderNames.AliyunOss]);
        Assert.Equal(typeof(TencentCosStorageProvider), providerTypes[ObjectStorageProviderNames.TencentCos]);
    }

    /// <summary>
    /// 本地提供程序的配置委托会作用到本地存储选项上
    /// </summary>
    [Fact]
    public void AddLocalFileStorageProvider_WithConfigureDelegate_AppliesLocalOptions()
    {
        var services = new ServiceCollection();
        services.AddXiHanObjectStorage();

        services.AddLocalFileStorageProvider(options =>
        {
            options.RootPath = _root;
            options.UrlPrefix = "/files";
        });

        using var serviceProvider = services.BuildServiceProvider();
        var localOptions = serviceProvider.GetRequiredService<IOptions<LocalStorageOptions>>().Value;

        Assert.Equal(_root, localOptions.RootPath);
        Assert.Equal("/files", localOptions.UrlPrefix);
    }

    /// <summary>
    /// 从配置绑定时同时绑上总选项与各 Provider 选项
    /// </summary>
    [Fact]
    public void AddXiHanObjectStorage_WithConfiguration_BindsOptionsSections()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["XiHan:ObjectStorage:DefaultProvider"] = ObjectStorageProviderNames.Local,
            ["XiHan:ObjectStorage:StrictRouteMatch"] = "true",
            ["XiHan:ObjectStorage:RouteProviderMappings:avatar"] = ObjectStorageProviderNames.Local,
            ["XiHan:ObjectStorage:Local:RootPath"] = _root,
            ["XiHan:ObjectStorage:Local:UrlPrefix"] = "/files",
            ["XiHan:ObjectStorage:Minio:Endpoint"] = "minio.local:9000",
            ["XiHan:ObjectStorage:AliyunOss:Endpoint"] = "oss-cn-hangzhou.aliyuncs.com",
            ["XiHan:ObjectStorage:TencentCos:Region"] = "ap-guangzhou"
        });
        var services = new ServiceCollection();

        services.AddXiHanObjectStorage(configuration);

        using var serviceProvider = services.BuildServiceProvider();

        var storageOptions = serviceProvider.GetRequiredService<IOptions<XiHanObjectStorageOptions>>().Value;
        Assert.Equal(ObjectStorageProviderNames.Local, storageOptions.DefaultProvider);
        Assert.True(storageOptions.StrictRouteMatch);
        Assert.Equal(ObjectStorageProviderNames.Local, storageOptions.RouteProviderMappings["avatar"]);

        Assert.Equal(_root, serviceProvider.GetRequiredService<IOptions<LocalStorageOptions>>().Value.RootPath);
        Assert.Equal("/files", serviceProvider.GetRequiredService<IOptions<LocalStorageOptions>>().Value.UrlPrefix);
        Assert.Equal("minio.local:9000", serviceProvider.GetRequiredService<IOptions<MinioStorageOptions>>().Value.Endpoint);
        Assert.Equal("oss-cn-hangzhou.aliyuncs.com", serviceProvider.GetRequiredService<IOptions<AliyunOssStorageOptions>>().Value.Endpoint);
        Assert.Equal("ap-guangzhou", serviceProvider.GetRequiredService<IOptions<TencentCosStorageOptions>>().Value.Region);
    }

    /// <summary>
    /// 配置为空节时按默认值注册本地提供程序
    /// </summary>
    [Fact]
    public void AddXiHanObjectStorage_WithEmptyConfiguration_RegistersLocalProviderByDefault()
    {
        var configuration = BuildConfiguration([]);
        var services = new ServiceCollection();

        services.AddXiHanObjectStorage(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var providerTypes = serviceProvider.GetRequiredService<IOptions<XiHanObjectStorageProviderOptions>>().Value.ProviderTypes;

        Assert.Equal(typeof(LocalFileStorageProvider), providerTypes[ObjectStorageProviderNames.Local]);
    }

    /// <summary>
    /// 配置里启用的提供程序会被自动注册
    /// </summary>
    [Fact]
    public void AddXiHanObjectStorage_WithEnabledProviders_RegistersEachOfThem()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["XiHan:ObjectStorage:DefaultProvider"] = ObjectStorageProviderNames.Local,
            ["XiHan:ObjectStorage:EnabledProviders:0"] = ObjectStorageProviderNames.Local,
            ["XiHan:ObjectStorage:EnabledProviders:1"] = ObjectStorageProviderNames.Minio
        });
        var services = new ServiceCollection();

        services.AddXiHanObjectStorage(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var providerTypes = serviceProvider.GetRequiredService<IOptions<XiHanObjectStorageProviderOptions>>().Value.ProviderTypes;

        Assert.Equal(typeof(LocalFileStorageProvider), providerTypes[ObjectStorageProviderNames.Local]);
        Assert.Equal(typeof(MinioFileStorageProvider), providerTypes[ObjectStorageProviderNames.Minio]);
    }

    /// <summary>
    /// 配置里出现不认识的提供程序名时在装配阶段就抛异常
    /// </summary>
    /// <remarks>
    /// 这条早失败很关键：拼错名字如果拖到运行期才暴露，表现是「上传时找不到 Provider」，定位成本高得多。
    /// </remarks>
    [Fact]
    public void AddXiHanObjectStorage_WithUnsupportedProviderName_ThrowsAtRegistrationTime()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["XiHan:ObjectStorage:EnabledProviders:0"] = "Ftp"
        });
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddXiHanObjectStorage(configuration));

        Assert.Contains("Ftp", exception.Message);
    }

    /// <summary>
    /// 装配完成后路由器能真正解析出本地提供程序并给出直链
    /// </summary>
    /// <remarks>
    /// 唯一一条会真实创建 Provider 实例的用例：本地存储不依赖外部服务，
    /// 根目录指向本用例独占的临时目录，构造时创建、Dispose 时清理。
    /// </remarks>
    [Fact]
    public async Task Router_AfterRegistration_ResolvesLocalFileStorageProvider()
    {
        var rootPath = Path.Combine(_root, "e2e");
        var services = new ServiceCollection();
        services.AddXiHanObjectStorage(options => options.DefaultProvider = ObjectStorageProviderNames.Local);
        services.AddLocalFileStorageProvider(options =>
        {
            options.RootPath = rootPath;
            options.UrlPrefix = "/files";
        });

        using var serviceProvider = services.BuildServiceProvider();
        var router = serviceProvider.GetRequiredService<IFileStorageRouter>();
        var manager = serviceProvider.GetRequiredService<IFileStorageProviderManager>();

        var provider = router.Route();

        Assert.IsType<LocalFileStorageProvider>(provider);
        Assert.Equal(ObjectStorageProviderNames.Local, provider.ProviderName);
        Assert.True(Directory.Exists(rootPath));
        Assert.Same(provider, manager.GetProvider(ObjectStorageProviderNames.Local));
        Assert.Equal(
            "/files/docs/a.txt",
            await provider.GeneratePresignedUrlAsync("docs/a.txt", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 清理当前用例的临时目录
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }
        catch
        {
            // 文件被占用或已被清理都不应影响用例结论，忽略
        }
    }

    /// <summary>
    /// 用内存键值对构造一份配置
    /// </summary>
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
