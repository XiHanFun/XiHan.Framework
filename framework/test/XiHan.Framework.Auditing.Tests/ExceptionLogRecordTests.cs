// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;

namespace XiHan.Framework.Auditing.Tests;

/// <summary>
/// 异常日志记录模型测试
/// </summary>
/// <remarks>
/// 与其它日志模型不同，本模型的 <c>Path</c> / <c>Method</c> 是可空的（异常可能发生在 HTTP 管道之外），
/// 而 <c>ExceptionType</c> / <c>ExceptionMessage</c> 是非空的（异常日志没有这两项就没有意义）。这个非对称是刻意的，需锁住。
/// </remarks>
public class ExceptionLogRecordTests
{
    /// <summary>
    /// 新建记录时异常类型与消息为空串，请求上下文字段为 null
    /// </summary>
    [Fact]
    public void Ctor_Default_KeepsExceptionFieldsNonNullAndRequestFieldsNullable()
    {
        var record = new ExceptionLogRecord();

        Assert.Equal(string.Empty, record.TraceId);
        Assert.Equal(string.Empty, record.ExceptionType);
        Assert.Equal(string.Empty, record.ExceptionMessage);

        Assert.Null(record.Path);
        Assert.Null(record.Method);
        Assert.Null(record.ControllerName);
        Assert.Null(record.ActionName);
        Assert.Null(record.ExceptionStackTrace);
        Assert.Null(record.RequestHeaders);
        Assert.Null(record.RequestParams);
        Assert.Null(record.RequestBody);
        Assert.Null(record.RemoteIp);
        Assert.Null(record.UserAgent);
        Assert.Null(record.UserId);
        Assert.Null(record.UserName);

        Assert.Equal(0, record.StatusCode);
    }

    /// <summary>
    /// System.Text.Json 往返后字段值与字段名均保持不变
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesValuesAndPascalCasePropertyNames()
    {
        var original = new ExceptionLogRecord
        {
            TraceId = "trace-3",
            UserId = 9,
            UserName = "tom",
            Path = "/api/orders/1",
            Method = "DELETE",
            ControllerName = "Order",
            ActionName = "Delete",
            StatusCode = 500,
            ExceptionType = "System.InvalidOperationException",
            ExceptionMessage = "订单状态不允许删除",
            ExceptionStackTrace = "   at Order.Delete()",
            RequestHeaders = "{\"Authorization\":\"***\"}",
            RequestParams = "{\"id\":1}",
            RequestBody = "{}",
            RemoteIp = "10.0.0.3",
            UserAgent = "xunit"
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<ExceptionLogRecord>(json);

        Assert.Contains("\"ExceptionType\":", json);
        Assert.Contains("\"ExceptionStackTrace\":", json);

        Assert.NotNull(restored);
        Assert.Equal(original.TraceId, restored!.TraceId);
        Assert.Equal(original.Path, restored.Path);
        Assert.Equal(original.Method, restored.Method);
        Assert.Equal(original.ExceptionType, restored.ExceptionType);
        Assert.Equal(original.ExceptionMessage, restored.ExceptionMessage);
        Assert.Equal(original.ExceptionStackTrace, restored.ExceptionStackTrace);
        Assert.Equal(original.RequestHeaders, restored.RequestHeaders);
        Assert.Equal(original.RequestParams, restored.RequestParams);
        Assert.Equal(original.StatusCode, restored.StatusCode);
    }
}
