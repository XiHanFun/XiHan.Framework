// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Services;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 默认升级迁移执行器测试
/// </summary>
/// <remarks>
/// 默认实现是「安全兜底」：没有应用层注册真实执行器时必须显式失败，
/// 绝不能静默把脚本当成执行成功，否则数据库版本会被错误推进。
/// </remarks>
public class DefaultUpgradeMigrationExecutorTests
{
    /// <summary>
    /// 未注册真实执行器时抛出带指引的异常，而不是静默成功
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_Always_ThrowsWithRegistrationHint()
    {
        var executor = new DefaultUpgradeMigrationExecutor();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await executor.ExecuteAsync("select 1", TestContext.Current.CancellationToken));

        Assert.Contains("IUpgradeMigrationExecutor", exception.Message);
    }

    /// <summary>
    /// 空脚本同样抛出，不存在「空脚本视为成功」的后门
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenSqlEmpty_StillThrows()
    {
        var executor = new DefaultUpgradeMigrationExecutor();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await executor.ExecuteAsync(string.Empty, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 取消令牌优先级高于兜底异常
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenTokenCancelled_ThrowsOperationCanceled()
    {
        var executor = new DefaultUpgradeMigrationExecutor();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await executor.ExecuteAsync("select 1", cancellationTokenSource.Token));
    }
}
