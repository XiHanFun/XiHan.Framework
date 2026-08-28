// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Utils.Security.ErrorObfuscation;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Models;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Options;

namespace XiHan.Framework.Utils.Tests.Security.ErrorObfuscation;

/// <summary>
/// 错误混淆辅助类测试
/// </summary>
/// <remarks>
/// 内容本身是随机的，可断言的是「形状」：指定格式必须给出该格式的载体类型，
/// 指定语言必须真的把该语言的名字写进伪造错误里，Content-Type 必须与格式一一对应。
/// 涉及延迟的异步方法一律用极小的延迟值，避免用例真的去睡两秒。
/// </remarks>
public class ErrorObfuscationHelperTests
{
    /// <summary>
    /// 指定格式时返回对应的载体类型
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithFormat_ReturnsMatchingCarrierType()
    {
        Assert.IsType<Dictionary<string, object>>(ErrorObfuscationHelper.GenerateObfuscatedError(ErrorFormat.JsonObject));
        Assert.IsType<Dictionary<string, object>>(ErrorObfuscationHelper.GenerateObfuscatedError(ErrorFormat.JsonArray));
        Assert.IsType<string>(ErrorObfuscationHelper.GenerateObfuscatedError(ErrorFormat.PlainText));
        Assert.IsType<string>(ErrorObfuscationHelper.GenerateObfuscatedError(ErrorFormat.Xml));
        Assert.IsType<string>(ErrorObfuscationHelper.GenerateObfuscatedError(ErrorFormat.Html));
    }

    /// <summary>
    /// JSON 对象格式携带完整的伪造字段
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithJsonObjectFormat_ContainsExpectedKeys()
    {
        var error = Assert.IsType<Dictionary<string, object>>(
            ErrorObfuscationHelper.GenerateObfuscatedError(ErrorFormat.JsonObject));

        string[] expectedKeys =
        [
            "status", "error", "message", "exception", "timestamp", "timestampISO",
            "traceId", "requestId", "path", "method", "server", "database", "stackTrace", "metadata"
        ];

        Assert.All(expectedKeys, key => Assert.True(error.ContainsKey(key), $"缺少键：{key}"));
        Assert.IsType<Dictionary<string, object>>(error["metadata"]);
    }

    /// <summary>
    /// JSON 数组格式携带错误列表与计数
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithJsonArrayFormat_ContainsErrorList()
    {
        var error = Assert.IsType<Dictionary<string, object>>(
            ErrorObfuscationHelper.GenerateObfuscatedError(ErrorFormat.JsonArray));

        Assert.True(error.ContainsKey("errors"));
        Assert.True(error.ContainsKey("timestamp"));
        Assert.True(error.ContainsKey("traceId"));

        var count = Assert.IsType<int>(error["count"]);
        var items = Assert.IsType<List<Dictionary<string, object>>>(error["errors"]);

        Assert.Equal(count, items.Count);
        Assert.InRange(count, 2, 4);
    }

    /// <summary>
    /// 纯文本格式是完整的错误报告文本
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithPlainTextFormat_ReturnsErrorReport()
    {
        var text = Assert.IsType<string>(ErrorObfuscationHelper.GenerateObfuscatedError(ErrorFormat.PlainText));

        Assert.Contains("ERROR REPORT", text);
        Assert.Contains("ERROR MESSAGE:", text);
        Assert.Contains("STACK TRACE:", text);
    }

    /// <summary>
    /// XML 格式的根元素是 Error
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithXmlFormat_ReturnsErrorDocument()
    {
        var xml = Assert.IsType<string>(ErrorObfuscationHelper.GenerateObfuscatedError(ErrorFormat.Xml));

        Assert.Contains("<Error", xml);
        Assert.Contains("</Error>", xml);
    }

    /// <summary>
    /// HTML 格式是完整页面
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithHtmlFormat_ReturnsHtmlDocument()
    {
        var html = Assert.IsType<string>(ErrorObfuscationHelper.GenerateObfuscatedError(ErrorFormat.Html));

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("</html>", html);
    }

    /// <summary>
    /// 无参重载返回五种载体之一
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithoutArguments_ReturnsSupportedCarrier()
    {
        for (var i = 0; i < 30; i++)
        {
            var error = ErrorObfuscationHelper.GenerateObfuscatedError();

            Assert.True(error is Dictionary<string, object> or string, $"意外的载体类型：{error.GetType()}");
        }
    }

