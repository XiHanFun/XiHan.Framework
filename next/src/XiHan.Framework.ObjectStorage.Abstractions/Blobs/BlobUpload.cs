// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Blobs;

/// <summary>
/// 一次写入：键、内容流与选项。流由调用方负责释放；Provider 从当前位置读到末尾。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobUpload
{
    /// <summary>
    /// 创建。
    /// </summary>
    public BlobUpload(BlobKey key, Stream content, BlobPutOptions options)
    {
        Key = key;
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        if (!content.CanRead)
        {
            throw new ArgumentException("The content stream must be readable.", nameof(content));
        }
    }

    /// <summary>
    /// 键。
    /// </summary>
    public BlobKey Key { get; }

    /// <summary>
    /// 内容流。
    /// </summary>
    public Stream Content { get; }

    /// <summary>
    /// 选项。
    /// </summary>
    public BlobPutOptions Options { get; }
}
