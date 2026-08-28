// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.DingTalk.Models;

namespace XiHan.Framework.Bot.DingTalk.Tests.Models;

/// <summary>
/// 钉钉响应体模型测试
/// </summary>
/// <remarks>
/// 钉钉网关返回的是 <c>{"errcode":0,"errmsg":"ok"}</c> 这种全小写字段，
/// DingTalkBot 又用 <c>ErrCode == 0 || ErrMsg == "ok"</c> 判定成功，
/// 一旦反序列化没命中字段名，失败响应会被静默当成成功（ErrCode 落回默认 0），后果比抛异常更严重，
/// 所以按真实响应报文验证解析结果。
/// </remarks>
public class DingTalkResultInfoDtoTests
{
    /// <summary>
    /// 默认值：错误码 0、错误消息空串
    /// </summary>
    [Fact]
    public void Defaults_AreZeroAndEmpty()
    {
        var dto = new DingTalkResultInfoDto();

        Assert.Equal(0, dto.ErrCode);
        Assert.Equal(string.Empty, dto.ErrMsg);
    }

    /// <summary>
    /// 成功响应报文可正确解析
    /// </summary>
    [Fact]
    public void Deserialize_FromSuccessPayload_MapsBothFields()
    {
        var dto = JsonSerializer.Deserialize<DingTalkResultInfoDto>("{\"errcode\":0,\"errmsg\":\"ok\"}");

        Assert.NotNull(dto);
        Assert.Equal(0, dto.ErrCode);
        Assert.Equal("ok", dto.ErrMsg);
    }

    /// <summary>
    /// 失败响应报文可正确解析
    /// </summary>
    [Fact]
    public void Deserialize_FromFailurePayload_MapsBothFields()
    {
        var dto = JsonSerializer.Deserialize<DingTalkResultInfoDto>("{\"errcode\":310000,\"errmsg\":\"sign not match\"}");

        Assert.NotNull(dto);
        Assert.Equal(310000, dto.ErrCode);
        Assert.Equal("sign not match", dto.ErrMsg);
    }

    /// <summary>
    /// 响应缺少 errmsg 时保持空串而非 null
    /// </summary>
    [Fact]
    public void Deserialize_WhenErrMsgMissing_KeepsEmptyString()
    {
        var dto = JsonSerializer.Deserialize<DingTalkResultInfoDto>("{\"errcode\":400101}");

        Assert.NotNull(dto);
        Assert.Equal(400101, dto.ErrCode);
        Assert.Equal(string.Empty, dto.ErrMsg);
    }

    /// <summary>
    /// 序列化回写用的仍是协议字段名
    /// </summary>
    [Fact]
    public void Serialize_UsesProtocolFieldNames()
    {
        var json = JsonSerializer.Serialize(new DingTalkResultInfoDto { ErrCode = 400102, ErrMsg = "robot is disabled" });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(400102, root.GetProperty("errcode").GetInt32());
        Assert.Equal("robot is disabled", root.GetProperty("errmsg").GetString());
        Assert.Equal(2, root.EnumerateObject().Count());
    }

    /// <summary>
    /// 记录类型按值相等，便于断言与去重
    /// </summary>
    [Fact]
    public void Record_UsesValueEquality()
    {
        var left = new DingTalkResultInfoDto { ErrCode = 430101, ErrMsg = "unsafe link" };
        var right = new DingTalkResultInfoDto { ErrCode = 430101, ErrMsg = "unsafe link" };
        var other = new DingTalkResultInfoDto { ErrCode = 430102, ErrMsg = "unsafe link" };

        Assert.Equal(left, right);
        Assert.NotEqual(left, other);
    }
}
