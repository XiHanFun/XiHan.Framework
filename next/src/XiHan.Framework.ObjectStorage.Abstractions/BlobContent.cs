// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions;

/// <summary>
/// 读取到的对象：元数据与内容流；释放时关闭流。
/// </summary>
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
    /// 元数据。
    /// </summary>
    public BlobMetadata Metadata { get; }

    /// <summary>
    /// 内容流，从头开始。
    /// </summary>
    public Stream Content { get; }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
