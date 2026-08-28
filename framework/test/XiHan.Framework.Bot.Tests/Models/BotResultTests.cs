// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Tests.Models;

/// <summary>
/// <see cref="BotResult"/> 测试
/// </summary>
/// <remarks>
/// IsSuccess 是只读派生属性，唯一依据是 Code == Success，任何"只改 Message 不改 Code"的写法都不该影响成败判定。
/// </remarks>
public class BotResultTests
{
    /// <summary>
    /// 默认实例即成功
    /// </summary>
    [Fact]
    public void Defaults_AreSuccess()
    {
        var result = new BotResult();

        Assert.Equal(BotResultCodes.Success, result.Code);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Message);
        Assert.Null(result.Data);
        Assert.Null(result.Provider);
    }

    /// <summary>
    /// 成功工厂方法带出数据与提供者
    /// </summary>
    [Fact]
    public void Success_CarriesDataAndProvider()
    {
        var payload = new { Id = 1 };

        var result = BotResult.Success(payload, "DingTalk");

        Assert.True(result.IsSuccess);
        Assert.Same(payload, result.Data);
        Assert.Equal("DingTalk", result.Provider);
        Assert.Null(result.Message);
    }

    /// <summary>
    /// 请求错误工厂方法产出 400 且不成功
    /// </summary>
    [Fact]
    public void BadRequest_ProducesFourHundred()
    {
        var result = BotResult.BadRequest("参数缺失", "Lark");

        Assert.Equal(BotResultCodes.BadRequest, result.Code);
        Assert.False(result.IsSuccess);
        Assert.Equal("参数缺失", result.Message);
        Assert.Equal("Lark", result.Provider);
    }

    /// <summary>
    /// 失败工厂方法产出 500 且不成功
    /// </summary>
    [Fact]
    public void Failed_ProducesFiveHundred()
    {
        var result = BotResult.Failed("网关超时", "WeCom");

        Assert.Equal(BotResultCodes.Failed, result.Code);
        Assert.False(result.IsSuccess);
        Assert.Equal("网关超时", result.Message);
        Assert.Equal("WeCom", result.Provider);
    }

    /// <summary>
    /// 工厂方法的参数均可省略
    /// </summary>
    [Fact]
    public void Factories_AllowOmittedArguments()
    {
        Assert.Null(BotResult.Success().Data);
        Assert.Null(BotResult.Success().Provider);
        Assert.Null(BotResult.BadRequest().Message);
        Assert.Null(BotResult.Failed().Message);
    }

    /// <summary>
    /// From 复制业务码、消息与数据，并用入参覆盖提供者
    /// </summary>
    [Fact]
    public void From_CopiesPayloadAndOverridesProvider()
    {
        var payload = new object();
        var source = new BotResult
        {
            Code = BotResultCodes.BadRequest,
            Message = "bad",
            Data = payload,
            Provider = "Origin"
        };

        var copy = BotResult.From(source, "Target");

        Assert.NotSame(source, copy);
        Assert.Equal(BotResultCodes.BadRequest, copy.Code);
        Assert.Equal("bad", copy.Message);
        Assert.Same(payload, copy.Data);
        Assert.Equal("Target", copy.Provider);
    }

    /// <summary>
    /// 只改 Message 不改 Code 时仍视为成功
    /// </summary>
    [Fact]
    public void IsSuccess_DependsOnlyOnCode()
    {
        var result = new BotResult { Message = "看起来像错误" };

        Assert.True(result.IsSuccess);

        result.Code = BotResultCodes.Failed;

        Assert.False(result.IsSuccess);
    }

    /// <summary>
    /// JSON 往返保留业务码、消息与提供者，且业务码是数字
    /// </summary>
    [Fact]
    public void JsonRoundTrip_KeepsContract()
    {
        var result = BotResult.Failed("boom", "Telegram");

        var json = JsonSerializer.Serialize(result);

        Assert.Contains("\"Code\":500", json);
        Assert.Contains("\"Provider\":\"Telegram\"", json);

        var restored = JsonSerializer.Deserialize<BotResult>(json);

        Assert.NotNull(restored);
        Assert.Equal(BotResultCodes.Failed, restored!.Code);
        Assert.Equal("boom", restored.Message);
        Assert.Equal("Telegram", restored.Provider);
        Assert.False(restored.IsSuccess);
    }
}
