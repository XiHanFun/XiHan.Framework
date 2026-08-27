// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Enums;
using XiHan.Framework.Upgrade.Models;
using XiHan.Framework.Upgrade.Services;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 升级协调器测试
/// </summary>
/// <remarks>
/// 协调器只负责一件事：把升级放到后台跑，并保证同一时刻只有一个后台任务。
/// 用一个可控闸门的引擎替身把「正在执行」这个时间窗口固定下来，避免依赖 sleep 猜时序。
/// </remarks>
public class UpgradeCoordinatorTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 空闲时启动升级，返回已启动并真的调用了引擎
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenIdle_StartsBackgroundUpgrade()
    {
        var engine = new GatedUpgradeEngine();
        using var provider = BuildProvider(engine);
        var coordinator = CreateCoordinator(provider);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await coordinator.StartAsync();

        Assert.True(result.Started);
        Assert.Equal(UpgradeStatus.Upgrading, result.Status);
        Assert.Equal("升级任务已启动", result.Message);

        await engine.Started.Task.WaitAsync(WaitTimeout, cancellationToken);
        engine.Release();
        await engine.Finished.Task.WaitAsync(WaitTimeout, cancellationToken);
        Assert.Equal(1, engine.ExecuteCount);
    }

    /// <summary>
    /// 升级进行中时再次启动被拒绝，引擎不会被重复调用
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenUpgradeInProgress_RejectsSecondStart()
    {
        var engine = new GatedUpgradeEngine();
        using var provider = BuildProvider(engine);
        var coordinator = CreateCoordinator(provider);
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await coordinator.StartAsync();
        await engine.Started.Task.WaitAsync(WaitTimeout, cancellationToken);
        var second = await coordinator.StartAsync();

        Assert.True(first.Started);
        Assert.False(second.Started);
        Assert.Equal(UpgradeStatus.Upgrading, second.Status);
        Assert.Equal("升级任务正在执行", second.Message);

        engine.Release();
        await engine.Finished.Task.WaitAsync(WaitTimeout, cancellationToken);
        Assert.Equal(1, engine.ExecuteCount);
    }

    /// <summary>
    /// 上一轮升级结束后可以再次启动
    /// </summary>
    [Fact]
    public async Task StartAsync_AfterPreviousRunCompleted_CanStartAgain()
    {
        var engine = new GatedUpgradeEngine();
        using var provider = BuildProvider(engine);
        var coordinator = CreateCoordinator(provider);
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await coordinator.StartAsync();
        Assert.True(first.Started);
        await engine.Started.Task.WaitAsync(WaitTimeout, cancellationToken);
        engine.Release();
        await engine.Finished.Task.WaitAsync(WaitTimeout, cancellationToken);

        var restarted = await WaitForRestartAsync(coordinator);

        Assert.True(restarted.Started);
        Assert.Equal("升级任务已启动", restarted.Message);
    }

    /// <summary>
    /// 引擎抛异常时不冒泡给调用方，后台任务也不会把协调器卡死
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenEngineThrows_SwallowsExceptionAndStaysUsable()
    {
        var engine = new GatedUpgradeEngine { ThrowOnExecute = true };
        using var provider = BuildProvider(engine);
        var coordinator = CreateCoordinator(provider);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await coordinator.StartAsync();

        Assert.True(result.Started);
        await engine.Started.Task.WaitAsync(WaitTimeout, cancellationToken);
        engine.Release();
        await engine.Finished.Task.WaitAsync(WaitTimeout, cancellationToken);

        var restarted = await WaitForRestartAsync(coordinator);
        Assert.True(restarted.Started);
    }

    /// <summary>
    /// 轮询直到协调器允许再次启动（后台任务完成存在极短的收尾窗口）
    /// </summary>
    /// <param name="coordinator">升级协调器</param>
    /// <returns>启动结果</returns>
    private static async Task<UpgradeStartResult> WaitForRestartAsync(UpgradeCoordinator coordinator)
    {
        var result = new UpgradeStartResult();
        for (var attempt = 0; attempt < 200; attempt++)
        {
            result = await coordinator.StartAsync();
            if (result.Started)
            {
                return result;
            }

            await Task.Delay(25);
        }

        return result;
    }

    /// <summary>
    /// 创建协调器
    /// </summary>
    /// <param name="provider">服务提供器</param>
    /// <returns>协调器</returns>
    private static UpgradeCoordinator CreateCoordinator(IServiceProvider provider)
    {
        return new UpgradeCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<UpgradeCoordinator>.Instance);
    }

    /// <summary>
    /// 构建只注册了引擎替身的服务提供器
    /// </summary>
    /// <param name="engine">引擎替身</param>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider(IUpgradeEngine engine)
    {
        var services = new ServiceCollection();
        services.AddSingleton(engine);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 带闸门的升级引擎替身，用于精确控制「执行中」的时间窗口
    /// </summary>
    private sealed class GatedUpgradeEngine : IUpgradeEngine
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _executeCount;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ThrowOnExecute { get; init; }

        public int ExecuteCount => Volatile.Read(ref _executeCount);

        public async Task<UpgradeStartResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _executeCount);
            Started.TrySetResult();

            try
            {
                await _gate.Task;

                if (ThrowOnExecute)
                {
                    throw new InvalidOperationException("模拟升级引擎异常");
                }

                return new UpgradeStartResult
                {
                    Started = true,
                    Status = UpgradeStatus.Completed,
                    Message = "升级完成"
                };
            }
            finally
            {
                Finished.TrySetResult();
            }
        }

        public void Release()
        {
            _gate.TrySetResult();
        }
    }
}
