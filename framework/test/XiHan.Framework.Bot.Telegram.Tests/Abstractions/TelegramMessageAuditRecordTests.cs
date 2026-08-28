// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.Telegram.Abstractions;

namespace XiHan.Framework.Bot.Telegram.Tests.Abstractions;

/// <summary>
/// <see cref="TelegramMessageAuditRecord"/> 出站消息审计记录测试
/// </summary>
/// <remarks>
/// 审计记录会被应用层直接落库，字段名与可空性属于对外契约；
/// record 的值相等语义则决定了去重/比对逻辑能不能靠 == 工作。
/// </remarks>
public class TelegramMessageAuditRecordTests
{
    /// <summary>
    /// 新建记录的默认值：字符串必填项为空串、可空项为 null、默认判定为失败
    /// </summary>
    [Fact]
    public void Defaults_AreEmptyStringsNullsAndFailure()
    {
        var record = new TelegramMessageAuditRecord();

        Assert.Equal(string.Empty, record.BotName);
        Assert.Equal(0L, record.BotConfigId);
        Assert.Equal(0L, record.ChatId);
        Assert.Equal(string.Empty, record.ApiMethod);
        Assert.Equal(string.Empty, record.MessageType);
        Assert.Null(record.Content);
        Assert.Null(record.ParseMode);
        Assert.Null(record.TelegramMessageId);
        Assert.False(record.Success);
        Assert.Null(record.ErrorCode);
        Assert.Null(record.ErrorMessage);
        Assert.Equal(0L, record.ElapsedMs);
    }

    /// <summary>
    /// 发送时间默认取当前 UTC 时间
    /// </summary>
    [Fact]
    public void Defaults_SendTimeIsCurrentUtcTime()
    {
        var before = DateTimeOffset.UtcNow;
        var record = new TelegramMessageAuditRecord();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(record.SendTime, before, after);
    }

    /// <summary>
    /// 字段全部一致的两条记录按值相等
    /// </summary>
    [Fact]
    public void Equality_IsByValue()
    {
        var sendTime = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

        var left = CreateRecord(sendTime);
        var right = CreateRecord(sendTime);

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// with 表达式产生新实例且只改动指定字段
    /// </summary>
    [Fact]
    public void WithExpression_ChangesOnlyTargetedMember()
    {
        var sendTime = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var origin = CreateRecord(sendTime);

        var failed = origin with { Success = false, ErrorCode = 429, ErrorMessage = "Too Many Requests" };

        Assert.True(origin.Success);
        Assert.False(failed.Success);
        Assert.Equal(429, failed.ErrorCode);
        Assert.Equal("Too Many Requests", failed.ErrorMessage);
        Assert.Equal(origin.BotName, failed.BotName);
        Assert.Equal(origin.ChatId, failed.ChatId);
        Assert.NotEqual(origin, failed);
    }

    /// <summary>
    /// JSON 往返保持全部字段名与取值
    /// </summary>
    [Fact]
    public void JsonRoundTrip_KeepsAllFields()
    {
        var record = CreateRecord(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(record);

        Assert.Contains("\"BotName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"BotConfigId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ChatId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ApiMethod\"", json, StringComparison.Ordinal);
        Assert.Contains("\"MessageType\"", json, StringComparison.Ordinal);
        Assert.Contains("\"TelegramMessageId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ElapsedMs\"", json, StringComparison.Ordinal);
        Assert.Contains("\"SendTime\"", json, StringComparison.Ordinal);

        var restored = JsonSerializer.Deserialize<TelegramMessageAuditRecord>(json);

        Assert.NotNull(restored);
        Assert.Equal(record, restored);
    }

    /// <summary>
    /// 构造一条成功的审计记录
    /// </summary>
    /// <param name="sendTime">发送时间</param>
    /// <returns>审计记录</returns>
    private static TelegramMessageAuditRecord CreateRecord(DateTimeOffset sendTime)
    {
        return new TelegramMessageAuditRecord
        {
            BotName = "main-bot",
            BotConfigId = 7L,
            ChatId = -100123456L,
            ApiMethod = "sendMessage",
            MessageType = "text",
            Content = "你好",
            ParseMode = "None",
            TelegramMessageId = 99,
            Success = true,
            ErrorCode = null,
            ErrorMessage = null,
            ElapsedMs = 123L,
            SendTime = sendTime
        };
    }
}
