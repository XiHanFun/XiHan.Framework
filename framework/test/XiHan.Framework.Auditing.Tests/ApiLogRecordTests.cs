// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;

namespace XiHan.Framework.Auditing.Tests;

/// <summary>
/// 接口日志记录模型测试
/// </summary>
/// <remarks>
/// 本模型有两个「乐观默认」：<c>IsSignatureValid</c> 与 <c>IsSuccess</c> 默认为 true，
/// 采集侧只在失败时显式置 false。这两个默认值一旦被改成 false，历史查询口径会整体翻转，必须锁死。
/// </remarks>
public class ApiLogRecordTests
{
    /// <summary>
    /// 新建记录时布尔字段乐观默认为 true，非空字符串字段为空串
    /// </summary>
    [Fact]
    public void Ctor_Default_UsesOptimisticBooleanDefaults()
    {
        var record = new ApiLogRecord();

        Assert.True(record.IsSignatureValid);
        Assert.True(record.IsSuccess);

        Assert.Equal(string.Empty, record.TraceId);
        Assert.Equal(string.Empty, record.Method);
        Assert.Equal(string.Empty, record.Path);

        Assert.Null(record.ClientId);
        Assert.Null(record.AppId);
        Assert.Null(record.SignatureAlgorithm);
        Assert.Null(record.ApiName);
        Assert.Null(record.ControllerName);
        Assert.Null(record.ActionName);
        Assert.Null(record.RequestParams);
        Assert.Null(record.RequestBody);
        Assert.Null(record.ResponseBody);
        Assert.Null(record.ErrorMessage);

        Assert.Equal(0, record.StatusCode);
        Assert.Equal(0L, record.RequestSize);
        Assert.Equal(0L, record.ResponseSize);
        Assert.Equal(0L, record.ElapsedMilliseconds);
    }

    /// <summary>
    /// System.Text.Json 往返后字段值与字段名均保持不变
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesValuesAndPascalCasePropertyNames()
    {
        var original = new ApiLogRecord
        {
            TraceId = "trace-2",
            UserId = 7,
            UserName = "api-user",
            ClientId = "ak-1",
            AppId = "app-1",
            IsSignatureValid = false,
            SignatureAlgorithm = "HMAC-SHA256",
            Method = "POST",
            Path = "/openapi/pay",
            ApiName = "CreatePayment",
            ControllerName = "Payment",
            ActionName = "Create",
            RequestParams = "{\"a\":1}",
            RequestBody = "{\"b\":2}",
            ResponseBody = "{\"c\":3}",
            StatusCode = 500,
            RemoteIp = "10.0.0.2",
            UserAgent = "xunit",
            Referer = null,
            ElapsedMilliseconds = 12,
            RequestSize = 64,
            ResponseSize = 128,
            IsSuccess = false,
            ErrorMessage = "签名校验失败"
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<ApiLogRecord>(json);

        Assert.Contains("\"IsSignatureValid\":", json);
        Assert.Contains("\"IsSuccess\":", json);

        Assert.NotNull(restored);
        Assert.Equal(original.TraceId, restored!.TraceId);
        Assert.Equal(original.ClientId, restored.ClientId);
        Assert.Equal(original.AppId, restored.AppId);
        Assert.Equal(original.SignatureAlgorithm, restored.SignatureAlgorithm);
        Assert.Equal(original.ApiName, restored.ApiName);
        Assert.Equal(original.RequestParams, restored.RequestParams);
        Assert.Equal(original.ResponseBody, restored.ResponseBody);
        Assert.Equal(original.StatusCode, restored.StatusCode);
        Assert.Equal(original.RequestSize, restored.RequestSize);
        Assert.Equal(original.ResponseSize, restored.ResponseSize);
        Assert.Equal(original.ErrorMessage, restored.ErrorMessage);

        // 显式置 false 的两个乐观默认必须能被往返带回来，不能被默认值吃掉
        Assert.False(restored.IsSignatureValid);
        Assert.False(restored.IsSuccess);
    }
}
