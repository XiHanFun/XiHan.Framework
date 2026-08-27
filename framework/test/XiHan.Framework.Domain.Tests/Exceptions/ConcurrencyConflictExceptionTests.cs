// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Exceptions;

namespace XiHan.Framework.Domain.Tests.Exceptions;

/// <summary>
/// 乐观并发冲突异常测试
/// </summary>
/// <remarks>
/// 默认消息会直接透出到接口响应给最终用户看，属于对外契约，必须逐字锁死。
/// </remarks>
public class ConcurrencyConflictExceptionTests
{
    /// <summary>
    /// 无参构造使用固定的中文默认提示
    /// </summary>
    [Fact]
    public void Constructor_WithoutArguments_UsesDefaultMessage()
    {
        var exception = new ConcurrencyConflictException();

        Assert.Equal("数据已被其他操作修改，请刷新后重试。", exception.Message);
    }

    /// <summary>
    /// 自定义消息覆盖默认提示
    /// </summary>
    [Fact]
    public void Constructor_WithMessage_OverridesDefaultMessage()
    {
        var exception = new ConcurrencyConflictException("行版本不一致");

        Assert.Equal("行版本不一致", exception.Message);
    }

    /// <summary>
    /// 保留仓储层翻译前的原始 ORM 异常
    /// </summary>
    [Fact]
    public void Constructor_WithInnerException_KeepsInnerException()
    {
        var inner = new InvalidOperationException("version mismatch");

        var exception = new ConcurrencyConflictException("行版本不一致", inner);

        Assert.Equal("行版本不一致", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    /// 并发冲突异常是领域异常的子类
    /// </summary>
    [Fact]
    public void ConcurrencyConflictException_IsDomainException()
    {
        Assert.IsAssignableFrom<DomainException>(new ConcurrencyConflictException());
    }

    /// <summary>
    /// 并发冲突异常不携带错误码与详情
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_LeavesCodeAndDetailsNull()
    {
        var exception = new ConcurrencyConflictException();

        Assert.Null(exception.Code);
        Assert.Null(exception.Details);
    }
}
