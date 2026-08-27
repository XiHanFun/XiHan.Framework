// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;

namespace XiHan.Framework.Auditing.Tests;

/// <summary>
/// 操作日志记录模型测试
/// </summary>
public class OperationLogRecordTests
{
    /// <summary>
    /// 新建记录时非空字符串字段为空串、可空字段为 null、数值字段为 0
    /// </summary>
    [Fact]
    public void Ctor_Default_InitializesNonNullableStringsToEmpty()
    {
        var record = new OperationLogRecord();

        Assert.Equal(string.Empty, record.TraceId);
        Assert.Equal(string.Empty, record.Method);
        Assert.Equal(string.Empty, record.Path);

        Assert.Null(record.SessionId);
        Assert.Null(record.UserId);
        Assert.Null(record.UserName);
        Assert.Null(record.ControllerName);
        Assert.Null(record.ActionName);
        Assert.Null(record.RequestParams);
        Assert.Null(record.ResponseResult);
        Assert.Null(record.RemoteIp);
        Assert.Null(record.UserAgent);
        Assert.Null(record.ErrorMessage);

        Assert.Equal(0, record.StatusCode);
        Assert.Equal(0L, record.ElapsedMilliseconds);
    }

    /// <summary>
    /// System.Text.Json 往返后字段值与字段名均保持不变
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesValuesAndPascalCasePropertyNames()
    {
        var original = new OperationLogRecord
        {
            TraceId = "trace-5",
            SessionId = "session-5",
            UserId = 11,
            UserName = "tom",
            ControllerName = "Order",
            ActionName = "Update",
            Method = "PUT",
            Path = "/api/orders/1",
            RequestParams = "{\"id\":1}",
            ResponseResult = "{\"ok\":true}",
            StatusCode = 200,
            ElapsedMilliseconds = 88,
            RemoteIp = "10.0.0.5",
            UserAgent = "xunit",
            ErrorMessage = null
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<OperationLogRecord>(json);

        Assert.Contains("\"TraceId\":", json);
        Assert.Contains("\"ResponseResult\":", json);

        Assert.NotNull(restored);
        Assert.Equal(original.TraceId, restored!.TraceId);
        Assert.Equal(original.SessionId, restored.SessionId);
        Assert.Equal(original.UserId, restored.UserId);
        Assert.Equal(original.ControllerName, restored.ControllerName);
        Assert.Equal(original.ActionName, restored.ActionName);
        Assert.Equal(original.Method, restored.Method);
        Assert.Equal(original.Path, restored.Path);
        Assert.Equal(original.RequestParams, restored.RequestParams);
        Assert.Equal(original.ResponseResult, restored.ResponseResult);
        Assert.Equal(original.StatusCode, restored.StatusCode);
        Assert.Equal(original.ElapsedMilliseconds, restored.ElapsedMilliseconds);
        Assert.Null(restored.ErrorMessage);
    }
}
