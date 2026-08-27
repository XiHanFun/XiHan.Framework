// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;

namespace XiHan.Framework.Application.Contracts.Tests.Dtos;

/// <summary>
/// 统一返回信封测试
/// </summary>
/// <remarks>
/// ApiResponse 是全框架对外响应的唯一出口，工厂方法的 Code/Message/Data 三元组一旦错位，
/// 前端会拿到「码是失败、文案是成功」的自相矛盾响应。这里逐个工厂方法锁定三元组，
/// 并锁定序列化后的字段名与 Code 的数字形态。
/// </remarks>
public class ApiResponseTests
{
    /// <summary>
    /// 无参构造出来的信封默认就是成功语义
    /// </summary>
    [Fact]
    public void Constructor_Default_IsSuccessEnvelope()
    {
        var response = new ApiResponse();

        Assert.Equal(ApiResponseCodes.Success, response.Code);
        Assert.Equal("请求成功", response.Message);
        Assert.Null(response.Data);
        Assert.Null(response.TraceId);
        Assert.True(response.IsSuccess);
    }

    /// <summary>
    /// 时间戳默认取 UTC 当前时间，偏移量必须为零
    /// </summary>
    [Fact]
    public void Timestamp_Default_IsUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var response = new ApiResponse();
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(TimeSpan.Zero, response.Timestamp.Offset);
        Assert.True(response.Timestamp >= before);
        Assert.True(response.Timestamp <= after);
    }

    /// <summary>
    /// 仅 2xx 视为成功，含未内置工厂的 2xx 与全部业务码
    /// </summary>
    [Theory]
    [InlineData(100, false)]
    [InlineData(199, false)]
    [InlineData(200, true)]
    [InlineData(201, true)]
    [InlineData(204, true)]
    [InlineData(299, true)]
    [InlineData(300, false)]
    [InlineData(304, false)]
    [InlineData(400, false)]
    [InlineData(423, false)]
    [InlineData(500, false)]
    [InlineData(10001, false)]
    [InlineData(11000, false)]
    public void IsSuccess_DependsOnlyOn2xxRange(int code, bool expected)
    {
        var response = new ApiResponse { Code = (ApiResponseCodes)code };

        Assert.Equal(expected, response.IsSuccess);
    }

    /// <summary>
    /// Continue 工厂：100 且不算成功
    /// </summary>
    [Fact]
    public void Continue_ReturnsInformationalEnvelope()
    {
        var response = ApiResponse.Continue();

        Assert.Equal(ApiResponseCodes.Continue, response.Code);
        Assert.Equal("继续请求", response.Message);
        Assert.Null(response.Data);
        Assert.Null(response.TraceId);
        Assert.False(response.IsSuccess);
    }

    /// <summary>
    /// Success 工厂：原样承载业务数据与追踪 ID
    /// </summary>
    [Fact]
    public void Success_CarriesDataAndTraceId()
    {
        var payload = new { Name = "曦寒" };

        var response = ApiResponse.Success(payload, "trace-001");

        Assert.Equal(ApiResponseCodes.Success, response.Code);
        Assert.Equal("请求成功", response.Message);
        Assert.Same(payload, response.Data);
        Assert.Equal("trace-001", response.TraceId);
        Assert.True(response.IsSuccess);
    }

    /// <summary>
    /// Success 工厂允许数据为 null（例如查询无结果但调用本身成功）
    /// </summary>
    [Fact]
    public void Success_WhenDataNull_StillSucceeds()
    {
        var response = ApiResponse.Success(null, null);

        Assert.True(response.IsSuccess);
        Assert.Null(response.Data);
        Assert.Null(response.TraceId);
    }

    /// <summary>
    /// Created 工厂两个参数均可省略，省略时不带数据
    /// </summary>
    [Fact]
    public void Created_WithoutArguments_UsesNullPayload()
    {
        var response = ApiResponse.Created();

        Assert.Equal(ApiResponseCodes.Created, response.Code);
        Assert.Equal("资源创建成功", response.Message);
        Assert.Null(response.Data);
        Assert.Null(response.TraceId);
        Assert.True(response.IsSuccess);
    }

    /// <summary>
    /// Created 工厂带参时把新资源标识放进 Data
    /// </summary>
    [Fact]
    public void Created_WithArguments_CarriesPayloadAndTraceId()
    {
        var response = ApiResponse.Created(1024L, "trace-002");

        Assert.Equal(1024L, response.Data);
        Assert.Equal("trace-002", response.TraceId);
    }

    /// <summary>
    /// BadRequest 工厂：错误明细落在 Data 而不是 Message
    /// </summary>
    /// <remarks>
    /// Message 固定为返回码描述、明细走 Data，是本信封的既定分工：前端弹提示用 Message，排查用 Data。
    /// </remarks>
    [Fact]
    public void BadRequest_PutsErrorDetailInData()
    {
        var response = ApiResponse.BadRequest("缺少参数 id", "trace-003");

        Assert.Equal(ApiResponseCodes.BadRequest, response.Code);
        Assert.Equal("请求错误", response.Message);
        Assert.Equal("缺少参数 id", response.Data);
        Assert.Equal("trace-003", response.TraceId);
        Assert.False(response.IsSuccess);
    }

    /// <summary>
    /// Unauthorized 工厂不接受追踪 ID，TraceId 必为 null
    /// </summary>
    [Fact]
    public void Unauthorized_HasNoTraceIdOverload()
    {
        var response = ApiResponse.Unauthorized("令牌缺失");

        Assert.Equal(ApiResponseCodes.Unauthorized, response.Code);
        Assert.Equal("未授权", response.Message);
        Assert.Equal("令牌缺失", response.Data);
        Assert.Null(response.TraceId);
    }

    /// <summary>
    /// Forbidden 工厂不回传任何明细
    /// </summary>
    [Fact]
    public void Forbidden_CarriesNoDetail()
    {
        var response = ApiResponse.Forbidden();

        Assert.Equal(ApiResponseCodes.Forbidden, response.Code);
        Assert.Equal("禁止访问", response.Message);
        Assert.Null(response.Data);
        Assert.Null(response.TraceId);
    }

    /// <summary>
    /// NotFound 工厂不回传任何明细
    /// </summary>
    [Fact]
    public void NotFound_CarriesNoDetail()
    {
        var response = ApiResponse.NotFound();

        Assert.Equal(ApiResponseCodes.NotFound, response.Code);
        Assert.Equal("资源不存在", response.Message);
        Assert.Null(response.Data);
    }

    /// <summary>
    /// UnprocessableEntity 工厂：校验明细落在 Data
    /// </summary>
    [Fact]
    public void UnprocessableEntity_PutsErrorDetailInData()
    {
        var response = ApiResponse.UnprocessableEntity("金额必须大于 0");

        Assert.Equal(ApiResponseCodes.UnprocessableEntity, response.Code);
        Assert.Equal("请求语义错误", response.Message);
        Assert.Equal("金额必须大于 0", response.Data);
    }

    /// <summary>
    /// TooManyRequests 工厂不回传任何明细
    /// </summary>
    [Fact]
    public void TooManyRequests_CarriesNoDetail()
    {
        var response = ApiResponse.TooManyRequests();

        Assert.Equal(ApiResponseCodes.TooManyRequests, response.Code);
        Assert.Equal("请求过于频繁", response.Message);
        Assert.Null(response.Data);
    }

    /// <summary>
    /// InternalServerError 工厂：明细与追踪 ID 都可选
    /// </summary>
    [Fact]
    public void InternalServerError_WithoutArguments_CarriesNoDetail()
    {
        var response = ApiResponse.InternalServerError();

        Assert.Equal(ApiResponseCodes.InternalServerError, response.Code);
        Assert.Equal("服务器内部错误", response.Message);
        Assert.Null(response.Data);
        Assert.Null(response.TraceId);
    }

    /// <summary>
    /// ServiceUnavailable 工厂：允许回传依赖不可达的排查线索
    /// </summary>
    [Fact]
    public void ServiceUnavailable_PutsDependencyHintInData()
    {
        var response = ApiResponse.ServiceUnavailable("缓存依赖不可用");

        Assert.Equal(ApiResponseCodes.ServiceUnavailable, response.Code);
        Assert.Equal("服务不可用", response.Message);
        Assert.Equal("缓存依赖不可用", response.Data);
        Assert.Null(response.TraceId);
    }

    /// <summary>
    /// Failure 工厂遇到已定义的业务码时使用其描述作为提示文案
    /// </summary>
    [Fact]
    public void Failure_WhenCodeDefined_UsesCodeDescription()
    {
        var response = ApiResponse.Failure(ApiResponseCodes.TokenExpired, "刷新令牌也过期了", "trace-004");

        Assert.Equal(ApiResponseCodes.TokenExpired, response.Code);
        Assert.Equal("令牌已过期", response.Message);
        Assert.Equal("刷新令牌也过期了", response.Data);
        Assert.Equal("trace-004", response.TraceId);
        Assert.False(response.IsSuccess);
    }

    /// <summary>
    /// Failure 工厂遇到未定义的数值时回退到通用文案，而不是回显数字
    /// </summary>
    [Fact]
    public void Failure_WhenCodeUndefined_UsesGenericMessage()
    {
        var response = ApiResponse.Failure((ApiResponseCodes)987654);

        Assert.Equal(987654, (int)response.Code);
        Assert.Equal("请求失败", response.Message);
        Assert.Null(response.Data);
        Assert.Null(response.TraceId);
    }

    /// <summary>
    /// 即使全局注册了字符串枚举转换器，Code 仍必须序列化为数字
    /// </summary>
    /// <remarks>
    /// 这是 ApiResponse.Code 上单独标注转换器的唯一理由：
    /// System.Text.Json 的优先级为「属性特性 &gt; options.Converters &gt; 类型特性」，
    /// Web 管道会把 JsonStringEnumConverter 塞进 options.Converters，只靠枚举的类型级标注会被压过。
    /// </remarks>
    [Fact]
    public void Serialize_WithGlobalStringEnumConverter_KeepsCodeNumeric()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(ApiResponse.NotFound(), options);

        Assert.Contains("\"Code\":404", json);
        Assert.DoesNotContain("\"Code\":\"NotFound\"", json);
    }

    /// <summary>
    /// 序列化字段名即对外协议字段名，含只读的 IsSuccess
    /// </summary>
    [Fact]
    public void Serialize_ExposesContractFieldNames()
    {
        var json = JsonSerializer.Serialize(ApiResponse.Success("payload", "trace-005"));

        Assert.Contains("\"Code\":200", json);
        Assert.Contains("\"Message\":", json);
        Assert.Contains("\"Data\":", json);
        Assert.Contains("\"TraceId\":\"trace-005\"", json);
        Assert.Contains("\"Timestamp\":", json);
        Assert.Contains("\"IsSuccess\":true", json);
    }

    /// <summary>
    /// 往返序列化保持返回码、文案与追踪 ID，IsSuccess 由 Code 重新推导
    /// </summary>
    [Fact]
    public void RoundTrip_PreservesEnvelopeFields()
    {
        var original = ApiResponse.BadRequest("缺少参数 id", "trace-006");

        var restored = JsonSerializer.Deserialize<ApiResponse>(JsonSerializer.Serialize(original));

        Assert.NotNull(restored);
        Assert.Equal(ApiResponseCodes.BadRequest, restored!.Code);
        Assert.Equal(original.Message, restored.Message);
        Assert.Equal(original.TraceId, restored.TraceId);
        Assert.NotNull(restored.Data);
        Assert.False(restored.IsSuccess);
    }
}
