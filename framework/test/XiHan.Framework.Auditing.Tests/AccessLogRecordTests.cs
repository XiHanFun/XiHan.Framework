// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;

namespace XiHan.Framework.Auditing.Tests;

/// <summary>
/// 访问日志记录模型测试
/// </summary>
/// <remarks>
/// 该模型会进队列、跨线程传递并最终落库，所以三件事必须钉住：
/// 非空字符串字段的默认值（避免消费端拿到 null 再 NRE）、JSON 字段名（对外线格式）、
/// 以及它是 class 而非 record —— 相等性是引用语义，队列去重之类的用法不能想当然。
/// </remarks>
public class AccessLogRecordTests
{
    /// <summary>
    /// 新建记录时非空字符串字段为空串、可空字段为 null、数值字段为 0
    /// </summary>
    [Fact]
    public void Ctor_Default_InitializesNonNullableStringsToEmpty()
    {
        var record = new AccessLogRecord();

        Assert.Equal(string.Empty, record.TraceId);
        Assert.Equal(string.Empty, record.Method);
        Assert.Equal(string.Empty, record.Path);

        Assert.Null(record.UserId);
        Assert.Null(record.UserName);
        Assert.Null(record.SessionId);
        Assert.Null(record.ResourceName);
        Assert.Null(record.QueryString);
        Assert.Null(record.RequestBody);
        Assert.Null(record.RemoteIp);
        Assert.Null(record.UserAgent);
        Assert.Null(record.Referer);
        Assert.Null(record.ErrorMessage);

        Assert.Equal(0, record.StatusCode);
        Assert.Equal(0L, record.ElapsedMilliseconds);
        Assert.Equal(0L, record.ResponseSize);
    }

    /// <summary>
    /// System.Text.Json 往返后字段值与字段名均保持不变
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesValuesAndPascalCasePropertyNames()
    {
        var original = new AccessLogRecord
        {
            TraceId = "trace-1",
            UserId = 1024,
            UserName = "tom",
            SessionId = "session-1",
            ResourceName = "OrderController.Get",
            Method = "GET",
            Path = "/api/orders",
            QueryString = "?page=1",
            RequestBody = "{}",
            StatusCode = 200,
            RemoteIp = "10.0.0.1",
            UserAgent = "xunit",
            Referer = "https://example.com",
            ElapsedMilliseconds = 37,
            ResponseSize = 512,
            ErrorMessage = null
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AccessLogRecord>(json);

        Assert.Contains("\"TraceId\":", json);
        Assert.Contains("\"ElapsedMilliseconds\":", json);

        Assert.NotNull(restored);
        Assert.Equal(original.TraceId, restored!.TraceId);
        Assert.Equal(original.UserId, restored.UserId);
        Assert.Equal(original.UserName, restored.UserName);
        Assert.Equal(original.SessionId, restored.SessionId);
        Assert.Equal(original.ResourceName, restored.ResourceName);
        Assert.Equal(original.Method, restored.Method);
        Assert.Equal(original.Path, restored.Path);
        Assert.Equal(original.QueryString, restored.QueryString);
        Assert.Equal(original.StatusCode, restored.StatusCode);
        Assert.Equal(original.ElapsedMilliseconds, restored.ElapsedMilliseconds);
        Assert.Equal(original.ResponseSize, restored.ResponseSize);
        Assert.Null(restored.ErrorMessage);
    }

    /// <summary>
    /// 日志记录是 class，字段相同的两个实例并不相等（引用语义）
    /// </summary>
    [Fact]
    public void Equals_WhenSameFieldValues_IsStillReferenceSemantics()
    {
        var left = new AccessLogRecord { TraceId = "same" };
        var right = new AccessLogRecord { TraceId = "same" };

        Assert.False(left.Equals(right));
        Assert.NotSame(left, right);
    }
}
