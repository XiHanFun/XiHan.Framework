// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.ObjectStorage.Models;

namespace XiHan.Framework.ObjectStorage.Tests.Models;

/// <summary>
/// 文件元数据契约测试
/// </summary>
/// <remarks>
/// FileMetadata 会被上层直接序列化后回传前端，字段名与可空性是对外契约；
/// 默认 System.Text.Json 不改属性名，这里做一次往返把字段名和取值语义一起锁死。
/// </remarks>
public class FileMetadataTests
{
    /// <summary>
    /// 新建实例除数值与布尔外全部为空
    /// </summary>
    [Fact]
    public void Defaults_AreNullExceptValueTypes()
    {
        var metadata = new FileMetadata();

        Assert.Null(metadata.Name);
        Assert.Null(metadata.Path);
        Assert.Equal(0L, metadata.Size);
        Assert.Null(metadata.ContentType);
        Assert.Null(metadata.LastModified);
        Assert.Null(metadata.ETag);
        Assert.False(metadata.IsDirectory);
        Assert.Null(metadata.Url);
        Assert.Null(metadata.Metadata);
    }

    /// <summary>
    /// JSON 序列化使用与属性同名的 Pascal 字段名
    /// </summary>
    [Fact]
    public void Serialize_UsesPascalCasePropertyNames()
    {
        var json = JsonSerializer.Serialize(new FileMetadata { Name = "a.png", Size = 12L });

        Assert.Contains("\"Name\"", json);
        Assert.Contains("\"Size\"", json);
        Assert.Contains("\"IsDirectory\"", json);
        Assert.Contains("\"ContentType\"", json);
    }

    /// <summary>
    /// 完整字段的 JSON 往返不丢信息
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesAllFields()
    {
        var source = new FileMetadata
        {
            Name = "a.png",
            Path = "img/a.png",
            Size = 12L,
            ContentType = "image/png",
            LastModified = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            ETag = "5d41402abc4b2a76b9719d911017c592",
            IsDirectory = false,
            Url = "/uploads/img/a.png",
            Metadata = new Dictionary<string, string> { ["owner"] = "tenant-1" }
        };

        var restored = JsonSerializer.Deserialize<FileMetadata>(JsonSerializer.Serialize(source));

        Assert.NotNull(restored);
        Assert.Equal(source.Name, restored.Name);
        Assert.Equal(source.Path, restored.Path);
        Assert.Equal(source.Size, restored.Size);
        Assert.Equal(source.ContentType, restored.ContentType);
        Assert.Equal(source.LastModified, restored.LastModified);
        Assert.Equal(source.ETag, restored.ETag);
        Assert.Equal(source.IsDirectory, restored.IsDirectory);
        Assert.Equal(source.Url, restored.Url);
        Assert.NotNull(restored.Metadata);
        Assert.Equal("tenant-1", restored.Metadata["owner"]);
    }

    /// <summary>
    /// 未赋值的可空字段往返后仍为空，不会被填成空串或默认时间
    /// </summary>
    [Fact]
    public void JsonRoundTrip_KeepsNullablesNull()
    {
        var restored = JsonSerializer.Deserialize<FileMetadata>(JsonSerializer.Serialize(new FileMetadata()));

        Assert.NotNull(restored);
        Assert.Null(restored.Name);
        Assert.Null(restored.LastModified);
        Assert.Null(restored.Metadata);
    }

    /// <summary>
    /// FileMetadata 是引用语义类型，字段相同的两个实例并不相等
    /// </summary>
    /// <remarks>
    /// 上层若按元数据去重必须自己写比较器，不能依赖记录类型的值相等。
    /// </remarks>
    [Fact]
    public void Equality_IsReferenceBased()
    {
        var left = new FileMetadata { Path = "img/a.png" };
        var right = new FileMetadata { Path = "img/a.png" };

        Assert.NotEqual(left, right);
        Assert.Equal(left, left);
    }
}