    /// <summary>
    /// 指定语言时该语言名会出现在纯文本伪造错误里
    /// </summary>
    [Theory]
    [InlineData(ProgrammingLanguage.CSharp)]
    [InlineData(ProgrammingLanguage.Java)]
    [InlineData(ProgrammingLanguage.Php)]
    [InlineData(ProgrammingLanguage.Go)]
    [InlineData(ProgrammingLanguage.Python)]
    [InlineData(ProgrammingLanguage.NodeJs)]
    [InlineData(ProgrammingLanguage.Ruby)]
    [InlineData(ProgrammingLanguage.Rust)]
    public void GenerateObfuscatedError_WithLanguage_StampsLanguageIntoPlainText(ProgrammingLanguage language)
    {
        var text = Assert.IsType<string>(
            ErrorObfuscationHelper.GenerateObfuscatedError(language, ErrorFormat.PlainText));

        Assert.Contains("Language:    " + language, text);
    }

    /// <summary>
    /// 指定语言时该语言名会出现在 XML 伪造错误里
    /// </summary>
    [Theory]
    [InlineData(ProgrammingLanguage.CSharp)]
    [InlineData(ProgrammingLanguage.Rust)]
    public void GenerateObfuscatedError_WithLanguage_StampsLanguageIntoXml(ProgrammingLanguage language)
    {
        var xml = Assert.IsType<string>(ErrorObfuscationHelper.GenerateObfuscatedError(language, ErrorFormat.Xml));

        Assert.Contains($"<Language>{language}</Language>", xml);
    }

    /// <summary>
    /// 只指定语言的重载返回 JSON 字典
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithLanguageOnly_ReturnsJsonDictionary()
    {
        var error = ErrorObfuscationHelper.GenerateObfuscatedError(ProgrammingLanguage.Ruby);

        Assert.True(error.ContainsKey("status"));
        Assert.True(error.ContainsKey("stackTrace"));
    }

    /// <summary>
    /// Content-Type 与格式一一对应
    /// </summary>
    [Theory]
    [InlineData(ErrorFormat.JsonObject, "application/json; charset=utf-8")]
    [InlineData(ErrorFormat.JsonArray, "application/json; charset=utf-8")]
    [InlineData(ErrorFormat.PlainText, "text/plain; charset=utf-8")]
    [InlineData(ErrorFormat.Xml, "application/xml; charset=utf-8")]
    [InlineData(ErrorFormat.Html, "text/html; charset=utf-8")]
    public void GetContentType_MapsFormatToMediaType(ErrorFormat format, string expected)
    {
        Assert.Equal(expected, ErrorObfuscationHelper.GetContentType(format));
    }

    /// <summary>
    /// 未定义的格式回落到 JSON 的 Content-Type
    /// </summary>
    [Fact]
    public void GetContentType_WithUndefinedFormat_FallsBackToJson()
    {
        Assert.Equal("application/json; charset=utf-8", ErrorObfuscationHelper.GetContentType((ErrorFormat)99));
    }

    /// <summary>
    /// 指定格式的响应同时给出正确的 Content-Type
    /// </summary>
    [Theory]
    [InlineData(ErrorFormat.JsonObject)]
    [InlineData(ErrorFormat.PlainText)]
    [InlineData(ErrorFormat.Xml)]
    [InlineData(ErrorFormat.Html)]
    public void GenerateObfuscatedErrorResponse_WithFormat_PairsPayloadWithContentType(ErrorFormat format)
    {
        var (errorObject, contentType) = ErrorObfuscationHelper.GenerateObfuscatedErrorResponse(format);

        Assert.NotNull(errorObject);
        Assert.Equal(ErrorObfuscationHelper.GetContentType(format), contentType);
    }

    /// <summary>
    /// 随机响应的 Content-Type 落在支持集合内
    /// </summary>
    [Fact]
    public void GenerateObfuscatedErrorResponse_WithoutFormat_ReturnsSupportedContentType()
    {
        string[] supported =
        [
            "application/json; charset=utf-8",
            "text/plain; charset=utf-8",
            "application/xml; charset=utf-8",
            "text/html; charset=utf-8"
        ];

        for (var i = 0; i < 30; i++)
        {
            var (errorObject, contentType) = ErrorObfuscationHelper.GenerateObfuscatedErrorResponse();

            Assert.NotNull(errorObject);
            Assert.Contains(contentType, supported);
        }
    }

