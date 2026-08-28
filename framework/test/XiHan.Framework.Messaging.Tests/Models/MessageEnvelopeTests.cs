// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Messaging.Models;

namespace XiHan.Framework.Messaging.Tests.Models;

/// <summary>
/// 消息信封测试
/// </summary>
/// <remarks>
/// 信封是跨进程/跨模块传递的消息契约，默认值与 JSON 字段名都会被外部依赖，
/// 因此这里锁死默认值语义与序列化往返，而不是只验证属性可读写。
/// </remarks>
public class MessageEnvelopeTests
{
    /// <summary>
    /// 新建信封使用文档约定的默认值
    /// </summary>
    [Fact]
    public void Constructor_Default_UsesDocumentedDefaults()
    {
        var envelope = new MessageEnvelope();

        Assert.Equal("default", envelope.Channel);
        Assert.Equal(string.Empty, envelope.Subject);
        Assert.Null(envelope.TenantId);
        Assert.Null(envelope.SenderId);
        Assert.Null(envelope.Content);
        Assert.Null(envelope.TemplateCode);
        Assert.Null(envelope.ScheduledTime);
        Assert.Null(envelope.ExpireTime);
        Assert.Null(envelope.CorrelationId);
        Assert.NotNull(envelope.TemplateParams);
        Assert.Empty(envelope.TemplateParams);
        Assert.NotNull(envelope.Metadata);
        Assert.Empty(envelope.Metadata);
        Assert.NotNull(envelope.Recipients);
        Assert.Empty(envelope.Recipients);
    }

    /// <summary>
    /// 默认消息标识是无分隔符的 32 位 GUID
    /// </summary>
    /// <remarks>
    /// 调度器会把该值回填到发送结果里，外部按定长字符串存储，格式漂移会直接影响持久化。
    /// </remarks>
    [Fact]
    public void MessageId_Default_IsCompactGuid()
    {
        var envelope = new MessageEnvelope();

        Assert.Equal(32, envelope.MessageId.Length);
        Assert.True(Guid.TryParseExact(envelope.MessageId, "N", out _));
    }

    /// <summary>
    /// 不同信封实例的默认消息标识互不相同
    /// </summary>
    [Fact]
    public void MessageId_AcrossInstances_IsUnique()
    {
        var ids = new HashSet<string>();

        for (var index = 0; index < 200; index++)
        {
            Assert.True(ids.Add(new MessageEnvelope().MessageId));
        }

        Assert.Equal(200, ids.Count);
    }

    /// <summary>
    /// 信封是普通可变类，采用引用相等而非值相等
    /// </summary>
    /// <remarks>
    /// 调度器会就地改写发送结果，若把信封当值对象放进集合去重会出现语义错配，这里显式钉死。
    /// </remarks>
    [Fact]
    public void Envelope_WithIdenticalContent_UsesReferenceEquality()
    {
        var left = new MessageEnvelope { MessageId = "same-id", Channel = "email", Subject = "标题" };
        var right = new MessageEnvelope { MessageId = "same-id", Channel = "email", Subject = "标题" };

        Assert.False(left.Equals(right));
        Assert.NotSame(left, right);
        Assert.True(left.Equals(left));
    }

    /// <summary>
    /// 标量字段经 JSON 往返后保持不变
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesScalarFields()
    {
        var envelope = new MessageEnvelope
        {
            MessageId = "msg-1",
            Channel = "email",
            TenantId = "tenant-1",
            SenderId = "sender-1",
            Subject = "主题",
            Content = "正文",
            TemplateCode = "TPL_001",
            ScheduledTime = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(8)),
            ExpireTime = new DateTimeOffset(2026, 1, 3, 3, 4, 5, TimeSpan.Zero),
            CorrelationId = "trace-1"
        };

        var restored = JsonSerializer.Deserialize<MessageEnvelope>(JsonSerializer.Serialize(envelope));

        Assert.NotNull(restored);
        Assert.Equal("msg-1", restored.MessageId);
        Assert.Equal("email", restored.Channel);
        Assert.Equal("tenant-1", restored.TenantId);
        Assert.Equal("sender-1", restored.SenderId);
        Assert.Equal("主题", restored.Subject);
        Assert.Equal("正文", restored.Content);
        Assert.Equal("TPL_001", restored.TemplateCode);
        Assert.Equal(envelope.ScheduledTime, restored.ScheduledTime);
        Assert.Equal(envelope.ExpireTime, restored.ExpireTime);
        Assert.Equal("trace-1", restored.CorrelationId);
    }

    /// <summary>
    /// 模板参数与元数据允许空值，且经 JSON 往返后空值不会退化成空串
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesDictionaryEntriesIncludingNullValues()
    {
        var envelope = new MessageEnvelope();
        envelope.TemplateParams["code"] = "123456";
        envelope.TemplateParams["nickname"] = null;
        envelope.Metadata["priority"] = "high";

        var restored = JsonSerializer.Deserialize<MessageEnvelope>(JsonSerializer.Serialize(envelope));

        Assert.NotNull(restored);
        Assert.Equal("123456", restored.TemplateParams["code"]);
        Assert.Null(restored.TemplateParams["nickname"]);
        Assert.Equal(2, restored.TemplateParams.Count);
        Assert.Equal("high", restored.Metadata["priority"]);
        Assert.Single(restored.Metadata);
    }

    /// <summary>
    /// 接收人集合经 JSON 往返后顺序与内容保持不变
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesRecipientsInOrder()
    {
        var envelope = new MessageEnvelope
        {
            Recipients =
            [
                new MessageRecipient { ReceiverId = "u1", Address = "a@x.com", DisplayName = "甲" },
                new MessageRecipient { Address = "b@x.com" }
            ]
        };

        var restored = JsonSerializer.Deserialize<MessageEnvelope>(JsonSerializer.Serialize(envelope));

        Assert.NotNull(restored);
        Assert.Equal(2, restored.Recipients.Count);
        Assert.Equal("u1", restored.Recipients[0].ReceiverId);
        Assert.Equal("a@x.com", restored.Recipients[0].Address);
        Assert.Equal("甲", restored.Recipients[0].DisplayName);
        Assert.Equal("b@x.com", restored.Recipients[1].Address);
        Assert.Null(restored.Recipients[1].ReceiverId);
        Assert.Null(restored.Recipients[1].DisplayName);
    }

    /// <summary>
    /// 默认序列化选项下字段名与属性名一致
    /// </summary>
    /// <remarks>
    /// 信封不带任何 JsonPropertyName 特性，一旦有人给属性改名或加驼峰策略，跨端契约就断了。
    /// </remarks>
    [Fact]
    public void Serialize_WithDefaultOptions_KeepsPropertyNames()
    {
        var json = JsonSerializer.Serialize(new MessageEnvelope());

        Assert.Contains("\"MessageId\"", json);
        Assert.Contains("\"Channel\"", json);
        Assert.Contains("\"Subject\"", json);
        Assert.Contains("\"TemplateParams\"", json);
        Assert.Contains("\"Metadata\"", json);
        Assert.Contains("\"Recipients\"", json);
    }
}
