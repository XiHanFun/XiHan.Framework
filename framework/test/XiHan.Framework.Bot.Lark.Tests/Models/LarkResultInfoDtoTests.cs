// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.Lark.Enums;
using XiHan.Framework.Bot.Lark.Models;

namespace XiHan.Framework.Bot.Lark.Tests.Models;

/// <summary>
/// 飞书响应体 DTO 测试
/// </summary>
/// <remarks>
/// 这是飞书返回给我们的报文契约：字段名固定为 code / msg / data，且大小写敏感。
/// LarkBot 用 code == 0 或 msg == "success" 判定成功，其余走错误码枚举翻译，
/// 所以这里把「反序列化能落到属性上」和「错误码能对上枚举」一起验证。
/// </remarks>
public class LarkResultInfoDtoTests
{
    /// <summary>
    /// 新建实例的默认值代表「成功且无数据」
    /// </summary>
    [Fact]
    public void Defaults_WhenNewInstance_AreZeroCodeAndEmptyMessage()
    {
        var dto = new LarkResultInfoDto();

        Assert.Equal(0, dto.Code);
        Assert.Equal(string.Empty, dto.Msg);
        Assert.NotNull(dto.Data);
    }

    /// <summary>
    /// 成功报文可反序列化到属性
    /// </summary>
    [Fact]
    public void Deserialize_WhenSuccessPayload_MapsCodeAndMessage()
    {
        var dto = JsonSerializer.Deserialize<LarkResultInfoDto>("{\"code\":0,\"msg\":\"success\"}");

        Assert.NotNull(dto);
        Assert.Equal(0, dto.Code);
        Assert.Equal("success", dto.Msg);
    }

    /// <summary>
    /// 错误报文的 code 能与错误码枚举对齐
    /// </summary>
    [Theory]
    [InlineData(9499, LarkResultErrCodeEnum.BadRequest)]
    [InlineData(19021, LarkResultErrCodeEnum.SignMatchFail)]
    [InlineData(19022, LarkResultErrCodeEnum.IpNotAllowed)]
    [InlineData(19024, LarkResultErrCodeEnum.KeyWordsNotFound)]
    public void Deserialize_WhenErrorPayload_CodeMatchesErrCodeEnum(int code, LarkResultErrCodeEnum expected)
    {
        var dto = JsonSerializer.Deserialize<LarkResultInfoDto>("{\"code\":" + code + ",\"msg\":\"failed\"}");

        Assert.NotNull(dto);
        Assert.Equal((int)expected, dto.Code);
        Assert.Equal("failed", dto.Msg);
    }

    /// <summary>
    /// 缺字段的报文保留默认值而不是抛异常
    /// </summary>
    [Fact]
    public void Deserialize_WhenEmptyObject_KeepsDefaults()
    {
        var dto = JsonSerializer.Deserialize<LarkResultInfoDto>("{}");

        Assert.NotNull(dto);
        Assert.Equal(0, dto.Code);
        Assert.Equal(string.Empty, dto.Msg);
    }

    /// <summary>
    /// 字段名大小写敏感，不接受 PascalCase 报文
    /// </summary>
    /// <remarks>
    /// 飞书返回的是 snake/lower 风格；这里固化「默认序列化选项下不会误绑定」，
    /// 避免后续有人误以为可以用 PascalCase 构造测试数据。
    /// </remarks>
    [Fact]
    public void Deserialize_WhenPascalCasePayload_DoesNotBind()
    {
        var dto = JsonSerializer.Deserialize<LarkResultInfoDto>("{\"Code\":9499,\"Msg\":\"bad\"}");

        Assert.NotNull(dto);
        Assert.Equal(0, dto.Code);
        Assert.Equal(string.Empty, dto.Msg);
    }

    /// <summary>
    /// data 字段以 JsonElement 承载任意结构
    /// </summary>
    [Fact]
    public void Deserialize_WhenDataPresent_IsCapturedAsJsonElement()
    {
        var dto = JsonSerializer.Deserialize<LarkResultInfoDto>("{\"code\":0,\"msg\":\"success\",\"data\":{\"extra\":1}}");

        Assert.NotNull(dto);
        Assert.True(dto.Data is JsonElement);

        var element = (JsonElement)dto.Data;
        Assert.Equal(1, element.GetProperty("extra").GetInt32());
    }

    /// <summary>
    /// 序列化输出的字段名与飞书契约一致
    /// </summary>
    [Fact]
    public void Serialize_Always_UsesProtocolFieldNames()
    {
        var json = JsonSerializer.Serialize(new LarkResultInfoDto
        {
            Code = 19021,
            Msg = "sign match fail"
        });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(19021, root.GetProperty("code").GetInt32());
        Assert.Equal("sign match fail", root.GetProperty("msg").GetString());
        Assert.True(root.TryGetProperty("data", out _));
        Assert.Equal(3, root.EnumerateObject().Count());
    }
}
