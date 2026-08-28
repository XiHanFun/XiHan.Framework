// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Messaging.Models;

namespace XiHan.Framework.Messaging.Tests.Models;

/// <summary>
/// 消息发送结果测试
/// </summary>
/// <remarks>
/// 最关键的一条：默认构造出来的结果必须是「失败」。发送器实现常常只在成功分支显式赋值 IsSuccess，
/// 一旦默认值翻成 true，所有未赋值的异常分支都会被当成发送成功。
/// </remarks>
public class MessageSendResultTests
{
    /// <summary>
    /// 新建结果默认是失败，且标识字段为空串而非 null
    /// </summary>
    [Fact]
    public void Constructor_Default_IsFailureWithEmptyIdentity()
    {
        var result = new MessageSendResult();

        Assert.False(result.IsSuccess);
        Assert.Equal(string.Empty, result.MessageId);
        Assert.Equal(string.Empty, result.Channel);
        Assert.Equal(string.Empty, result.RecipientAddress);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.ProviderMessageId);
    }

    /// <summary>
    /// 分发时间默认取当前 UTC 时刻
    /// </summary>
    [Fact]
    public void DispatchedAt_Default_IsCurrentUtcInstant()
    {
        var before = DateTimeOffset.UtcNow;
        var result = new MessageSendResult();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(result.DispatchedAt, before, after);
        Assert.Equal(TimeSpan.Zero, result.DispatchedAt.Offset);
    }

    /// <summary>
    /// 全部字段经 JSON 往返后保持不变
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesAllFields()
    {
        var result = new MessageSendResult
        {
            MessageId = "msg-1",
            Channel = "email",
            RecipientAddress = "a@x.com",
            IsSuccess = true,
            ErrorMessage = "无",
            ProviderMessageId = "p-1",
            DispatchedAt = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.FromHours(8))
        };

        var restored = JsonSerializer.Deserialize<MessageSendResult>(JsonSerializer.Serialize(result));

        Assert.NotNull(restored);
        Assert.Equal("msg-1", restored.MessageId);
        Assert.Equal("email", restored.Channel);
        Assert.Equal("a@x.com", restored.RecipientAddress);
        Assert.True(restored.IsSuccess);
        Assert.Equal("无", restored.ErrorMessage);
        Assert.Equal("p-1", restored.ProviderMessageId);
        Assert.Equal(result.DispatchedAt, restored.DispatchedAt);
    }

    /// <summary>
    /// 默认序列化选项下字段名与属性名一致
    /// </summary>
    [Fact]
    public void Serialize_WithDefaultOptions_KeepsPropertyNames()
    {
        var json = JsonSerializer.Serialize(new MessageSendResult());

        Assert.Contains("\"MessageId\"", json);
        Assert.Contains("\"Channel\"", json);
        Assert.Contains("\"RecipientAddress\"", json);
        Assert.Contains("\"IsSuccess\"", json);
        Assert.Contains("\"DispatchedAt\"", json);
    }
}
