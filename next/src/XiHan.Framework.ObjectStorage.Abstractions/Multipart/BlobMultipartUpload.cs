// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Multipart;

/// <summary>
/// 一次进行中的分片上传。应用要跨请求续传时持久化这四项，之后原样构造回来交给 <see cref="IBlobMultipartStore"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobMultipartUpload
{
    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="container">容器。</param>
    /// <param name="key">完成后对象的键。</param>
    /// <param name="uploadId">Provider 给出的上传标识（不透明，1 到 1024 个可见字符）。</param>
    /// <param name="overwrite">完成时对象已存在是否覆盖（发起时的 <c>BlobPutOptions.Overwrite</c>）。</param>
    public BlobMultipartUpload(ContainerName container, BlobKey key, string uploadId, bool overwrite = true)
    {
        ArgumentNullException.ThrowIfNull(uploadId);
        if (uploadId.Length is 0 or > 1024 || uploadId.Any(static ch => char.IsWhiteSpace(ch) || char.IsControl(ch)))
        {
            throw new ArgumentException("The upload ID must be 1 to 1024 visible characters.", nameof(uploadId));
        }

        Container = container;
        Key = key;
        UploadId = uploadId;
        Overwrite = overwrite;
    }

    /// <summary>
    /// 容器。
    /// </summary>
    public ContainerName Container { get; }

    /// <summary>
    /// 完成后对象的键。
    /// </summary>
    public BlobKey Key { get; }

    /// <summary>
    /// Provider 给出的上传标识。
    /// </summary>
    public string UploadId { get; }

    /// <summary>
    /// 完成时对象已存在是否覆盖。
    /// </summary>
    public bool Overwrite { get; }
}
