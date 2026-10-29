// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.ObjectStorage.Conformance;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// S3 命名实例 Provider 的一致性用例。
/// </summary>
/// <remarks>
/// 让 <see cref="S3BlobStoreProvider"/> 对真实的 S3 兼容服务跑完整套 <see cref="BlobStoreProviderConformance"/> 用例。
/// 未设置 <c>XIHAN_TEST_S3</c> 时跳过。
/// </remarks>
public sealed class S3BlobStoreProviderConformanceTests : BlobStoreProviderConformance
{
    /// <inheritdoc />
    protected override IBlobStoreProvider CreateProvider() => new S3BlobStoreProvider(TimeProvider.System);

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string> ValidSettings()
    {
        S3TestServer.EnsureBucket(S3TestServer.Client(), Container.Value);
        return new Dictionary<string, string>
        {
            ["ServiceUrl"] = S3TestServer.RequireServiceUrl(),
            ["AccessKey"] = S3TestServer.AccessKey,
            ["SecretKey"] = S3TestServer.SecretKey,
        };
    }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string> OtherValidSettings()
        => new Dictionary<string, string>(ValidSettings()) { ["HealthBucket"] = Container.Value, ["ForcePathStyle"] = "true" };
}
