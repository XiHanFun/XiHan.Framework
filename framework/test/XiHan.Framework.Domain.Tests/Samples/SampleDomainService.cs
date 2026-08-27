// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Rules;
using XiHan.Framework.Domain.Services;

namespace XiHan.Framework.Domain.Tests.Samples;

/// <summary>
/// 领域服务基类的最小具体子类
/// </summary>
/// <remarks>
/// 基类的规则校验与性能监控方法都是 protected，这里逐个开放为 public 供测试驱动。
/// 方法名刻意区分有无返回值的重载，避免调用点因 lambda 推断而选错重载。
/// </remarks>
public sealed class SampleDomainService : DomainService
{
    /// <summary>
    /// 检查单条业务规则
    /// </summary>
    /// <param name="rule">业务规则</param>
    /// <param name="context">上下文信息</param>
    public void RunCheckBusinessRule(IBusinessRule rule, string? context = null)
    {
        CheckBusinessRule(rule, context);
    }

    /// <summary>
    /// 批量检查业务规则
    /// </summary>
    /// <param name="rules">业务规则集合</param>
    /// <param name="context">上下文信息</param>
    public void RunCheckBusinessRules(IEnumerable<IBusinessRule> rules, string? context = null)
    {
        CheckBusinessRules(rules, context);
    }

    /// <summary>
    /// 异步检查单条业务规则
    /// </summary>
    /// <param name="rule">业务规则</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检查任务</returns>
    public Task RunCheckBusinessRuleAsync(IBusinessRule rule, CancellationToken cancellationToken = default)
    {
        return CheckBusinessRuleAsync(rule, null, cancellationToken);
    }

    /// <summary>
    /// 异步批量检查业务规则
    /// </summary>
    /// <param name="rules">业务规则集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检查任务</returns>
    public Task RunCheckBusinessRulesAsync(IEnumerable<IBusinessRule> rules, CancellationToken cancellationToken = default)
    {
        return CheckBusinessRulesAsync(rules, null, cancellationToken);
    }

    /// <summary>
    /// 带性能监控执行无返回值操作
    /// </summary>
    /// <param name="operation">操作名称</param>
    /// <param name="action">要执行的操作</param>
    public void RunMonitored(string operation, Action action)
    {
        ExecuteWithPerformanceMonitoring(operation, action);
    }

    /// <summary>
    /// 带性能监控执行有返回值操作
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="operation">操作名称</param>
    /// <param name="func">要执行的操作</param>
    /// <returns>操作结果</returns>
    public T RunMonitoredResult<T>(string operation, Func<T> func)
    {
        return ExecuteWithPerformanceMonitoring(operation, func);
    }

    /// <summary>
    /// 带性能监控异步执行无返回值操作
    /// </summary>
    /// <param name="operation">操作名称</param>
    /// <param name="func">要执行的异步操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作任务</returns>
    public Task RunMonitoredAsync(string operation, Func<CancellationToken, Task> func, CancellationToken cancellationToken = default)
    {
        return ExecuteWithPerformanceMonitoringAsync(operation, func, null, cancellationToken);
    }

    /// <summary>
    /// 带性能监控异步执行有返回值操作
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="operation">操作名称</param>
    /// <param name="func">要执行的异步操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public Task<T> RunMonitoredResultAsync<T>(string operation, Func<CancellationToken, Task<T>> func, CancellationToken cancellationToken = default)
    {
        return ExecuteWithPerformanceMonitoringAsync(operation, func, null, cancellationToken);
    }
}
