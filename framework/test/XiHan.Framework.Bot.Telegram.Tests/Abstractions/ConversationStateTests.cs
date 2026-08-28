// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.Telegram.Abstractions;

namespace XiHan.Framework.Bot.Telegram.Tests.Abstractions;

/// <summary>
/// <see cref="ConversationState"/> 会话状态测试
/// </summary>
/// <remarks>
/// Step 默认空串（而不是 null）很关键：分发器用 <c>string.IsNullOrWhiteSpace(state.Step)</c> 判定「有没有活跃状态」，
/// 空串会被当作无状态，等价于「新建但没填步骤的状态不会劫持用户的下一条消息」。
/// </remarks>
public class ConversationStateTests
{
    /// <summary>
    /// 新建状态默认步骤为空串、无上下文数据
    /// </summary>
    [Fact]
    public void Defaults_StepIsEmptyAndPayloadIsNull()
    {
        var state = new ConversationState();

        Assert.Equal(string.Empty, state.Step);
        Assert.Null(state.Payload);
    }

    /// <summary>
    /// 创建时间默认取当前 UTC 时间
    /// </summary>
    [Fact]
    public void Defaults_CreateTimeIsCurrentUtcTime()
    {
        var before = DateTimeOffset.UtcNow;
        var state = new ConversationState();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(state.CreateTime, before, after);
        Assert.Equal(TimeSpan.Zero, state.CreateTime.Offset);
    }

    /// <summary>
    /// 属性可写并原样读回
    /// </summary>
    [Fact]
    public void Properties_AreMutableAndRoundTrip()
    {
        var createTime = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var state = new ConversationState
        {
            Step = "awaiting_amount",
            Payload = """{"orderId":"A-1"}""",
            CreateTime = createTime
        };

        Assert.Equal("awaiting_amount", state.Step);
        Assert.Equal("""{"orderId":"A-1"}""", state.Payload);
        Assert.Equal(createTime, state.CreateTime);
    }

    /// <summary>
    /// JSON 往返保持字段名与取值（分布式实现会把状态序列化进缓存）
    /// </summary>
    [Fact]
    public void JsonRoundTrip_KeepsFieldNamesAndValues()
    {
        var state = new ConversationState
        {
            Step = "awaiting_amount",
            Payload = """{"orderId":"A-1"}""",
            CreateTime = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize(state);

        Assert.Contains("\"Step\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Payload\"", json, StringComparison.Ordinal);
        Assert.Contains("\"CreateTime\"", json, StringComparison.Ordinal);

        var restored = JsonSerializer.Deserialize<ConversationState>(json);

        Assert.NotNull(restored);
        Assert.Equal(state.Step, restored!.Step);
        Assert.Equal(state.Payload, restored.Payload);
        Assert.Equal(state.CreateTime, restored.CreateTime);
    }
}
