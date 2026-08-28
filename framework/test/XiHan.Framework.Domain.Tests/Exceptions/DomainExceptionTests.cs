// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Exceptions;

namespace XiHan.Framework.Domain.Tests.Exceptions;

/// <summary>
/// 领域异常基类测试
/// </summary>
/// <remarks>
/// 错误码与详情是可选的旁路信息，ToString 的拼装顺序被日志与接口错误响应依赖，需要锁死。
/// </remarks>
public class DomainExceptionTests
{
    /// <summary>
    /// 无参构造不带详情与错误码
    /// </summary>
    [Fact]
    public void Constructor_WithoutArguments_LeavesDetailsAndCodeNull()
    {
        var exception = new DomainException();

        Assert.Null(exception.Details);
        Assert.Null(exception.Code);
    }

    /// <summary>
    /// 单参构造只写消息
    /// </summary>
    [Fact]
    public void Constructor_WithMessage_SetsMessageOnly()
    {
        var exception = new DomainException("订单不存在");

        Assert.Equal("订单不存在", exception.Message);
        Assert.Null(exception.Details);
        Assert.Null(exception.Code);
    }

    /// <summary>
    /// 消息加内部异常构造保留内部异常
    /// </summary>
    [Fact]
    public void Constructor_WithInnerException_KeepsInnerException()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new DomainException("外层失败", inner);

        Assert.Equal("外层失败", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    /// 消息加详情构造写入详情
    /// </summary>
    [Fact]
    public void Constructor_WithDetails_SetsDetails()
    {
        var exception = new DomainException("订单不存在", "orderId=1");

        Assert.Equal("订单不存在", exception.Message);
        Assert.Equal("orderId=1", exception.Details);
    }

    /// <summary>
    /// 详情与错误码可后置写入
    /// </summary>
    [Fact]
    public void DetailsAndCode_AreWritable()
    {
        var exception = new DomainException("失败")
        {
            Details = "detail",
            Code = "D001"
        };

        Assert.Equal("detail", exception.Details);
        Assert.Equal("D001", exception.Code);
    }

    /// <summary>
    /// 工厂方法组装消息、错误码与详情
    /// </summary>
    [Fact]
    public void Create_WithAllArguments_BuildsFullException()
    {
        var exception = DomainException.Create("失败", "D002", "detail");

        Assert.Equal("失败", exception.Message);
        Assert.Equal("D002", exception.Code);
        Assert.Equal("detail", exception.Details);
    }

    /// <summary>
    /// 工厂方法的错误码与详情均可省略
    /// </summary>
    [Fact]
    public void Create_WithMessageOnly_LeavesOptionalFieldsNull()
    {
        var exception = DomainException.Create("失败");

        Assert.Equal("失败", exception.Message);
        Assert.Null(exception.Code);
        Assert.Null(exception.Details);
    }

    /// <summary>
    /// 字符串表示按「错误码在前、详情在后」包裹基类输出
    /// </summary>
    [Fact]
    public void ToString_WithCodeAndDetails_WrapsBaseOutput()
    {
        var exception = DomainException.Create("失败", "D003", "detail");

        var text = exception.ToString();

        Assert.StartsWith("Code: D003", text, StringComparison.Ordinal);
        Assert.EndsWith("Details: detail", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 无错误码与详情时字符串表示退化为基类输出
    /// </summary>
    [Fact]
    public void ToString_WithoutCodeAndDetails_FallsBackToBaseOutput()
    {
        var exception = new DomainException("失败");

        var text = exception.ToString();

        Assert.DoesNotContain("Code:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Details:", text, StringComparison.Ordinal);
        Assert.Contains("失败", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 领域异常是普通异常，可被通用异常处理捕获
    /// </summary>
    [Fact]
    public void DomainException_IsSystemException()
    {
        Assert.IsAssignableFrom<Exception>(new DomainException("失败"));
    }
}
