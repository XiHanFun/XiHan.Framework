// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Signing;

/// <summary>
/// 预签名地址（能力 <c>objectstorage.presign@1</c>，RFC-0012）：签出一个限时的地址，客户端不带凭据直接下载、上传或上传分片。
/// 原生支持的存储（S3）由存储签名、客户端直连存储；不支持的存储（文件系统）由宿主的签名端点代为收发。签名是本地计算，不检查对象是否存在。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IBlobUrlSigner
{
    /// <summary>
    /// 签出地址；分片上传的地址（<see cref="BlobUrlRequest.Upload"/>）要求其容器与键就是 <paramref name="container"/> 与 <paramref name="key"/>。
    /// </summary>
    ValueTask<Result<BlobSignedUrl>> SignAsync(ContainerName container, BlobKey key, BlobUrlRequest request, CancellationToken cancellationToken);
}
