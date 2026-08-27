// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Castle.Tests.TestDoubles;

/// <summary>
/// 异步样例服务契约
/// </summary>
/// <remarks>
/// 同时提供"同步完成"和"真异步"两类方法：适配器对 void / 同步返回值走的是
/// <c>GetAwaiter().GetResult()</c> 的阻塞路径，这类用例只能用同步完成的任务，
/// 否则会把测试线程按在阻塞上；Task / Task&lt;T&gt; 走的是全异步路径，才用真异步方法。
/// </remarks>
public interface IAsyncSampleService
{
    /// <summary>
    /// 同步完成的求和
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>已完成的和</returns>
    Task<int> SumAsync(int left, int right);

    /// <summary>
    /// 同步完成的标记方法，无返回值
    /// </summary>
    /// <returns>已完成的任务</returns>
    Task MarkAsync();

    /// <summary>
    /// 真异步的翻倍计算
    /// </summary>
    /// <param name="value">取值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>翻倍结果</returns>
    Task<int> DoubleAsync(int value, CancellationToken cancellationToken);

    /// <summary>
    /// 真异步的无返回值方法
    /// </summary>
    /// <returns>任务</returns>
    Task DelayAsync();

    /// <summary>
    /// 真异步且抛异常的方法，带返回值
    /// </summary>
    /// <returns>永不返回</returns>
    Task<int> FailAsync();

    /// <summary>
    /// 真异步且抛异常的方法，无返回值
    /// </summary>
    /// <returns>永不返回</returns>
    Task FailVoidAsync();

    /// <summary>
    /// 返回 <see cref="ValueTask{TResult}"/> 的方法
    /// </summary>
    /// <param name="value">取值</param>
    /// <returns>三倍结果</returns>
    ValueTask<int> TripleAsync(int value);
}

/// <summary>
/// 异步样例服务
/// </summary>
public sealed class AsyncSampleService : IAsyncSampleService
{
    /// <summary>
    /// 异步方法抛出的异常消息
    /// </summary>
    public const string FailureMessage = "异步样例故意失败";

    /// <summary>
    /// <see cref="SumAsync"/> 是否被真正执行
    /// </summary>
    public bool SumCalled { get; private set; }

    /// <summary>
    /// <see cref="MarkAsync"/> 是否被真正执行
    /// </summary>
    public bool MarkCalled { get; private set; }

    /// <summary>
    /// <see cref="DoubleAsync"/> 是否已跑完整个异步体
    /// </summary>
    public bool DoubleCompleted { get; private set; }

    /// <summary>
    /// <see cref="DelayAsync"/> 是否已跑完整个异步体
    /// </summary>
    public bool DelayCompleted { get; private set; }

    /// <summary>
    /// 同步完成的求和
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>已完成的和</returns>
    public Task<int> SumAsync(int left, int right)
    {
        SumCalled = true;
        return Task.FromResult(left + right);
    }

    /// <summary>
    /// 同步完成的标记方法，无返回值
    /// </summary>
    /// <returns>已完成的任务</returns>
    public Task MarkAsync()
    {
        MarkCalled = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 真异步的翻倍计算
    /// </summary>
    /// <param name="value">取值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>翻倍结果</returns>
    public async Task<int> DoubleAsync(int value, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        DoubleCompleted = true;
        return value * 2;
    }

    /// <summary>
    /// 真异步的无返回值方法
    /// </summary>
    /// <returns>任务</returns>
    public async Task DelayAsync()
    {
        await Task.Yield();
        DelayCompleted = true;
    }

    /// <summary>
    /// 真异步且抛异常的方法，带返回值
    /// </summary>
    /// <returns>永不返回</returns>
    public async Task<int> FailAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException(FailureMessage);
    }

    /// <summary>
    /// 真异步且抛异常的方法，无返回值
    /// </summary>
    /// <returns>永不返回</returns>
    public async Task FailVoidAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException(FailureMessage);
    }

    /// <summary>
    /// 返回 <see cref="ValueTask{TResult}"/> 的方法
    /// </summary>
    /// <param name="value">取值</param>
    /// <returns>三倍结果</returns>
    public ValueTask<int> TripleAsync(int value)
    {
        return ValueTask.FromResult(value * 3);
    }
}
