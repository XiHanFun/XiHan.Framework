// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.ObjectStorage.Models;

namespace XiHan.Framework.ObjectStorage.Tests.Models;

/// <summary>
/// 文件上传结果契约测试
/// </summary>
/// <remarks>
/// Success 默认必须是 false：所有 Provider 的失败分支都只填 ErrorMessage 就直接返回，
/// 一旦默认值翻成 true，失败会被上层当成成功。
/// </remarks>
public class FileUploadResultTests
{
    /// <summary>
    /// 新建实例默认为「失败且无任何路径信息」
    /// </summary>
    [Fact]
    public void Defaults_AreUnsuccessfulAndEmpty()
    {
        var result = new FileUploadResult();

        Assert.False(result.Success);
        Assert.Null(result.Path);
        Assert.Null(result.FullPath);
        Assert.Null(result.Url);
        Assert.Equal(0L, result.FileSize);
        Assert.Null(result.ETag);
        Assert.Equal(0L, result.DurationMs);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Extra);
    }

    /// <summary>
    /// 成功结果的 JSON 往返不丢字段
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesScalarFields()
    {
        var source = new FileUploadResult
        {
            Success = true,
            Path = "img/a.png",
            FullPath = "/data/files/img/a.png",
            Url = "/uploads/img/a.png",
            FileSize = 2048L,
            ETag = "5d41402abc4b2a76b9719d911017c592",
            DurationMs = 37L,
            ErrorMessage = null
        };

        var restored = JsonSerializer.Deserialize<FileUploadResult>(JsonSerializer.Serialize(source));

        Assert.NotNull(restored);
        Assert.True(restored.Success);
        Assert.Equal(source.Path, restored.Path);
        Assert.Equal(source.FullPath, restored.FullPath);
        Assert.Equal(source.Url, restored.Url);
        Assert.Equal(source.FileSize, restored.FileSize);
        Assert.Equal(source.ETag, restored.ETag);
        Assert.Equal(source.DurationMs, restored.DurationMs);
        Assert.Null(restored.ErrorMessage);
    }

    /// <summary>
    /// 扩展信息字典能被序列化并在反序列化后保留键
    /// </summary>
    /// <remarks>
    /// Extra 的值类型是 object，反序列化后会变成 JsonElement 而不是原始 CLR 类型，
    /// 所以只断言键存在与原始 JSON 中的取值，不断言值的运行期类型。
    /// </remarks>
    [Fact]
    public void JsonRoundTrip_KeepsExtraKeys()
    {
        var source = new FileUploadResult
        {
            Success = true,
            Extra = new Dictionary<string, object> { ["region"] = "cn-hangzhou" }
        };

        var json = JsonSerializer.Serialize(source);
        var restored = JsonSerializer.Deserialize<FileUploadResult>(json);

        Assert.Contains("cn-hangzhou", json);
        Assert.NotNull(restored);
        Assert.NotNull(restored.Extra);
        Assert.True(restored.Extra.ContainsKey("region"));
    }

    /// <summary>
    /// 失败结果只带错误消息，不携带路径
    /// </summary>
    [Fact]
    public void FailureShape_CarriesOnlyErrorMessage()
    {
        var result = new FileUploadResult
        {
            Success = false,
            ErrorMessage = "File already exists"
        };

        Assert.False(result.Success);
        Assert.Equal("File already exists", result.ErrorMessage);
        Assert.Null(result.Path);
        Assert.Null(result.Url);
    }
}
