// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs;

/// <summary>
/// 后台作业执行异常测试
/// </summary>
/// <remarks>
/// 这个异常类型本身就是协议：Worker 只对它做"退避重试"，其它异常一律当致命错误直接放弃。
/// 所以它必须能承载原始异常（便于定位真实故障）并保持公共可继承，不能被收窄成内部类型。
/// </remarks>
public class BackgroundJobExecutionExceptionTests
{
    /// <summary>
    /// 无参构造时消息由基类给出，作业信息为空
    /// </summary>
    [Fact]
    public void Constructor_Parameterless_HasNoJobInfo()
    {
        var exception = new BackgroundJobExecutionException();

        Assert.Null(exception.JobName);
        Assert.Null(exception.JobArgs);
        Assert.Null(exception.InnerException);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    /// <summary>
    /// 带消息构造时内部异常可省略
    /// </summary>
    [Fact]
    public void Constructor_WithMessageOnly_KeepsMessageAndNoInnerException()
    {
        var exception = new BackgroundJobExecutionException("作业执行失败");

        Assert.Equal("作业执行失败", exception.Message);
        Assert.Null(exception.InnerException);
    }

    /// <summary>
    /// 带内部异常构造时原始异常被完整保留
    /// </summary>
    [Fact]
    public void Constructor_WithInnerException_KeepsOriginalCause()
    {
        var cause = new TimeoutException("下游超时");

        var exception = new BackgroundJobExecutionException("作业执行失败", cause);

        Assert.Equal("作业执行失败", exception.Message);
        Assert.Same(cause, exception.InnerException);
    }

    /// <summary>
    /// 作业名与作业参数可在抛出前补写，便于日志定位
    /// </summary>
    [Fact]
    public void JobNameAndJobArgs_AreAssignable()
    {
        var exception = new BackgroundJobExecutionException("失败")
        {
            JobName = "order-created",
            JobArgs = """{"OrderId":1}"""
        };

        Assert.Equal("order-created", exception.JobName);
        Assert.Equal("""{"OrderId":1}""", exception.JobArgs);
    }

    /// <summary>
    /// 继承自 Exception，可被通用异常处理捕获
    /// </summary>
    [Fact]
    public void Type_DerivesFromException()
    {
        Assert.True(typeof(Exception).IsAssignableFrom(typeof(BackgroundJobExecutionException)));
        Assert.True(typeof(BackgroundJobExecutionException).IsPublic);
    }
}
