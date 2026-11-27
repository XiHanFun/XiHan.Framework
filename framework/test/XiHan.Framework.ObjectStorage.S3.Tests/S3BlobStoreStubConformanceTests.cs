// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Conformance;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 内存替身上的 S3 存储跑一致性用例 16
/// </summary>
/// <remarks>
/// 整套一致性用例对真实的服务跑（<see cref="S3BlobStoreConformanceTests"/>，未设置 <c>XIHAN_TEST_S3</c> 时跳过）；非 ASCII 的元数据往返另由替身兜住，不依赖服务：<see cref="FakeS3Client"/> 记下 SDK 请求里 Provider 编码之后的头值、HEAD 与整读原样交回，Provider 再解码（ADR-0344）。线上的写法与读法另由 <see cref="S3MetadataWireTests"/> 核对。套件的其余用例要替身不模拟的请求（列举、范围读取、内容的拼接与并发的条件写入），不在这里跑
/// </remarks>
public sealed class S3BlobStoreStubConformanceTests
{
    [Fact]
    public async Task Metadata_WithNonAsciiValues_RoundTrips() => await new StubSuite().Metadata_WithNonAsciiValues_RoundTrips();

    /// <summary>
    /// 以内存替身为客户端的一致性套件
    /// </summary>
    /// <remarks>
    /// 不公开：只借它的用例逐条调用，测试框架不把它当测试类、不跑整套用例
    /// </remarks>
    private sealed class StubSuite : BlobStoreConformance
    {
        /// <summary>
        /// 内存替身，预先建好套件的容器
        /// </summary>
        private readonly FakeS3Client _fake = FakeS3Client.Create("conformance");

        /// <inheritdoc />
        protected override ValueTask<IBlobStore> CreateStoreAsync()
            => ValueTask.FromResult<IBlobStore>(new S3BlobStore(_fake.AsClient(), new S3ObjectStorageOptions { ServiceUrl = "http://localhost:9000" }, TimeProvider.System, NullLoggerFactory.Instance));

        /// <inheritdoc />
        /// <remarks>
        /// 用例 16 用不到；替身不认绕过契约的写法
        /// </remarks>
        protected override Task PlantOutsideContractAsync(ContainerName container, string key, byte[] content)
            => throw new NotSupportedException("The fake S3 client does not plant objects outside the contract.");
    }
}
