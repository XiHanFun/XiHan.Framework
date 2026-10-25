// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Conformance;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 让 <see cref="S3BlobStore"/> 对真实的 S3 兼容服务跑完整套 <see cref="BlobStoreConformance"/> 用例；未设置 <c>XIHAN_TEST_S3</c> 时跳过。
/// </summary>
public sealed class S3BlobStoreConformanceTests : BlobStoreConformance
{
    /// <inheritdoc />
    protected override IBlobStore CreateStore() => S3TestServer.CreateStore(Container.Value);
}
