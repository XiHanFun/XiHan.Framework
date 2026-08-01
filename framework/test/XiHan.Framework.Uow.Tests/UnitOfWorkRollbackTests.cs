// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.Uow.Tests;

/// <summary>
/// 工作单元回滚语义的测试
/// </summary>
/// <remarks>
/// 覆盖「内层回滚传导至外层后，外层不得再静默提交」这一契约。
/// </remarks>
public class UnitOfWorkRollbackTests
{
    /// <summary>
    /// 回滚后再提交必须抛出，而不是静默返回
    /// </summary>
    [Fact]
    public async Task CompleteAsync_AfterRollback_Throws()
    {
        var unitOfWork = CreateUnitOfWork();

        await unitOfWork.RollbackAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<XiHanException>(
            () => unitOfWork.CompleteAsync(TestContext.Current.CancellationToken));
        Assert.Contains("已被回滚", exception.Message);
    }

    /// <summary>
    /// 未回滚时提交正常完成
    /// </summary>
    [Fact]
    public async Task CompleteAsync_WithoutRollback_Completes()
    {
        var unitOfWork = CreateUnitOfWork();

        await unitOfWork.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.True(unitOfWork.IsCompleted);
        Assert.False(unitOfWork.IsRolledback);
    }

    /// <summary>
    /// 回滚状态经子工作单元传导至外层
    /// </summary>
    [Fact]
    public async Task ChildRollback_PropagatesToParent()
    {
        var parent = CreateUnitOfWork();
        var child = new ChildUnitOfWork(parent);

        await child.RollbackAsync(TestContext.Current.CancellationToken);

        Assert.True(parent.IsRolledback);
        Assert.True(child.IsRolledback);
    }

    /// <summary>
    /// 内层回滚后外层提交必须抛出
    /// </summary>
    [Fact]
    public async Task ParentComplete_AfterChildRollback_Throws()
    {
        var parent = CreateUnitOfWork();
        var child = new ChildUnitOfWork(parent);

        await child.RollbackAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<XiHanException>(
            () => parent.CompleteAsync(TestContext.Current.CancellationToken));
        Assert.False(parent.IsCompleted);
    }

    /// <summary>
    /// 子工作单元提交不推进父工作单元的完成状态
    /// </summary>
    [Fact]
    public async Task ChildComplete_DoesNotCompleteParent()
    {
        var parent = CreateUnitOfWork();
        var child = new ChildUnitOfWork(parent);

        await child.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.False(parent.IsCompleted);
    }

    /// <summary>
    /// 重复回滚是幂等的
    /// </summary>
    [Fact]
    public async Task RollbackAsync_IsIdempotent()
    {
        var unitOfWork = CreateUnitOfWork();

        await unitOfWork.RollbackAsync(TestContext.Current.CancellationToken);
        await unitOfWork.RollbackAsync(TestContext.Current.CancellationToken);

        Assert.True(unitOfWork.IsRolledback);
    }

    /// <summary>
    /// 创建一个已初始化的工作单元
    /// </summary>
    /// <returns>工作单元</returns>
    private static UnitOfWork CreateUnitOfWork()
    {
        var unitOfWork = new UnitOfWork(
            new EmptyServiceProvider(),
            new NullUnitOfWorkEventPublisher(),
            Microsoft.Extensions.Options.Options.Create(new XiHanUnitOfWorkDefaultOptions()),
            NullLogger<UnitOfWork>.Instance);

        unitOfWork.Initialize(new XiHanUnitOfWorkOptions());

        return unitOfWork;
    }
}

/// <summary>
/// 不解析任何服务的服务提供器
/// </summary>
public class EmptyServiceProvider : IServiceProvider
{
    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <returns>始终为空</returns>
    public object? GetService(Type serviceType) => null;
}
