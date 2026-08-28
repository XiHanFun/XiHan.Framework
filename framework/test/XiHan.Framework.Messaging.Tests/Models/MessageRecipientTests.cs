// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Messaging.Models;

namespace XiHan.Framework.Messaging.Tests.Models;

/// <summary>
/// 消息接收人测试
/// </summary>
/// <remarks>
/// 接收人只有三个字段，但 <c>Address</c> 是调度器回填发送结果的唯一依据，
/// 其「默认空串而非 null」的语义必须锁死，否则调度器的空串判定会退化。
/// </remarks>
public class MessageRecipientTests
{
    /// <summary>
    /// 新建接收人使用文档约定的默认值
    /// </summary>
    [Fact]
    public void Constructor_Default_UsesDocumentedDefaults()
    {
        var recipient = new MessageRecipient();

        Assert.Null(recipient.ReceiverId);
        Assert.Equal(string.Empty, recipient.Address);
        Assert.Null(recipient.DisplayName);
    }

    /// <summary>
    /// 全部字段经 JSON 往返后保持不变，且字段名与属性名一致
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesAllFields()
    {
        var recipient = new MessageRecipient
        {
            ReceiverId = "u-1",
            Address = "a@x.com",
            DisplayName = "甲"
        };

        var json = JsonSerializer.Serialize(recipient);
        var restored = JsonSerializer.Deserialize<MessageRecipient>(json);

        Assert.Contains("\"ReceiverId\"", json);
        Assert.Contains("\"Address\"", json);
        Assert.Contains("\"DisplayName\"", json);
        Assert.NotNull(restored);
        Assert.Equal("u-1", restored.ReceiverId);
        Assert.Equal("a@x.com", restored.Address);
        Assert.Equal("甲", restored.DisplayName);
    }

    /// <summary>
    /// 接收人是普通可变类，采用引用相等而非值相等
    /// </summary>
    [Fact]
    public void Recipient_WithIdenticalContent_UsesReferenceEquality()
    {
        var left = new MessageRecipient { Address = "a@x.com" };
        var right = new MessageRecipient { Address = "a@x.com" };

        Assert.False(left.Equals(right));
        Assert.NotSame(left, right);
    }
}