    /// <summary>
    /// 批量生成指定数量的伪造错误
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void GenerateBatchObfuscatedErrors_ReturnsRequestedCount(int count)
    {
        var errors = ErrorObfuscationHelper.GenerateBatchObfuscatedErrors(count);

        Assert.Equal(count, errors.Count);
        Assert.All(errors, error => Assert.NotNull(error));
    }

    /// <summary>
    /// 批量数量不为正数时拒绝
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GenerateBatchObfuscatedErrors_WithNonPositiveCount_ThrowsArgumentException(int count)
    {
        Assert.Throws<ArgumentException>(() => { _ = ErrorObfuscationHelper.GenerateBatchObfuscatedErrors(count); });
    }

    /// <summary>
    /// 按配置项生成时格式生效
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithOptions_HonorsFormatAndLanguage()
    {
        var options = ErrorObfuscationOptions.With(ProgrammingLanguage.Php, ErrorFormat.PlainText);

        var text = Assert.IsType<string>(ErrorObfuscationHelper.GenerateObfuscatedError(options));

        Assert.Contains("ERROR REPORT", text);
        Assert.Contains("Language:    Php", text);
    }

    /// <summary>
    /// 配置项为空引用时拒绝
    /// </summary>
    [Fact]
    public void GenerateObfuscatedError_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => { _ = ErrorObfuscationHelper.GenerateObfuscatedError((ErrorObfuscationOptions)null!); });
    }

    /// <summary>
    /// 异步响应按配置项给出格式与 Content-Type
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task GenerateObfuscatedErrorResponseAsync_WithFixedDelay_HonorsFormat()
    {
        var options = ErrorObfuscationOptions.With(ProgrammingLanguage.Go, ErrorFormat.Xml);
        options.DelayMs = 1;

        var (errorObject, contentType) = await ErrorObfuscationHelper.GenerateObfuscatedErrorResponseAsync(options);

        var xml = Assert.IsType<string>(errorObject);
        Assert.Contains("<Language>Go</Language>", xml);
        Assert.Equal("application/xml; charset=utf-8", contentType);
    }

    /// <summary>
    /// 异步响应支持随机延迟区间
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task GenerateObfuscatedErrorResponseAsync_WithRandomDelay_StillReturnsPayload()
    {
        var options = ErrorObfuscationOptions.WithRandomDelay(1, 3);
        options.Format = ErrorFormat.JsonObject;

        var (errorObject, contentType) = await ErrorObfuscationHelper.GenerateObfuscatedErrorResponseAsync(options);

        Assert.IsType<Dictionary<string, object>>(errorObject);
        Assert.Equal("application/json; charset=utf-8", contentType);
    }

    /// <summary>
    /// 配置项为空引用时异步方法同样拒绝
    /// </summary>
    [Fact]
    public async Task GenerateObfuscatedErrorResponseAsync_WithNullOptions_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ErrorObfuscationHelper.GenerateObfuscatedErrorResponseAsync(null!));
    }

    /// <summary>
    /// 带延迟的响应生成仍然返回可用载体
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task GenerateObfuscatedErrorResponseWithDelayAsync_ReturnsPayload()
    {
        var (errorObject, contentType) = await ErrorObfuscationHelper.GenerateObfuscatedErrorResponseWithDelayAsync(1, 3);

        Assert.NotNull(errorObject);
        Assert.NotEmpty(contentType);
    }

    /// <summary>
    /// JSON 对象载体可以被直接序列化为合法 JSON
    /// </summary>
    /// <remarks>
    /// 中间件会把这个字典直接写进响应体，所以「能被 System.Text.Json 序列化并解析回来」是硬要求。
    /// </remarks>
    [Fact]
    public void GenerateObfuscatedError_JsonObjectPayload_IsSerializableJson()
    {
        var error = Assert.IsType<Dictionary<string, object>>(
            ErrorObfuscationHelper.GenerateObfuscatedError(ErrorFormat.JsonObject));

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(error));

        Assert.True(document.RootElement.TryGetProperty("status", out var status));
        Assert.InRange(status.GetInt32(), 400, 511);
    }
}
