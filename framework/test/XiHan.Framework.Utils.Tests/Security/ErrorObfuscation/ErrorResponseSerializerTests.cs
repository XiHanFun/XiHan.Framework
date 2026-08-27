// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Models;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Utilities;

namespace XiHan.Framework.Utils.Tests.Security.ErrorObfuscation;

/// <summary>
/// 错误响应序列化器测试
/// </summary>
/// <remarks>
/// 序列化结果会被直接写进 HTTP 响应体，字段名（camelCase）、根元素名与转义规则都是对外可见的形状，
/// 所以逐类型锁死。JSON 断言走 <see cref="JsonDocument"/> 解析而不是字符串包含，避免被缩进格式影响。
/// </remarks>
public class ErrorResponseSerializerTests
{
    /// <summary>
    /// JSON 对象响应按 camelCase 序列化，嵌套元数据同样生效
    /// </summary>
    [Fact]
    public void Serialize_JsonErrorResponse_UsesCamelCaseProperties()
    {
        var response = new JsonErrorResponse
        {
            Status = 500,
            Error = "InternalServerError",
            Message = "boom",
            Exception = "System.InvalidOperationException",
            Timestamp = 1_700_000_000_000,
            TimestampISO = "2023-11-14T22:13:20.000Z",
            TraceId = "trace-1",
            RequestId = "request-1",
            Path = "/api/v1/users",
            Method = "GET",
            Language = "CSharp",
            Server = "Kestrel/7.0.4",
            Database = "PostgreSQL 15.2",
            StackTrace = "at Foo.Bar()",
            Metadata = new ErrorMetadata
            {
                Hostname = "web-001.internal.com",
                Pid = 1234,
                ThreadId = 7,
                MemoryUsage = "512MB"
            }
        };

        using var document = JsonDocument.Parse(ErrorResponseSerializer.Serialize(response));
        var root = document.RootElement;

        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("InternalServerError", root.GetProperty("error").GetString());
        Assert.Equal("boom", root.GetProperty("message").GetString());
        Assert.Equal(1_700_000_000_000, root.GetProperty("timestamp").GetInt64());
        Assert.Equal("2023-11-14T22:13:20.000Z", root.GetProperty("timestampISO").GetString());
        Assert.Equal("trace-1", root.GetProperty("traceId").GetString());
        Assert.Equal("web-001.internal.com", root.GetProperty("metadata").GetProperty("hostname").GetString());
        Assert.Equal(1234, root.GetProperty("metadata").GetProperty("pid").GetInt32());
    }

    /// <summary>
    /// JSON 数组响应序列化出错误列表与计数
    /// </summary>
    [Fact]
    public void Serialize_JsonErrorArrayResponse_EmitsErrorList()
    {
        var response = new JsonErrorArrayResponse
        {
            Errors =
            [
                new ErrorItem
                {
                    Code = "500",
                    Type = "InternalServerError",
                    Message = "boom",
                    Detail = "at Foo.Bar()",
                    Source = new ErrorSource { Language = "Go", Exception = "runtime.Error" }
                }
            ],
            Timestamp = "2023-11-14T22:13:20.000Z",
            TraceId = "trace-1",
            Count = 1
        };

        using var document = JsonDocument.Parse(ErrorResponseSerializer.Serialize(response));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("count").GetInt32());
        Assert.Equal(1, root.GetProperty("errors").GetArrayLength());
        Assert.Equal("500", root.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal("Go", root.GetProperty("errors")[0].GetProperty("source").GetProperty("language").GetString());
    }

    /// <summary>
    /// 纯文本响应直接走自身的 ToString
    /// </summary>
    [Fact]
    public void Serialize_PlainTextErrorResponse_UsesItsOwnToString()
    {
        var response = new PlainTextErrorResponse
        {
            Time = "2023-11-14 22:13:20",
            Status = 503,
            Type = "ServiceUnavailable",
            Language = "Python",
            Server = "gunicorn/20.1.0",
            TraceId = "trace-1",
            Host = "api-002.cloud.com",
            Message = "boom",
            StackTrace = "Traceback (most recent call last):"
        };

        var text = ErrorResponseSerializer.Serialize(response);

        Assert.Equal(response.ToString(), text);
        Assert.Contains("ERROR REPORT", text);
        Assert.Contains("Status:      HTTP 503", text);
        Assert.Contains("Language:    Python", text);
        Assert.Contains("Host:        api-002.cloud.com", text);
        Assert.Contains("ERROR MESSAGE:", text);
        Assert.Contains("STACK TRACE:", text);
    }

    /// <summary>
    /// XML 响应的根元素名为 Error
    /// </summary>
    [Fact]
    public void Serialize_XmlErrorResponse_UsesErrorRootElement()
    {
        var response = new XmlErrorResponse
        {
            Status = 502,
            Type = "BadGateway",
            Message = "boom",
            Exception = "java.lang.RuntimeException",
            Timestamp = "2023-11-14T22:13:20.000Z",
            TraceId = "trace-1",
            Language = "Java",
            Server = "Tomcat/9.0.71",
            StackTrace = "at Foo.bar()",
            Metadata = new XmlErrorMetadata
            {
                Hostname = "srv-003.prod.com",
                ProcessId = 4321,
                ThreadId = 9
            }
        };

        var xml = ErrorResponseSerializer.Serialize(response);

        Assert.Contains("<Error", xml);
        Assert.Contains("</Error>", xml);
        Assert.Contains("<Status>502</Status>", xml);
        Assert.Contains("<Language>Java</Language>", xml);
        Assert.Contains("<Hostname>srv-003.prod.com</Hostname>", xml);
    }

    /// <summary>
    /// HTML 响应是完整页面且对正文做转义
    /// </summary>
    /// <remarks>
    /// 这是给攻击者看的伪造错误页，如果不转义就等于把注入点直接送出去，因此转义是安全契约的一部分。
    /// </remarks>
    [Fact]
    public void Serialize_HtmlErrorResponse_EscapesUserVisibleFields()
    {
        var response = new HtmlErrorResponse
        {
            ErrorType = "InternalServerError",
            StatusCode = 500,
            Message = "<script>alert(1)</script>",
            ExceptionType = "System.Exception",
            Language = "NodeJs",
            Timestamp = "2023-11-14 22:13:20",
            TraceId = "trace-1",
            Server = "Express/4.18.2",
            Hostname = "node-004.local.com",
            StackTrace = "at Object.<anonymous>"
        };

        var html = ErrorResponseSerializer.Serialize(response);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<title>Error 500 - InternalServerError</title>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("HTTP Error 500", html);
        Assert.Contains("node-004.local.com", html);
        Assert.EndsWith("</html>" + Environment.NewLine, html);
    }

    /// <summary>
    /// 未识别的对象回落到通用 JSON 序列化，同样是 camelCase
    /// </summary>
    [Fact]
    public void Serialize_UnknownType_FallsBackToCamelCaseJson()
    {
        using var document = JsonDocument.Parse(ErrorResponseSerializer.Serialize(new ErrorMetadata
        {
            Hostname = "host-005.cluster.com",
            Pid = 42,
            ThreadId = 3,
            MemoryUsage = "128MB"
        }));

        Assert.Equal("host-005.cluster.com", document.RootElement.GetProperty("hostname").GetString());
        Assert.Equal(42, document.RootElement.GetProperty("pid").GetInt32());
        Assert.Equal("128MB", document.RootElement.GetProperty("memoryUsage").GetString());
    }
}
