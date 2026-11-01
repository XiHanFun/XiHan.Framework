// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Cli.Packaging;
using XiHan.Framework.Configuration.Nacos;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Kafka;
using XiHan.Framework.Messaging.RabbitMQ;
using XiHan.Framework.ObjectStorage.S3;
using XiHan.Framework.Persistence.EntityFrameworkCore;
using XiHan.Framework.Persistence.SqlSugar;

namespace XiHan.Framework.Cli.Tests.Packaging;

/// <summary>
/// 框架 Provider 清单声明的绑定类型都能打包。
/// </summary>
/// <remarks>
/// <c>xihan pack</c> 认识它们，并为每种生成依赖服务。
/// </remarks>
public sealed class BindingKindTests
{
    /// <summary>
    /// 声明了资源绑定的框架 Provider。
    /// </summary>
    private static IFeature[] Providers() =>
    [
        new SqlSugarPersistenceFeature(),
        new EntityFrameworkCorePersistenceFeature<DbContext>(static (_, _) => { }),
        new RedisCachingFeature(),
        new RabbitMQMessagingFeature(),
        new KafkaMessagingFeature(),
        new S3ObjectStorageFeature(),
        new NacosConfigurationFeature(),
    ];

    [Fact]
    public void EveryProviderBindingType_IsAPackableBindingKind()
    {
        var declared = Providers().SelectMany(static f => f.Manifest.Bindings).Select(static b => b.Type).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["blob", "cache:distributed", "configuration:remote", "connection:relational", "queue", "stream"], declared);
        Assert.Equal(declared, BindingKind.All.Select(static k => k.Type).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void KafkaAndNacos_DeclareTheBindingNamesTheirOptionsRead()
    {
        Assert.Equal([("stream", "stream")], new KafkaMessagingFeature().Manifest.Bindings.Select(static b => (b.Name, b.Type)));
        Assert.Equal([("nacos", "configuration:remote")], new NacosConfigurationFeature().Manifest.Bindings.Select(static b => (b.Name, b.Type)));
    }

    [Fact]
    public void EveryBindingKind_PinsItsComposeImageTag()
    {
        // 新增的种类固定镜像版本；blob 的 MinIO 镜像沿用既有写法
        foreach (var kind in BindingKind.All.Where(static k => k.Type != "blob"))
        {
            var tag = kind.Image[(kind.Image.LastIndexOf(':') + 1)..];
            Assert.True(kind.Image.Contains(':', StringComparison.Ordinal) && tag != "latest", $"'{kind.Type}' must pin its image tag, but uses '{kind.Image}'.");
        }
    }
}
