// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Hosting.AspNetCore.Tests.Fixtures;

/// <summary>
/// 记下被读走多少字节的只读流
/// </summary>
/// <remarks>
/// 只重写数组那个读取：<see cref="MemoryStream"/> 的派生类型上，其余读取（跨度、异步）都落到它
/// </remarks>
/// <param name="content">流的内容</param>
internal sealed class CountingStream(byte[] content) : MemoryStream(content, writable: false)
{
    /// <summary>
    /// 已经读走的字节数
    /// </summary>
    public long BytesRead { get; private set; }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = base.Read(buffer, offset, count);
        BytesRead += read;
        return read;
    }
}
