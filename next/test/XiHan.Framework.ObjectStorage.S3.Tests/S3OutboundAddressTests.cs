// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 命名实例的端点过出站地址策略（RFC-0012、ADR-0117）
/// </summary>
/// <remarks>
/// 命名实例的定义可能来自应用的库，端点不可信。全部走真实的 SDK 与套接字；放行之后能正常工作由对真实服务的 <see cref="S3BlobStoreProviderConformanceTests"/> 验证
/// </remarks>
public sealed class S3OutboundAddressTests
{
    [Fact]
    public async Task NamedStore_DeniedEndpoint_NeverConnects_AndReportsUnavailable()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var logs = new LogCapture();
        var registry = new BlobStores([new S3BlobStoreProvider(TimeProvider.System, logs, new OutboundAddressPolicy(new OutboundAddressOptions(), logs))], TimeProvider.System, logs);
        await using (registry)
        {
            var settings = new Dictionary<string, string>
            {
                ["ServiceUrl"] = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}",
                ["AccessKey"] = "ak",
                ["SecretKey"] = "sk",
                ["ConditionalWrites"] = "false",
            };
            var store = (await registry.GetOrAddAsync(new BlobStoreDefinition("tenant", S3BlobStoreProvider.ProviderName, settings), CancellationToken.None)).ValueOrThrow();

            var put = await store.PutAsync("media", "a.txt", Encoding.UTF8.GetBytes("a"), new BlobPutOptions(), CancellationToken.None);

            Assert.Equal(ObjectStorageErrors.StoreUnavailable, put.Error?.Code);
            Assert.False(listener.Pending());
            Assert.Contains(logs.Entries, static entry => entry.Category == typeof(OutboundAddressPolicy).FullName && entry.Level == LogLevel.Warning);
        }
    }

    [Fact]
    public void DefaultClient_KeepsTheSdksOwnTransport()
    {
        // 缺省实例的端点是运维配的，不过策略；SDK 的配置里不能多出工厂
        using var client = S3ObjectStorageFeature.CreateClient(new S3ObjectStorageOptions { ServiceUrl = "http://127.0.0.1:1", AccessKey = "ak", SecretKey = "sk" });

        Assert.Null(client.Config.HttpClientFactory);
    }
}
