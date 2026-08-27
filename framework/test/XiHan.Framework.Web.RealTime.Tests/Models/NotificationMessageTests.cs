// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Web.RealTime.Models;

namespace XiHan.Framework.Web.RealTime.Tests.Models;

/// <summary>
/// 通知消息模型测试
/// </summary>
/// <remarks>
/// 该模型是跨端契约：服务端序列化后由浏览器反序列化。用例覆盖默认值语义与 JSON 往返，
/// 并单独锁死 camelCase 下的字段名——<c>AddXiHanSignalRWithJson</c> 就是按 camelCase 配置协议的，
/// 字段名一旦漂移，前端会静默拿到 undefined。
/// </remarks>
public class NotificationMessageTests
{
    /// <summary>
    /// 默认生成的消息 ID 是可解析的 GUID 且互不重复
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_GeneratesUniqueParsableId()
    {
        var first = new NotificationMessage();
        var second = new NotificationMessage();

        Assert.True(Guid.TryParse(first.Id, out _));
        Assert.NotEqual(first.Id, second.Id);
    }

    /// <summary>
    /// 默认消息类型为 Info 且处于未读状态
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_UsesInfoTypeAndUnreadState()
    {
        var message = new NotificationMessage();

        Assert.Equal("Info", message.Type);
        Assert.False(message.IsRead);
    }

    /// <summary>
    /// 默认的可空字段全部为空
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_LeavesOptionalFieldsNull()
    {
        var message = new NotificationMessage();

        Assert.Null(message.SenderId);
        Assert.Null(message.ReceiverId);
        Assert.Null(message.Title);
        Assert.Null(message.Content);
        Assert.Null(message.Data);
    }

    /// <summary>
    /// 创建时间默认打的是 UTC 时间戳
    /// </summary>
    /// <remarks>
    /// 多端同步依赖时间可比较，本地时间会随部署时区漂移，因此 Kind 必须是 Utc。
    /// </remarks>
    [Fact]
    public void Constructor_ByDefault_StampsUtcCreatedTime()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var message = new NotificationMessage();
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.Equal(DateTimeKind.Utc, message.CreatedTime.Kind);
        Assert.InRange(message.CreatedTime, before, after);
    }

    /// <summary>
    /// JSON 往返保留全部标量字段
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesEveryScalarField()
    {
        var message = new NotificationMessage
        {
            Id = "msg-1",
            SenderId = "u1",
            ReceiverId = "u2",
            Type = "Warning",
            Title = "标题",
            Content = "正文内容",
            CreatedTime = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            IsRead = true
        };

        var restored = JsonSerializer.Deserialize<NotificationMessage>(JsonSerializer.Serialize(message));

        Assert.NotNull(restored);
        Assert.Equal("msg-1", restored.Id);
        Assert.Equal("u1", restored.SenderId);
        Assert.Equal("u2", restored.ReceiverId);
        Assert.Equal("Warning", restored.Type);
        Assert.Equal("标题", restored.Title);
        Assert.Equal("正文内容", restored.Content);
        Assert.Equal(message.CreatedTime, restored.CreatedTime);
        Assert.Equal(DateTimeKind.Utc, restored.CreatedTime.Kind);
        Assert.True(restored.IsRead);
    }

    /// <summary>
    /// JSON 往返保留空值字段的可空语义
    /// </summary>
    [Fact]
    public void JsonRoundTrip_KeepsOptionalFieldsNull()
    {
        var message = new NotificationMessage { Id = "msg-1" };

        var restored = JsonSerializer.Deserialize<NotificationMessage>(JsonSerializer.Serialize(message));

        Assert.NotNull(restored);
        Assert.Null(restored.SenderId);
        Assert.Null(restored.ReceiverId);
        Assert.Null(restored.Title);
        Assert.Null(restored.Content);
        Assert.Null(restored.Data);
    }

    /// <summary>
    /// 自由载荷字段在往返后以 JsonElement 形式保留原值
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesDataPayloadAsJsonElement()
    {
        var message = new NotificationMessage
        {
            Data = "任务已完成"
        };

        var restored = JsonSerializer.Deserialize<NotificationMessage>(JsonSerializer.Serialize(message));

        Assert.NotNull(restored);
        var payload = Assert.IsType<JsonElement>(restored.Data);
        Assert.Equal(JsonValueKind.String, payload.ValueKind);
        Assert.Equal("任务已完成", payload.GetString());
    }

    /// <summary>
    /// 默认序列化选项下字段名保持 PascalCase
    /// </summary>
    [Fact]
    public void Serialize_WithDefaultOptions_UsesPascalCasePropertyNames()
    {
        var message = new NotificationMessage { Id = "msg-1" };

        var json = JsonSerializer.Serialize(message);

        Assert.Contains("\"Id\":", json);
        Assert.Contains("\"SenderId\":", json);
        Assert.Contains("\"CreatedTime\":", json);
        Assert.Contains("\"IsRead\":", json);
    }

    /// <summary>
    /// camelCase 策略下的字段名与前端约定一致
    /// </summary>
    [Fact]
    public void Serialize_WithCamelCasePolicy_MatchesFrontendFieldNames()
    {
        var message = new NotificationMessage { Id = "msg-1" };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        var json = JsonSerializer.Serialize(message, options);

        Assert.Contains("\"id\":", json);
        Assert.Contains("\"senderId\":", json);
        Assert.Contains("\"receiverId\":", json);
        Assert.Contains("\"type\":", json);
        Assert.Contains("\"title\":", json);
        Assert.Contains("\"content\":", json);
        Assert.Contains("\"data\":", json);
        Assert.Contains("\"createdTime\":", json);
        Assert.Contains("\"isRead\":", json);
    }

    /// <summary>
    /// camelCase 策略下反序列化能吃回自己序列化出的载荷
    /// </summary>
    [Fact]
    public void JsonRoundTrip_WithCamelCasePolicy_IsSymmetric()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var message = new NotificationMessage
        {
            Id = "msg-1",
            SenderId = "u1",
            Type = "Error",
            Title = "标题",
            Content = "正文",
            IsRead = true
        };

        var restored = JsonSerializer.Deserialize<NotificationMessage>(
            JsonSerializer.Serialize(message, options), options);

        Assert.NotNull(restored);
        Assert.Equal("msg-1", restored.Id);
        Assert.Equal("u1", restored.SenderId);
        Assert.Equal("Error", restored.Type);
        Assert.Equal("标题", restored.Title);
        Assert.Equal("正文", restored.Content);
        Assert.True(restored.IsRead);
    }

    /// <summary>
    /// 全部字段可写，便于调用方按场景组装消息
    /// </summary>
    [Fact]
    public void Properties_AreMutable()
    {
        var createdTime = new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        var payload = new object();

        var message = new NotificationMessage
        {
            Id = "msg-2",
            SenderId = "sender",
            ReceiverId = "receiver",
            Type = "Success",
            Title = "标题",
            Content = "正文",
            Data = payload,
            CreatedTime = createdTime,
            IsRead = true
        };

        Assert.Equal("msg-2", message.Id);
        Assert.Equal("sender", message.SenderId);
        Assert.Equal("receiver", message.ReceiverId);
        Assert.Equal("Success", message.Type);
        Assert.Equal("标题", message.Title);
        Assert.Equal("正文", message.Content);
        Assert.Same(payload, message.Data);
        Assert.Equal(createdTime, message.CreatedTime);
        Assert.True(message.IsRead);
    }
}
