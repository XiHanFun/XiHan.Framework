// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;

namespace XiHan.Framework.Application.Contracts.Tests.Dtos;

/// <summary>
/// 泛型统一返回信封测试
/// </summary>
/// <remarks>
/// <see cref="ApiResponse{T}"/> 用 <c>new</c> 遮蔽了基类的 <c>object? Data</c>。
/// 遮蔽而非重写意味着：通过基类引用访问 Data 永远拿不到强类型数据。
/// 这条隐式契约对「先按 ApiResponse 收下再取 Data」的中间件/过滤器是致命的，因此单独锁定。
/// </remarks>
public class ApiResponseGenericTests
{
    /// <summary>
    /// 泛型信封仍是 ApiResponse，可被统一管道按基类处理
    /// </summary>
    [Fact]
    public void GenericEnvelope_IsAssignableToBaseEnvelope()
    {
        Assert.True(typeof(ApiResponse).IsAssignableFrom(typeof(ApiResponse<string>)));
    }

    /// <summary>
    /// Success 工厂填充强类型数据并沿用成功语义
    /// </summary>
    [Fact]
    public void Success_FillsTypedDataAndSuccessSemantics()
    {
        ApiResponse<string> response = ApiResponse<string>.Success("payload", "trace-101");

        Assert.Equal(ApiResponseCodes.Success, response.Code);
        Assert.Equal("请求成功", response.Message);
        Assert.Equal("payload", response.Data);
        Assert.Equal("trace-101", response.TraceId);
        Assert.True(response.IsSuccess);
    }

    /// <summary>
    /// Success 的 traceId 可省略
    /// </summary>
    [Fact]
    public void Success_WithoutTraceId_LeavesTraceIdNull()
    {
        ApiResponse<int> response = ApiResponse<int>.Success(7);

        Assert.Equal(7, response.Data);
        Assert.Null(response.TraceId);
        Assert.True(response.IsSuccess);
    }

    /// <summary>
    /// 强类型 Data 只写进派生属性，基类的 object? Data 仍是 null
    /// </summary>
    /// <remarks>
    /// 这是 <c>new</c> 遮蔽的直接后果：任何把 <see cref="ApiResponse{T}"/> 上转型为
    /// <see cref="ApiResponse"/> 再读 Data 的代码都会拿到 null。
    /// </remarks>
    [Fact]
    public void Success_DoesNotFillShadowedBaseData()
    {
        ApiResponse<string> response = ApiResponse<string>.Success("payload");

        Assert.Equal("payload", response.Data);
        Assert.Null(((ApiResponse)response).Data);
    }

    /// <summary>
    /// 泛型 InternalServerError 省参时数据为类型默认值
    /// </summary>
    [Fact]
    public void InternalServerError_WithoutArguments_DataIsDefault()
    {
        ApiResponse<string> response = ApiResponse<string>.InternalServerError();

        Assert.Equal(ApiResponseCodes.InternalServerError, response.Code);
        Assert.Equal("服务器内部错误", response.Message);
        Assert.Null(response.Data);
        Assert.Null(response.TraceId);
        Assert.False(response.IsSuccess);
    }

    /// <summary>
    /// 泛型 InternalServerError 可把强类型错误明细放进 Data
    /// </summary>
    [Fact]
    public void InternalServerError_WithDetail_CarriesTypedDetail()
    {
        ApiResponse<string> response = ApiResponse<string>.InternalServerError("依赖超时", "trace-102");

        Assert.Equal("依赖超时", response.Data);
        Assert.Equal("trace-102", response.TraceId);
    }

    /// <summary>
    /// 派生 Data 与基类 Data 是两个独立属性，且类型分别为 T 与 object
    /// </summary>
    [Fact]
    public void DataProperty_ShadowsBaseObjectProperty()
    {
        var derived = typeof(ApiResponse<string>)
            .GetProperty("Data", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var baseProperty = typeof(ApiResponse)
            .GetProperty("Data", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotNull(derived);
        Assert.NotNull(baseProperty);
        Assert.Equal(typeof(string), derived!.PropertyType);
        Assert.Equal(typeof(object), baseProperty!.PropertyType);
    }

    /// <summary>
    /// 泛型信封复用基类的 2xx 成功判定
    /// </summary>
    [Fact]
    public void IsSuccess_ReusesBaseRangeRule()
    {
        var response = new ApiResponse<string> { Code = ApiResponseCodes.NoContent };

        Assert.True(response.IsSuccess);

        response.Code = ApiResponseCodes.Conflict;

        Assert.False(response.IsSuccess);
    }
}
