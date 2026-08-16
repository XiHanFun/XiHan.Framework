// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Http.Models;
using XiHan.Framework.Http.Options;

namespace XiHan.Framework.Http.Tests.Extensions;

/// <summary>
/// <see cref="HttpServiceExtensions"/> 中结果辅助方法的测试
/// </summary>
public class HttpServiceExtensionsTests
{
    /// <summary>
    /// 成功结果 GetDataOrThrow 返回数据
    /// </summary>
    [Fact]
    public void GetDataOrThrow_ReturnsData_WhenSuccess()
    {
        var result = HttpResult<string>.Success("data");

        Assert.Equal("data", result.GetDataOrThrow());
    }

    /// <summary>
    /// 失败结果 GetDataOrThrow 抛出携带消息的 HttpRequestException
    /// </summary>
    [Fact]
    public void GetDataOrThrow_Throws_WhenFailure()
    {
        var result = HttpResult<string>.Failure("boom");

        var exception = Assert.Throws<HttpRequestException>(() => result.GetDataOrThrow());

        Assert.Equal("boom", exception.Message);
    }

    /// <summary>
    /// GetDataOrDefault 在成功时返回数据、失败时返回默认值
    /// </summary>
    [Fact]
    public void GetDataOrDefault_ReturnsDataOrFallback()
    {
        var success = HttpResult<int>.Success(42);
        var failure = HttpResult<int>.Failure("err");

        Assert.Equal(42, success.GetDataOrDefault());
        Assert.Equal(0, failure.GetDataOrDefault());
        Assert.Equal(99, failure.GetDataOrDefault(99));
    }

    /// <summary>
    /// 状态码分类方法与 HTTP 语义一致
    /// </summary>
    /// <param name="statusCode">状态码</param>
    /// <param name="isSuccess">是否成功</param>
    /// <param name="isClientError">是否客户端错误</param>
    /// <param name="isServerError">是否服务器错误</param>
    [Theory]
    [InlineData(HttpStatusCode.OK, true, false, false)]
    [InlineData(HttpStatusCode.NoContent, true, false, false)]
    [InlineData(HttpStatusCode.BadRequest, false, true, false)]
    [InlineData(HttpStatusCode.NotFound, false, true, false)]
    [InlineData(HttpStatusCode.InternalServerError, false, false, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false, false, true)]
    public void StatusCodeClassification_MatchesSemantics(HttpStatusCode statusCode, bool isSuccess, bool isClientError, bool isServerError)
    {
        var result = HttpResult<string>.Success("x", statusCode);

        Assert.Equal(isSuccess, result.IsSuccessStatusCode());
        Assert.Equal(isClientError, result.IsClientError());
        Assert.Equal(isServerError, result.IsServerError());
    }

    /// <summary>
    /// 授权相关扩展设置正确的请求头
    /// </summary>
    [Fact]
    public void AuthorizationHelpers_SetExpectedHeaders()
    {
        var bearer = new XiHanHttpRequestOptions().WithAuthorization("token", "Bearer");
        Assert.Equal("Bearer token", bearer.Headers["Authorization"]);

        var basic = new XiHanHttpRequestOptions().WithBasicAuth("user", "pass");
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass")), basic.Headers["Authorization"]);
    }

    /// <summary>
    /// 选项开关扩展方法设置对应标志
    /// </summary>
    [Fact]
    public void OptionFlags_Extensions_SetExpectedFlags()
    {
        var options = new XiHanHttpRequestOptions()
            .WithoutRetry()
            .WithoutCircuitBreaker()
            .WithoutCache();

        Assert.False(options.EnableRetry);
        Assert.False(options.EnableCircuitBreaker);
        Assert.Equal("no-cache, no-store, must-revalidate", options.Headers["Cache-Control"]);

        options.UseClient("named");
        Assert.Equal("named", options.Tags["ClientName"]);

        options.WithVerboseLogging();
        Assert.True(options.LogRequest);
        Assert.True(options.LogResponse);

        options.WithoutLogging();
        Assert.False(options.LogRequest);
        Assert.False(options.LogResponse);
    }

    /// <summary>
    /// 响应头辅助方法提取头值
    /// </summary>
    [Fact]
    public void HeaderHelpers_ExtractHeaderValues()
    {
        var result = HttpResult<string>.Success("body");
        result.Headers["Content-Type"] = ["application/json; charset=utf-8"];
        result.Headers["Content-Length"] = ["12345"];

        Assert.Equal("application/json; charset=utf-8", result.GetHeader("Content-Type"));
        Assert.Equal("application/json; charset=utf-8", result.GetContentType());
        Assert.Equal(12345L, result.GetContentLength());
        Assert.Null(result.GetHeader("Missing"));
    }
}
