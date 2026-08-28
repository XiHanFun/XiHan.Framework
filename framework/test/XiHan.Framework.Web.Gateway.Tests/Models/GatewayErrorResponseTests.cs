// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Web.Gateway.Models;

namespace XiHan.Framework.Web.Gateway.Tests.Models;

/// <summary>
/// 网关错误响应契约测试
/// </summary>
/// <remarks>
/// 这个类型是网关唯一的对外错误报文，字段名会直接出现在客户端解析代码里。
/// 网关异常中间件用 <see cref="JsonNamingPolicy.CamelCase"/> 序列化它，
/// 因此这里锁死的是「小驼峰后的线上字段名」而不是 C# 属性名。
/// </remarks>
public class GatewayErrorResponseTests
{
    /// <summary>
    /// 与网关异常中间件保持一致的序列化配置
    /// </summary>
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 新实例的可选字段为空、时间戳未赋值
    /// </summary>
    [Fact]
    public void NewInstance_HasNullDetailsAndDefaultTimestamp()
    {
        var response = new GatewayErrorResponse();

        Assert.Null(response.Details);
        Assert.Equal(default(DateTime), response.Timestamp);
    }

    /// <summary>
    /// 小驼峰序列化后使用约定的线上字段名
    /// </summary>
    [Fact]
    public void Serialize_WithCamelCasePolicy_UsesWireFieldNames()
    {
        var response = new GatewayErrorResponse
        {
            TraceId = "trace-1",
            ErrorCode = "GATEWAY_ERROR",
            ErrorMessage = "下游服务不可用",
            Path = "/api/orders",
            Timestamp = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(response, CamelCaseOptions);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("trace-1", root.GetProperty("traceId").GetString());
        Assert.Equal("GATEWAY_ERROR", root.GetProperty("errorCode").GetString());
        Assert.Equal("下游服务不可用", root.GetProperty("errorMessage").GetString());
        Assert.Equal("/api/orders", root.GetProperty("path").GetString());
        // 时间戳以 ISO 8601 字符串出现，而不是数字时间戳
        Assert.Equal(JsonValueKind.String, root.GetProperty("timestamp").ValueKind);
    }

    /// <summary>
    /// 未赋值的详细信息序列化为 null 而不是被省略
    /// </summary>
    /// <remarks>
    /// 中间件没有配置 IgnoreNullValues，客户端可以稳定地按「字段一定存在」来解析。
    /// </remarks>
    [Fact]
    public void Serialize_WithoutDetails_KeepsNullDetailsField()
    {
        var response = new GatewayErrorResponse
        {
            TraceId = "trace-1",
            ErrorCode = "GATEWAY_ERROR",
            ErrorMessage = "下游服务不可用",
            Path = "/api/orders",
            Timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(response, CamelCaseOptions);

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("details", out var details));
        Assert.Equal(JsonValueKind.Null, details.ValueKind);
    }

    /// <summary>
    /// 带详细信息的往返序列化保留全部字段
    /// </summary>
    [Fact]
    public void Roundtrip_WithDetails_PreservesAllFields()
    {
        var response = new GatewayErrorResponse
        {
            TraceId = "trace-2",
            ErrorCode = "GATEWAY_ERROR",
            ErrorMessage = "参数错误",
            Path = "/api/users/1",
            Timestamp = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            Details = new Dictionary<string, object>
            {
                ["field"] = "name"
            }
        };

        var json = JsonSerializer.Serialize(response, CamelCaseOptions);
        var restored = JsonSerializer.Deserialize<GatewayErrorResponse>(json, CamelCaseOptions);

        Assert.NotNull(restored);
        Assert.Equal(response.TraceId, restored.TraceId);
        Assert.Equal(response.ErrorCode, restored.ErrorCode);
        Assert.Equal(response.ErrorMessage, restored.ErrorMessage);
        Assert.Equal(response.Path, restored.Path);
        Assert.Equal(response.Timestamp, restored.Timestamp);
        Assert.NotNull(restored.Details);
        Assert.True(restored.Details.ContainsKey("field"));
    }

    /// <summary>
    /// UTC 时间戳往返后仍然是同一时刻
    /// </summary>
    /// <remarks>
    /// 中间件写入的是 <c>DateTime.UtcNow</c>，如果序列化丢掉时区标记，
    /// 客户端会把它当本地时间解析，链路排查时间线会整体漂移。
    /// </remarks>
    [Fact]
    public void Roundtrip_WithUtcTimestamp_KeepsUtcInstant()
    {
        var timestamp = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var response = new GatewayErrorResponse
        {
            TraceId = "trace-3",
            ErrorCode = "GATEWAY_ERROR",
            ErrorMessage = "超时",
            Path = "/api/ping",
            Timestamp = timestamp
        };

        var json = JsonSerializer.Serialize(response, CamelCaseOptions);

        using var document = JsonDocument.Parse(json);
        var serialized = document.RootElement.GetProperty("timestamp").GetDateTime();
        Assert.Equal(timestamp, serialized.ToUniversalTime());
    }
}
