// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using XiHan.Framework.Http.Models;

namespace XiHan.Framework.Http.Tests.Models;

/// <summary>
/// <see cref="HttpResult{T}"/> 与无泛型 <see cref="HttpResult"/> 的纯逻辑测试
/// </summary>
public class HttpResultTests
{
    /// <summary>
    /// 成功构造默认使用 OK 状态码并往返数据
    /// </summary>
    [Fact]
    public void Success_WithData_IsSuccessAndRoundTripsData()
    {
        var payload = new Payload { Id = 1, Name = "sample" };

        var result = HttpResult<Payload>.Success(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Same(payload, result.Data);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// 成功构造可指定自定义状态码
    /// </summary>
    [Fact]
    public void Success_WithCustomStatusCode_SetsStatusCode()
    {
        var result = HttpResult<string>.Success("created", HttpStatusCode.Created);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        Assert.Equal("created", result.Data);
    }

    /// <summary>
    /// 失败构造默认使用 500 状态码并携带错误消息
    /// </summary>
    [Fact]
    public void Failure_WithMessage_DefaultsToInternalServerError()
    {
        var result = HttpResult<string>.Failure("boom");

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal("boom", result.ErrorMessage);
        Assert.Null(result.Exception);
    }

    /// <summary>
    /// 失败构造保留异常与自定义状态码
    /// </summary>
    [Fact]
    public void Failure_WithException_PreservesException()
    {
        var exception = new InvalidOperationException("inner");
        var result = HttpResult<int>.Failure("failed", HttpStatusCode.BadGateway, exception);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
        Assert.Equal("failed", result.ErrorMessage);
        Assert.Same(exception, result.Exception);
    }

    /// <summary>
    /// 无泛型成功结果默认使用 OK 状态码
    /// </summary>
    [Fact]
    public void NonGeneric_Success_IsSuccessWithOkStatus()
    {
        var result = HttpResult.Success();

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    /// <summary>
    /// 无泛型失败结果携带消息与状态码
    /// </summary>
    [Fact]
    public void NonGeneric_Failure_SetsMessageAndStatus()
    {
        var result = HttpResult.Failure("denied", HttpStatusCode.Forbidden);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal("denied", result.ErrorMessage);
    }

    /// <summary>
    /// 测试用负载类型
    /// </summary>
    private sealed class Payload
    {
        /// <summary>
        /// 标识
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}
