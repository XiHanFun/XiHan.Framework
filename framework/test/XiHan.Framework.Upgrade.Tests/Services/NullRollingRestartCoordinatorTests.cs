// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Services;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 滚动重启空实现测试
/// </summary>
/// <remarks>
/// 空实现绝不能真的去重启进程，只能留日志；真实滚动重启由部署侧实现替换。
/// </remarks>
public class NullRollingRestartCoordinatorTests
{
    /// <summary>
    /// 调用后仅记录一条日志并正常完成
    /// </summary>
    [Fact]
    public async Task RestartAsync_OnlyLogsAndCompletes()
    {
        var logger = new RecordingLogger<NullRollingRestartCoordinator>();
        var coordinator = new NullRollingRestartCoordinator(logger);

        await coordinator.RestartAsync(TestContext.Current.CancellationToken);

        var message = Assert.Single(logger.Messages);
        Assert.Contains("滚动重启", message);
    }
}
