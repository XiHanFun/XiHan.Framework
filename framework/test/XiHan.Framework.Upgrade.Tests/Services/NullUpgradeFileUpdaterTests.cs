// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Services;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 程序文件更新空实现测试
/// </summary>
/// <remarks>
/// 空实现必须真的什么都不做：默认配置下引擎不会调用它，一旦被调用也只能留日志，
/// 绝不允许出现「默认实现顺手动了程序文件」这种事故。
/// </remarks>
public class NullUpgradeFileUpdaterTests
{
    /// <summary>
    /// 调用后仅记录一条日志并正常完成
    /// </summary>
    [Fact]
    public async Task ApplyAsync_OnlyLogsAndCompletes()
    {
        var logger = new RecordingLogger<NullUpgradeFileUpdater>();
        var updater = new NullUpgradeFileUpdater(logger);

        await updater.ApplyAsync(TestContext.Current.CancellationToken);

        var message = Assert.Single(logger.Messages);
        Assert.Contains("替换程序文件", message);
    }
}
