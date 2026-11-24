// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Multipart;

/// <summary>
/// 分片上传的单条便捷方法：都转调批量方法
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class BlobMultipartStoreExtensions
{
    /// <summary>
    /// 放弃一个上传并丢弃已上传的分片
    /// </summary>
    /// <remarks>
    /// 上传不存在也成功（幂等）。要放弃多个上传时调批量的 <see cref="IBlobMultipartStore.AbortAsync"/>，不要循环调本方法
    /// </remarks>
    public static async ValueTask<Result> AbortAsync(this IBlobMultipartStore store, BlobMultipartUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(upload);
        var results = await store.AbortAsync([upload], cancellationToken).ConfigureAwait(false);
        return results[0];
    }
}
