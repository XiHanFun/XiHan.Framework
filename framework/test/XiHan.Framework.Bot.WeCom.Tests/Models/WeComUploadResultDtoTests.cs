// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Bot.WeCom.Models;

namespace XiHan.Framework.Bot.WeCom.Tests.Models;

/// <summary>
/// <see cref="WeComUploadResultDto"/> 上传结果模型测试
/// </summary>
/// <remarks>
/// 这是框架自己回给调用方的结果，不是企业微信协议体，所以刻意没有标 JsonPropertyName；
/// 这里锁死「默认空串 + 使用 CLR 属性名」这两点，避免被误当协议模型改成蛇形命名。
/// </remarks>
public class WeComUploadResultDtoTests
{
    /// <summary>
    /// 默认实例三个字段均为空串
    /// </summary>
    [Fact]
    public void Defaults_AreEmptyStrings()
    {
        var dto = new WeComUploadResultDto();

        Assert.Equal(string.Empty, dto.Message);
        Assert.Equal(string.Empty, dto.MediaId);
        Assert.Equal(string.Empty, dto.Type);
    }

    /// <summary>
    /// 序列化使用 CLR 属性名，不做蛇形转换
    /// </summary>
    [Fact]
    public void Serialize_UsesClrPropertyNames()
    {
        var dto = new WeComUploadResultDto
        {
            Message = "上传成功；",
            MediaId = "MEDIA",
            Type = "file"
        };

        var node = JsonNode.Parse(JsonSerializer.Serialize(dto));

        Assert.NotNull(node);
        Assert.Equal("上传成功；", node!["Message"]!.GetValue<string>());
        Assert.Equal("MEDIA", node["MediaId"]!.GetValue<string>());
        Assert.Equal("file", node["Type"]!.GetValue<string>());
    }

    /// <summary>
    /// 往返序列化保持三个字段值不变
    /// </summary>
    [Fact]
    public void RoundTrip_PreservesAllValues()
    {
        var dto = new WeComUploadResultDto
        {
            Message = "上传成功；",
            MediaId = "MEDIA",
            Type = "voice"
        };

        var restored = JsonSerializer.Deserialize<WeComUploadResultDto>(JsonSerializer.Serialize(dto));

        Assert.NotNull(restored);
        Assert.Equal(dto.Message, restored!.Message);
        Assert.Equal(dto.MediaId, restored.MediaId);
        Assert.Equal(dto.Type, restored.Type);
    }
}
