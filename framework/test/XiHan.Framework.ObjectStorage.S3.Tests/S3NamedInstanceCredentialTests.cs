// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 命名实例必须自带密钥
/// </summary>
/// <remarks>
/// 安全审查 S-03（ADR-0178）：命名实例的设置可以由租户填写，原先没给密钥就用宿主的 AWS 凭据链建客户端，填设置的人就以宿主的身份访问它能访问的任何桶
/// </remarks>
public sealed class S3NamedInstanceCredentialTests
{
    [Fact]
    public async Task WithoutKeys_TheInstanceIsRejected_UnlessTheOperatorAllowsAmbientCredentials()
    {
        var denied = await CreateAsync(ambientCredentials: false, accessKey: null, secretKey: null);
        Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, denied.Error?.Code);

        var allowed = await CreateAsync(ambientCredentials: true, accessKey: null, secretKey: null);
        Assert.True(allowed.IsSuccess, allowed.ToString());
        await DisposeAsync(allowed);
    }

    [Fact]
    public async Task OneSidedKeys_AreAlwaysRejected()
    {
        Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, (await CreateAsync(ambientCredentials: true, accessKey: "key", secretKey: null)).Error?.Code);
        Assert.Equal(ObjectStorageErrors.StoreDefinitionInvalid, (await CreateAsync(ambientCredentials: true, accessKey: null, secretKey: "secret")).Error?.Code);
    }

    [Fact]
    public async Task BothKeys_CreateTheInstance()
    {
        var created = await CreateAsync(ambientCredentials: false, accessKey: "key", secretKey: "secret");
        Assert.True(created.IsSuccess, created.ToString());
        await DisposeAsync(created);
    }

    [Fact]
    public void TheOperatorSwitch_MustBeABoolean()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new(S3BlobStoreProvider.AmbientCredentialsKey, "maybe")]).Build();
        Assert.Throws<ArgumentException>(() => new S3BlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance, new OutboundAddressPolicy(new OutboundAddressOptions(), NullLoggerFactory.Instance), configuration));
    }

    /// <summary>
    /// 按给定密钥建一个不探测条件写入的命名实例
    /// </summary>
    private static async ValueTask<Primitives.Result<IBlobStore>> CreateAsync(bool ambientCredentials, string? accessKey, string? secretKey)
    {
        var settings = new Dictionary<string, string>
        {
            ["ServiceUrl"] = "http://tenant-store:9000",
            ["Region"] = "us-east-1",
            ["ConditionalWrites"] = "false",
        };
        if (accessKey is not null)
        {
            settings["AccessKey"] = accessKey;
        }

        if (secretKey is not null)
        {
            settings["SecretKey"] = secretKey;
        }

        var provider = new S3BlobStoreProvider(TimeProvider.System, NullLoggerFactory.Instance, _ => FakeS3Client.Create().AsClient(), ambientCredentials);
        return await provider.CreateAsync(new BlobStoreDefinition("tenant-a", S3BlobStoreProvider.ProviderName, settings), CancellationToken.None);
    }

    /// <summary>
    /// 释放建出的实例
    /// </summary>
    private static async ValueTask DisposeAsync(Primitives.Result<IBlobStore> created)
    {
        if (created.TryGetValue(out var store) && store is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
    }
}
