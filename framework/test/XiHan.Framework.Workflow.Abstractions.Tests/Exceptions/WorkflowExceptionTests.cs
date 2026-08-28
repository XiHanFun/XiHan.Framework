// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Exceptions;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Exceptions;

/// <summary>
/// 工作流异常基类测试
/// </summary>
/// <remarks>
/// 该基类是引擎协议错误的统一捕获点，两条契约必须锁死：
/// 消息原样透出（不加任何前缀，调用方会把它直接回给接口调用者），以及内部异常可选链接。
/// </remarks>
public class WorkflowExceptionTests
{
    /// <summary>
    /// 单参构造原样保留消息且无内部异常
    /// </summary>
    [Fact]
    public void Constructor_WithMessage_KeepsMessageVerbatim()
    {
        var exception = new WorkflowException("定义未发布");

        Assert.Equal("定义未发布", exception.Message);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    /// 双参构造保留内部异常链接
    /// </summary>
    [Fact]
    public void Constructor_WithInnerException_KeepsBoth()
    {
        var inner = new InvalidOperationException("底层失败");

        var exception = new WorkflowException("恢复书签失败", inner);

        Assert.Equal("恢复书签失败", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    /// 内部异常允许显式传空
    /// </summary>
    [Fact]
    public void Constructor_WithNullInnerException_IsAllowed()
    {
        var exception = new WorkflowException("表达式非法", null);

        Assert.Equal("表达式非法", exception.Message);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    /// 继承自系统异常基类，可被通用异常中间件捕获
    /// </summary>
    [Fact]
    public void Type_DerivesFromException()
    {
        Assert.True(typeof(Exception).IsAssignableFrom(typeof(WorkflowException)));
    }

    /// <summary>
    /// 抛出后可按基类型捕获
    /// </summary>
    [Fact]
    public void Throw_CanBeCaughtAsWorkflowException()
    {
        var exception = Assert.Throws<WorkflowException>(ThrowConsumedBookmark);

        Assert.Equal("书签已消费", exception.Message);
    }

    /// <summary>
    /// 抛出一个书签已消费的工作流异常
    /// </summary>
    private static void ThrowConsumedBookmark()
    {
        throw new WorkflowException("书签已消费");
    }
}
