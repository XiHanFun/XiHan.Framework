// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Bot.WeCom.Models;

namespace XiHan.Framework.Bot.WeCom.Tests.Models;

/// <summary>
/// <see cref="WeComResultInfoDto"/> 响应解析契约测试
/// </summary>
/// <remarks>
/// 这是企业微信所有接口的统一响应载体，机器人靠 errcode/errmsg 判成败、靠 media_id 拿素材，
/// 字段名解析错会让上传结果整体丢失但不报错，因此按官方真实报文做解析验证。
/// </remarks>
public class WeComResultInfoDtoTests
{
    /// <summary>
    /// 上传成功报文的全部字段可被正确解析
    /// </summary>
    [Fact]
    public void Deserialize_FromUploadSuccessPayload_MapsAllFields()
    {
        const string Payload = """{"errcode":0,"errmsg":"ok","type":"file","media_id":"3a8asd892asd8asd","created_at":"1380000000"}""";

        var result = JsonSerializer.Deserialize<WeComResultInfoDto>(Payload);

        Assert.NotNull(result);
        Assert.Equal(0, result!.ErrCode);
        Assert.Equal("ok", result.ErrMsg);
        Assert.Equal("file", result.Type);
        Assert.Equal("3a8asd892asd8asd", result.MediaId);
        Assert.Equal("1380000000", result.CreatedAt);
    }

    /// <summary>
    /// 失败报文只带 errcode/errmsg 时其余字段保持默认空串
    /// </summary>
    [Fact]
    public void Deserialize_FromErrorPayload_KeepsOtherFieldsEmpty()
    {
        const string Payload = """{"errcode":93000,"errmsg":"invalid webhook url"}""";

        var result = JsonSerializer.Deserialize<WeComResultInfoDto>(Payload);

        Assert.NotNull(result);
        Assert.Equal(93000, result!.ErrCode);
        Assert.Equal("invalid webhook url", result.ErrMsg);
        Assert.Equal(string.Empty, result.Type);
        Assert.Equal(string.Empty, result.MediaId);
        Assert.Equal(string.Empty, result.CreatedAt);
    }

    /// <summary>
    /// 序列化时使用协议字段名而非 CLR 属性名
    /// </summary>
    [Fact]
    public void Serialize_UsesProtocolFieldNames()
    {
        var dto = new WeComResultInfoDto
        {
            ErrCode = 0,
            ErrMsg = "ok",
            Type = "voice",
            MediaId = "MEDIA",
            CreatedAt = "1380000000"
        };

        var node = JsonNode.Parse(JsonSerializer.Serialize(dto));

        Assert.NotNull(node);
        Assert.Equal(0, node!["errcode"]!.GetValue<int>());
        Assert.Equal("ok", node["errmsg"]!.GetValue<string>());
        Assert.Equal("voice", node["type"]!.GetValue<string>());
        Assert.Equal("MEDIA", node["media_id"]!.GetValue<string>());
        Assert.Equal("1380000000", node["created_at"]!.GetValue<string>());
    }

    /// <summary>
    /// 默认实例的字符串字段为空串而非 null
    /// </summary>
    /// <remarks>
    /// 上传成功分支会把 Type/MediaId 直接搬进结果 DTO，默认空串保证不会往外传 null。
    /// </remarks>
    [Fact]
    public void Defaults_AreEmptyStringsAndZeroCode()
    {
        var dto = new WeComResultInfoDto();

        Assert.Equal(0, dto.ErrCode);
        Assert.Equal(string.Empty, dto.ErrMsg);
        Assert.Equal(string.Empty, dto.Type);
        Assert.Equal(string.Empty, dto.MediaId);
        Assert.Equal(string.Empty, dto.CreatedAt);
    }

    /// <summary>
    /// 作为 record 具备值相等语义
    /// </summary>
    [Fact]
    public void Records_WithSameValues_AreEqual()
    {
        var left = new WeComResultInfoDto { ErrCode = 0, ErrMsg = "ok", MediaId = "M" };
        var right = new WeComResultInfoDto { ErrCode = 0, ErrMsg = "ok", MediaId = "M" };
        var other = new WeComResultInfoDto { ErrCode = 1, ErrMsg = "ok", MediaId = "M" };

        Assert.Equal(left, right);
        Assert.NotEqual(left, other);
    }
}
