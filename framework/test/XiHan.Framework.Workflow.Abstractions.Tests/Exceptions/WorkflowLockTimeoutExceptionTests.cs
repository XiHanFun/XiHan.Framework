// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Exceptions;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Exceptions;

/// <summary>
/// 实例锁获取超时异常测试
/// </summary>
/// <remarks>
/// 这是唯一一个"可重试"的工作流异常：调用方靠捕获这个具体类型来决定退避重试还是直接失败，
/// 所以它必须是 <see cref="WorkflowException"/> 的独立子类型，且实例标识要能单独取出来用于日志与重试定位，
/// 不能只藏在消息文本里。
/// </remarks>
public class WorkflowLockTimeoutExceptionTests
{
    /// <summary>
    /// 实例标识单独暴露为属性
    /// </summary>
    [Fact]
    public void Constructor_ExposesInstanceIdAsProperty()
    {
        var exception = new WorkflowLockTimeoutException("ins-1");

        Assert.Equal("ins-1", exception.InstanceId);
    }

    /// <summary>
    /// 消息中包含实例标识便于日志定位
    /// </summary>
    [Fact]
    public void Constructor_MessageContainsInstanceId()
    {
        var exception = new WorkflowLockTimeoutException("ins-42");

        Assert.Contains("ins-42", exception.Message);
        Assert.Contains("执行锁", exception.Message);
    }

    /// <summary>
    /// 继承自工作流异常但与校验异常互不兼容
    /// </summary>
    /// <remarks>
    /// 两者的处理策略相反：锁超时可重试，校验失败必须让用户改定义，
    /// 因此绝不能出现继承关系让 catch 顺序把两者混在一起。
    /// </remarks>
    [Fact]
    public void Type_DerivesFromWorkflowExceptionButNotFromValidationException()
    {
        Assert.True(typeof(WorkflowException).IsAssignableFrom(typeof(WorkflowLockTimeoutException)));
        Assert.False(typeof(WorkflowDefinitionValidationException).IsAssignableFrom(typeof(WorkflowLockTimeoutException)));
        Assert.False(typeof(WorkflowLockTimeoutException).IsAssignableFrom(typeof(WorkflowDefinitionValidationException)));
    }

    /// <summary>
    /// 抛出后可按具体类型捕获并取回实例标识用于重试
    /// </summary>
    [Fact]
    public void Throw_CanBeCaughtByConcreteTypeForRetry()
    {
        WorkflowException? caught = null;

        try
        {
            throw new WorkflowLockTimeoutException("ins-7");
        }
        catch (WorkflowException exception)
        {
            caught = exception;
        }

        var timeout = Assert.IsType<WorkflowLockTimeoutException>(caught);
        Assert.Equal("ins-7", timeout.InstanceId);
    }

    /// <summary>
    /// 锁资源键由通用常量前缀与实例标识拼成
    /// </summary>
    /// <remarks>
    /// 前缀是集群内唯一的锁命名空间，与本异常成对使用：改前缀等于换锁，会让滚动发布期间新旧节点同时拿到锁。
    /// </remarks>
    [Fact]
    public void LockKey_IsPrefixPlusInstanceId()
    {
        var exception = new WorkflowLockTimeoutException("ins-7");

        var lockKey = WorkflowConsts.InstanceLockKeyPrefix + exception.InstanceId;

        Assert.Equal("xihan:workflow:lock:instance:ins-7", lockKey);
    }
}
