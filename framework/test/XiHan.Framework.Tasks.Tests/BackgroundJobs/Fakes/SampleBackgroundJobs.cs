// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Attributes;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

/// <summary>
/// 带稳定名称标注的作业参数
/// </summary>
[BackgroundJobName("xihan-tests-named-args")]
public class NamedJobArgs
{
    /// <summary>
    /// 文本值
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 数量
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// 继承自带标注参数的派生参数（用于验证特性不被继承）
/// </summary>
public class DerivedNamedJobArgs : NamedJobArgs
{
}

/// <summary>
/// 未标注名称的作业参数
/// </summary>
public class UnnamedJobArgs
{
    /// <summary>
    /// 文本值
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 处理带名称参数的作业处理器
/// </summary>
public sealed class NamedArgsJob : AsyncBackgroundJob<NamedJobArgs>
{
    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="args">作业参数</param>
    /// <returns>任务</returns>
    public override Task ExecuteAsync(NamedJobArgs args)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 同一参数类型的另一个处理器（用于验证注册表按参数类型覆盖）
/// </summary>
public sealed class AlternateNamedArgsJob : IAsyncBackgroundJob<NamedJobArgs>
{
    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="args">作业参数</param>
    /// <returns>任务</returns>
    public Task ExecuteAsync(NamedJobArgs args)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 处理未标注参数的作业处理器
/// </summary>
public sealed class UnnamedArgsJob : AsyncBackgroundJob<UnnamedJobArgs>
{
    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="args">作业参数</param>
    /// <returns>任务</returns>
    public override Task ExecuteAsync(UnnamedJobArgs args)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 记录收到参数的作业处理器
/// </summary>
public sealed class RecordingNamedArgsJob : IAsyncBackgroundJob<NamedJobArgs>
{
    private readonly List<NamedJobArgs> _executed = [];

    /// <summary>
    /// 收到过的参数
    /// </summary>
    public IReadOnlyList<NamedJobArgs> Executed => _executed;

    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="args">作业参数</param>
    /// <returns>任务</returns>
    public Task ExecuteAsync(NamedJobArgs args)
    {
        _executed.Add(args);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 异步抛异常的作业处理器（异常由返回的 Task 承载）
/// </summary>
public sealed class AsyncThrowingJob : IAsyncBackgroundJob<UnnamedJobArgs>
{
    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="args">作业参数</param>
    /// <returns>任务</returns>
    public async Task ExecuteAsync(UnnamedJobArgs args)
    {
        await Task.Yield();
        throw new InvalidOperationException("异步作业内部失败");
    }
}

/// <summary>
/// 同步抛异常的作业处理器（反射调用时抛 TargetInvocationException）
/// </summary>
public sealed class SyncThrowingJob : IAsyncBackgroundJob<UnnamedJobArgs>
{
    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="args">作业参数</param>
    /// <returns>任务</returns>
    public Task ExecuteAsync(UnnamedJobArgs args)
    {
        throw new NotSupportedException("同步作业立即失败");
    }
}

/// <summary>
/// 开放泛型作业处理器（用于验证泛型定义不被当作可用作业）
/// </summary>
/// <typeparam name="TArgs">作业参数类型</typeparam>
public sealed class OpenGenericJob<TArgs> : IAsyncBackgroundJob<TArgs>
{
    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="args">作业参数</param>
    /// <returns>任务</returns>
    public Task ExecuteAsync(TArgs args)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 抽象作业处理器（用于验证抽象类型不被当作可用作业）
/// </summary>
public abstract class AbstractSampleJob : IAsyncBackgroundJob<UnnamedJobArgs>
{
    /// <summary>
    /// 执行作业
    /// </summary>
    /// <param name="args">作业参数</param>
    /// <returns>任务</returns>
    public abstract Task ExecuteAsync(UnnamedJobArgs args);
}

/// <summary>
/// 完全不是作业的普通类型
/// </summary>
public sealed class NotABackgroundJob
{
    /// <summary>
    /// 无关的属性
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
