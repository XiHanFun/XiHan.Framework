// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using XiHan.Framework.Bot.Enums;

namespace XiHan.Framework.Bot.Tests.Enums;

/// <summary>
/// <see cref="BotResultCodes"/> 枚举测试
/// </summary>
/// <remarks>
/// 该枚举会随 <c>BotResult</c> 序列化成 int 落到日志与外部对接方，数值必须锁死。
/// </remarks>
public class BotResultCodesTests
{
    /// <summary>
    /// 枚举数值与 HTTP 语义一致且不漂移
    /// </summary>
    [Theory]
    [InlineData(BotResultCodes.Success, 200)]
    [InlineData(BotResultCodes.BadRequest, 400)]
    [InlineData(BotResultCodes.Failed, 500)]
    public void Values_AreStable(BotResultCodes code, int expected)
    {
        Assert.Equal(expected, (int)code);
    }

    /// <summary>
    /// 枚举成员数量固定为三个
    /// </summary>
    [Fact]
    public void Members_CountIsThree()
    {
        Assert.Equal(3, Enum.GetValues<BotResultCodes>().Length);
    }

    /// <summary>
    /// 每个成员都带中文描述
    /// </summary>
    [Theory]
    [InlineData(BotResultCodes.Success, "请求成功")]
    [InlineData(BotResultCodes.BadRequest, "请求错误")]
    [InlineData(BotResultCodes.Failed, "服务器内部错误")]
    public void Members_HaveDescription(BotResultCodes code, string expected)
    {
        var field = typeof(BotResultCodes).GetField(code.ToString(), BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(field);

        var description = field!.GetCustomAttribute<DescriptionAttribute>();
        Assert.NotNull(description);
        Assert.Equal(expected, description!.Description);
    }

    /// <summary>
    /// 默认序列化为数字而非名称
    /// </summary>
    [Fact]
    public void Serialization_UsesNumericValue()
    {
        var json = JsonSerializer.Serialize(BotResultCodes.Failed);

        Assert.Equal("500", json);
    }
}
