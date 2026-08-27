// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Models;

/// <summary>
/// 后台作业执行上下文测试
/// </summary>
/// <remarks>
/// 上下文是执行器与 Worker 之间唯一的数据通道：作用域服务提供器、处理器类型、
/// 已反序列化参数、取消令牌四者缺一不可，且全部只读——执行期间被改写会让重试语义失真。
/// </remarks>
public class BackgroundJobExecutionContextTests
{
    /// <summary>
    /// 构造时原样保存四个要素
    /// </summary>
    [Fact]
    public void Constructor_KeepsAllComponents()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var cts = new CancellationTokenSource();
        var args = new NamedJobArgs { Value = "abc", Count = 3 };

        var context = new BackgroundJobExecutionContext(provider, typeof(NamedArgsJob), args, cts.Token);

        Assert.Same(provider, context.ServiceProvider);
        Assert.Equal(typeof(NamedArgsJob), context.JobType);
        Assert.Same(args, context.JobArgs);
        Assert.Equal(cts.Token, context.CancellationToken);
    }

    /// <summary>
    /// 不传取消令牌时为默认令牌（不可取消）
    /// </summary>
    [Fact]
    public void Constructor_WhenTokenOmitted_UsesNoneToken()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var context = new BackgroundJobExecutionContext(provider, typeof(UnnamedArgsJob), new UnnamedJobArgs());

        Assert.Equal(CancellationToken.None, context.CancellationToken);
        Assert.False(context.CancellationToken.CanBeCanceled);
    }

    /// <summary>
    /// 取消令牌被原样传播，源取消后上下文里的令牌同步生效
    /// </summary>
    [Fact]
    public void CancellationToken_WhenSourceCancelled_IsCancelled()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        using var cts = new CancellationTokenSource();
        var context = new BackgroundJobExecutionContext(provider, typeof(UnnamedArgsJob), new UnnamedJobArgs(), cts.Token);

        Assert.False(context.CancellationToken.IsCancellationRequested);

        cts.Cancel();

        Assert.True(context.CancellationToken.IsCancellationRequested);
    }
}
