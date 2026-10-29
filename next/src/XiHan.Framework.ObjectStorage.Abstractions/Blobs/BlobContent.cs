// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Blobs;

/// <summary>
/// 读取到的对象。
/// </summary>
/// <remarks>
/// 元数据与内容流；释放时关闭流。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class BlobContent : IAsyncDisposable
{
    /// <summary>
    /// 创建。
    /// </summary>
    public BlobContent(BlobMetadata metadata, Stream content)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>
    /// 创建范围读取的结果：<paramref name="content"/> 只含 <paramref name="range"/> 的字节，<see cref="BlobMetadata.Length"/> 仍是对象总长。
    /// </summary>
    public BlobContent(BlobMetadata metadata, Stream content, BlobContentRange range)
        : this(metadata, content)
    {
        Range = range ?? throw new ArgumentNullException(nameof(range));
    }

    /// <summary>
    /// 范围读取时实际读到的范围。
    /// </summary>
    /// <remarks>
    /// 整对象读取时为 <see langword="null"/>。
    /// </remarks>
    public BlobContentRange? Range { get; }

    /// <summary>
    /// 元数据。
    /// </summary>
    public BlobMetadata Metadata { get; }

    /// <summary>
    /// 内容流，从头开始。
    /// </summary>
    /// <remarks>
    /// 范围读取时只含该范围的字节。
    /// </remarks>
    public Stream Content { get; }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
