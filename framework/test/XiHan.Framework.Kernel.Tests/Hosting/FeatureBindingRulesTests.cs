// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// 内核装配特性时对清单声明的绑定的核对
/// </summary>
/// <remarks>
/// RFC-0016「资源绑定与配置节」第 8 条（ADR-0301）：与插件宿主同一套规则（<c>BindingRules</c>）。必需的绑定没有内容（没给，或只给了类型）、来源声明的类型与清单不同（必需与可选都核对）即启动失败，按清单的顺序报第一处
/// </remarks>
public sealed class FeatureBindingRulesTests : IDisposable
{
    /// <summary>
    /// 本用例的 Service Binding 根目录
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xihan-feature-bindings-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void ARequiredBindingWithOnlyAType_FailsAtBuildTime()
    {
        // 修之前「来源里有这个名字」就算给出：只有类型的绑定让特性带着一个没有连接的 BindingValue 装配
        var feature = Feature(new Binding("db", "connection:relational"));

        var exception = Assert.Throws<FrameworkException>(() => Hosts.BuildApp(b => b.UseFeature(feature), Hosts.Configuration(("XiHan:Bindings:db:type", "connection:relational"))));

        Assert.Equal(KernelErrors.MissingBinding, exception.Code);
        Assert.Equal("Feature 'xihan.persistence.inmemory' is not configured: binding 'db' is required but not provided.", exception.Message);
    }

    [Fact]
    public void ARequiredBindingMountedWithOnlyATypeFile_FailsAtBuildTime()
    {
        // 目录来源同理：只有 type 文件的绑定目录没有主值也没有属性
        Directory.CreateDirectory(Path.Combine(_root, "db"));
        File.WriteAllText(Path.Combine(_root, "db", "type"), "connection:relational\n");
        var feature = Feature(new Binding("db", "connection:relational"));

        var exception = Assert.Throws<FrameworkException>(() => Hosts.BuildApp(b => b.UseServiceBindingRoot(_root).UseFeature(feature)));

        Assert.Equal(KernelErrors.MissingBinding, exception.Code);
        Assert.Equal("Feature 'xihan.persistence.inmemory' is not configured: binding 'db' is required but not provided.", exception.Message);
    }

    [Fact]
    public void ARequiredBindingThatIsNotProvided_NamesTheFeatureAndTheBinding()
    {
        var feature = Feature(new Binding("db", "connection:relational"));

        var exception = Assert.Throws<FrameworkException>(() => Hosts.BuildApp(b => b.UseFeature(feature)));

        Assert.Equal(KernelErrors.MissingBinding, exception.Code);
        Assert.Equal("Feature 'xihan.persistence.inmemory' is not configured: binding 'db' is required but not provided.", exception.Message);
    }

    [Fact]
    public void AnOptionalBindingOfAnotherType_FailsAtBuildTime_WithoutItsValues()
    {
        // 修之前可选的绑定不核对类型：特性拿着消息队列的地址去连关系库
        var feature = Feature(new Binding("db", "connection:relational") { Required = false });
        var configuration = Hosts.Configuration(("XiHan:Bindings:db:type", "queue"), ("XiHan:Bindings:db:value", "amqp://app:s3cret@mq"), ("XiHan:Bindings:db:password", "s3cret"));

        var exception = Assert.Throws<FrameworkException>(() => Hosts.BuildApp(b => b.UseFeature(feature), configuration));

        Assert.Equal(KernelErrors.MissingBinding, exception.Code);
        Assert.Equal("Feature 'xihan.persistence.inmemory' is not configured: binding 'db' is of type 'queue' but the manifest declares 'connection:relational'.", exception.Message);
        Assert.DoesNotContain("s3cret", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ARequiredBindingOfAnotherType_FailsAtBuildTime()
    {
        var feature = Feature(new Binding("db", "connection:relational"));
        var configuration = Hosts.Configuration(("XiHan:Bindings:db:type", "connection:document"), ("XiHan:Bindings:db:value", "mongodb://db/app"));

        var exception = Assert.Throws<FrameworkException>(() => Hosts.BuildApp(b => b.UseFeature(feature), configuration));

        Assert.Equal("Feature 'xihan.persistence.inmemory' is not configured: binding 'db' is of type 'connection:document' but the manifest declares 'connection:relational'.", exception.Message);
    }

    [Theory]
    [InlineData(true, "XiHan:Bindings:db:host", "db.internal")]
    [InlineData(true, "XiHan:Bindings:db:value", "Host=db")]
    [InlineData(false, "XiHan:Bindings:other", "x")]
    [InlineData(false, "XiHan:Bindings:db:type", "queue")]
    public void BindingsThatAreProvidedOrOptional_AreConfigured(bool required, string key, string value)
    {
        // 只有属性也算给出了；可选的没有内容就不核对类型（特性经 FindBinding 自己处理没有它的情形）
        var configured = false;
        var feature = new RecordingFeature(Manifests.Create("xihan.persistence.inmemory", bindings: [new Binding("db", "connection:relational") { Required = required }]), [])
        {
            OnConfigure = context => configured = true,
        };

        var (provider, _) = Hosts.BuildApp(b => b.UseFeature(feature), Hosts.Configuration((key, value)));
        using var _ = provider;

        Assert.True(configured, "The feature must be configured.");
    }

    [Fact]
    public void TheFirstBindingThatDoesNotMatch_InManifestOrder_IsReported()
    {
        // 两个绑定都不合：报清单里排在前面的那个，与插件宿主一致
        var feature = Feature(new Binding("db", "connection:relational") { Required = false }, new Binding("cache", "cache:distributed"));
        var configuration = Hosts.Configuration(("XiHan:Bindings:db:type", "queue"), ("XiHan:Bindings:db:value", "amqp://mq"));

        var exception = Assert.Throws<FrameworkException>(() => Hosts.BuildApp(b => b.UseFeature(feature), configuration));

        Assert.Equal("Feature 'xihan.persistence.inmemory' is not configured: binding 'db' is of type 'queue' but the manifest declares 'connection:relational'.", exception.Message);

        var swapped = Feature(new Binding("cache", "cache:distributed"), new Binding("db", "connection:relational") { Required = false });
        Assert.Equal("Feature 'xihan.persistence.inmemory' is not configured: binding 'cache' is required but not provided.", Assert.Throws<FrameworkException>(() => Hosts.BuildApp(b => b.UseFeature(swapped), configuration)).Message);
    }

    /// <summary>
    /// 声明这些绑定、什么也不做的特性
    /// </summary>
    private static RecordingFeature Feature(params Binding[] bindings)
        => new(Manifests.Create("xihan.persistence.inmemory", bindings: bindings), []);
}
