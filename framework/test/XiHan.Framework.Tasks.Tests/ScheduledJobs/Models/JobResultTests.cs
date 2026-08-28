// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Models;

/// <summary>
/// JobResult 执行结果测试
/// </summary>
/// <remarks>
/// 三个工厂方法决定了 IsSuccess 与 Status 的配对关系，中间件与执行器都靠这对组合分流，
/// 一旦错配会导致失败任务被记成成功，因此逐一钉死。
/// </remarks>
public class JobResultTests
{
    /// <summary>
    /// 无参成功结果：成功标记与成功状态配对，其余字段为空
    /// </summary>
    [Fact]
    public void Success_WithoutArguments_MarksSucceededWithEmptyPayload()
    {
        var result = JobResult.Success();

        Assert.True(result.IsSuccess);
        Assert.Equal(JobStatus.Succeeded, result.Status);
        Assert.Null(result.Data);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Exception);
        Assert.Equal(TimeSpan.Zero, result.Duration);
        Assert.Equal(0, result.RetryCount);
    }

    /// <summary>
    /// 带数据与耗时的成功结果原样保留入参
    /// </summary>
    [Fact]
    public void Success_WithDataAndDuration_KeepsBothValues()
    {
        var payload = new { Rows = 12 };

        var result = JobResult.Success(payload, TimeSpan.FromSeconds(3));

        Assert.True(result.IsSuccess);
        Assert.Same(payload, result.Data);
        Assert.Equal(TimeSpan.FromSeconds(3), result.Duration);
    }

    /// <summary>
    /// 失败结果：失败标记与失败状态配对，携带错误信息
    /// </summary>
    [Fact]
    public void Failure_WithMessageOnly_MarksFailedAndKeepsMessage()
    {
        var result = JobResult.Failure("下游不可用");

        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Failed, result.Status);
        Assert.Equal("下游不可用", result.ErrorMessage);
        Assert.Null(result.Exception);
        Assert.Equal(TimeSpan.Zero, result.Duration);
    }

    /// <summary>
    /// 失败结果可携带原始异常与耗时，便于上层排障
    /// </summary>
    [Fact]
    public void Failure_WithExceptionAndDuration_KeepsBothValues()
    {
        var exception = new InvalidOperationException("炸了");

        var result = JobResult.Failure("下游不可用", exception, TimeSpan.FromMilliseconds(250));

        Assert.Same(exception, result.Exception);
        Assert.Equal(TimeSpan.FromMilliseconds(250), result.Duration);
    }

    /// <summary>
    /// 取消结果不算成功，但状态与失败区分开
    /// </summary>
    [Fact]
    public void Canceled_IsNotSuccessButDistinctFromFailure()
    {
        var result = JobResult.Canceled();

        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Canceled, result.Status);
        Assert.NotEqual(JobStatus.Failed, result.Status);
        Assert.Equal("任务已取消", result.ErrorMessage);
        Assert.Null(result.Exception);
        Assert.Equal(TimeSpan.Zero, result.Duration);
    }

    /// <summary>
    /// 取消结果可携带耗时
    /// </summary>
    [Fact]
    public void Canceled_WithDuration_KeepsIt()
    {
        var result = JobResult.Canceled(TimeSpan.FromSeconds(7));

        Assert.Equal(TimeSpan.FromSeconds(7), result.Duration);
    }

    /// <summary>
    /// 每次调用工厂方法都返回全新实例
    /// </summary>
    [Fact]
    public void Factories_ReturnNewInstancesEveryTime()
    {
        Assert.NotSame(JobResult.Success(), JobResult.Success());
        Assert.NotSame(JobResult.Failure("x"), JobResult.Failure("x"));
        Assert.NotSame(JobResult.Canceled(), JobResult.Canceled());
    }

    /// <summary>
    /// 直接 new 出来的结果是"未成功 + 等待中"的中性状态
    /// </summary>
    [Fact]
    public void Constructor_Default_IsNeutralPendingState()
    {
        var result = new JobResult();

        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Pending, result.Status);
        Assert.Null(result.Data);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Exception);
        Assert.Equal(TimeSpan.Zero, result.Duration);
        Assert.Equal(0, result.RetryCount);
    }
}
