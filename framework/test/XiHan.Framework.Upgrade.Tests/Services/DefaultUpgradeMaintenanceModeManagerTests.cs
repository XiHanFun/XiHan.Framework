// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Services;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 默认维护模式管理器测试
/// </summary>
/// <remarks>
/// 默认实现刻意不阻断流量（只记日志），因为真正的维护模式需要接入网关。
/// 这里锁住「不抛异常、可重复进出、有明确日志痕迹」的兜底契约。
/// </remarks>
public class DefaultUpgradeMaintenanceModeManagerTests
{
    /// <summary>
    /// 进出维护模式都会留下可追溯的日志
    /// </summary>
    [Fact]
    public async Task EnterAndExit_LogInformationForEachTransition()
    {
        var logger = new RecordingLogger<DefaultUpgradeMaintenanceModeManager>();
        var manager = new DefaultUpgradeMaintenanceModeManager(logger);
        var cancellationToken = TestContext.Current.CancellationToken;

        await manager.EnterAsync(cancellationToken);
        await manager.ExitAsync(cancellationToken);

        Assert.Equal(2, logger.Messages.Count);
        Assert.Contains("进入维护模式", logger.Messages[0]);
        Assert.Contains("退出维护模式", logger.Messages[1]);
    }

    /// <summary>
    /// 重复进出不抛异常，失败回滚路径会二次调用退出
    /// </summary>
    [Fact]
    public async Task ExitAsync_CalledTwice_DoesNotThrow()
    {
        var logger = new RecordingLogger<DefaultUpgradeMaintenanceModeManager>();
        var manager = new DefaultUpgradeMaintenanceModeManager(logger);
        var cancellationToken = TestContext.Current.CancellationToken;

        await manager.ExitAsync(cancellationToken);
        await manager.ExitAsync(cancellationToken);

        Assert.Equal(2, logger.Messages.Count);
    }
}
