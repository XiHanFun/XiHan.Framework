// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Blobs;

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 写入选项的内容类型与自定义元数据有文法与上限（ADR-0214）
/// </summary>
/// <remarks>
/// 内容类型与元数据会进 S3 的请求头与文件系统的元数据文件：换行能拆出别的头，不限大小的元数据能把一个对象的元数据撑到任意大
/// </remarks>
public sealed class BlobPutOptionsTests
{
    [Fact]
    public void ContentType_MustBeAMediaTypeOfAtMost255Characters()
    {
        foreach (var valid in new[] { "text/plain", "image/svg+xml", "application/vnd.ms-excel", "text/plain; charset=utf-8", "multipart/form-data;boundary=\"a b\"", "*/*" })
        {
            Assert.True(BlobPutOptions.IsValidContentType(valid), valid);
            Assert.Equal(valid, new BlobPutOptions { ContentType = valid }.ContentType);
        }

        foreach (var invalid in new[] { "text/plain\r\nX-Injected: 1", "text/plain\n", "plain", "/plain", "text/", "text plain/x", "text/plain\t; a=b", "文本/plain", "text/" + new string('x', 251) })
        {
            Assert.False(BlobPutOptions.IsValidContentType(invalid), invalid);
            Assert.Throws<ArgumentException>(() => new BlobPutOptions { ContentType = invalid });
        }

        Assert.True(BlobPutOptions.IsValidContentType("text/" + new string('x', 250)));
        Assert.Equal(255, BlobPutOptions.MaxContentTypeLength);
    }

    [Fact]
    public void MetadataValues_HaveNoControlCharacters_AndTheTotalIsBounded()
    {
        Assert.Throws<ArgumentException>(static () => new BlobPutOptions { Metadata = new Dictionary<string, string> { ["owner"] = "alice\r\nx-amz-acl: public-read" } });
        Assert.Throws<ArgumentException>(static () => new BlobPutOptions { Metadata = new Dictionary<string, string> { ["owner"] = "a\u0000b" } });

        // 名与值按 UTF-8 字节合计：恰好 2048 可以，多一个字节不行；非 ASCII 的值照常可写
        Assert.Equal(2048, BlobPutOptions.MaxMetadataBytes);
        var full = new BlobPutOptions { Metadata = new Dictionary<string, string> { ["a"] = new string('x', 1000), ["b"] = new string('y', 1046) } };
        Assert.Equal(2, full.Metadata.Count);
        Assert.Throws<ArgumentException>(static () => new BlobPutOptions { Metadata = new Dictionary<string, string> { ["a"] = new string('x', 1000), ["b"] = new string('y', 1047) } });
        Assert.Throws<ArgumentException>(static () => new BlobPutOptions { Metadata = new Dictionary<string, string> { ["title"] = new string('中', 683) } });
        Assert.Equal("报告", new BlobPutOptions { Metadata = new Dictionary<string, string> { ["title"] = "报告" } }.Metadata["title"]);
    }
}
