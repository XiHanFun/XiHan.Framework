#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DomainService
// Guid:9371cd5b-c0c1-4907-af52-3c9ec9e371c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 5:54:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Domain.Rules;

namespace XiHan.Framework.Domain.Services;

/// <summary>
/// 领域服务基类
/// 提供通用的领域服务功能
/// </summary>
public abstract class DomainService : IDomainService
{
    /// <summary>
    /// 领域服务提供者
    /// </summary>
    public ITransientCachedServiceProvider TransientCachedServiceProvider { get; set; } = default!;

    /// <summary>
    /// 日志工厂
    /// </summary>
    protected ILoggerFactory LoggerFactory => TransientCachedServiceProvider.GetRequiredService<ILoggerFactory>();

    /// <summary>
    /// 日志记录器
    /// </summary>
    protected ILogger Logger => TransientCachedServiceProvider.GetService<ILogger>(provider => LoggerFactory?.CreateLogger(GetType().FullName!) ?? NullLogger.Instance);

    /// <summary>
    /// 检查业务规则
    /// </summary>
    /// <param name="rule">业务规则</param>
    /// <param name="context">上下文信息（可选）</param>
    /// <exception cref="ArgumentNullException">当规则为空时抛出</exception>
    protected virtual void CheckBusinessRule(IBusinessRule rule, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(rule);

        try
        {
            rule.CheckRule();
            Logger.LogDebug("Business rule validated successfully: {RuleType} {Context}", 
                rule.GetType().Name, context ?? string.Empty);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Business rule validation failed: {RuleType} {Context} - {Message}", 
                rule.GetType().Name, context ?? string.Empty, rule.Message);
            throw;
        }
    }

    /// <summary>
    /// 批量检查业务规则
    /// </summary>
    /// <param name="rules">业务规则集合</param>
    /// <param name="context">上下文信息（可选）</param>
    /// <exception cref="ArgumentNullException">当规则集合为空时抛出</exception>
    protected virtual void CheckBusinessRules(IEnumerable<IBusinessRule> rules, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var ruleList = rules.ToList();
        Logger.LogDebug("Validating {RuleCount} business rules {Context}", 
            ruleList.Count, context ?? string.Empty);

        try
        {
            ruleList.CheckRules();
            Logger.LogDebug("All business rules validated successfully {Context}", context ?? string.Empty);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Business rules validation failed {Context}", context ?? string.Empty);
            throw;
        }
    }

    /// <summary>
    /// 异步检查业务规则
    /// </summary>
    /// <param name="rule">业务规则</param>
    /// <param name="context">上下文信息（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检查任务</returns>
    /// <exception cref="ArgumentNullException">当规则为空时抛出</exception>
    protected virtual async Task CheckBusinessRuleAsync(
        IBusinessRule rule, 
        string? context = null, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await rule.CheckRuleAsync();
            Logger.LogDebug("Business rule validated successfully: {RuleType} {Context}", 
                rule.GetType().Name, context ?? string.Empty);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Business rule validation failed: {RuleType} {Context} - {Message}", 
                rule.GetType().Name, context ?? string.Empty, rule.Message);
            throw;
        }
    }

    /// <summary>
    /// 异步批量检查业务规则
    /// </summary>
    /// <param name="rules">业务规则集合</param>
    /// <param name="context">上下文信息（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检查任务</returns>
    /// <exception cref="ArgumentNullException">当规则集合为空时抛出</exception>
    protected virtual async Task CheckBusinessRulesAsync(
        IEnumerable<IBusinessRule> rules, 
        string? context = null, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        cancellationToken.ThrowIfCancellationRequested();

        var ruleList = rules.ToList();
        Logger.LogDebug("Validating {RuleCount} business rules {Context}", 
            ruleList.Count, context ?? string.Empty);

        try
        {
            await ruleList.CheckRulesAsync();
            Logger.LogDebug("All business rules validated successfully {Context}", context ?? string.Empty);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Business rules validation failed {Context}", context ?? string.Empty);
            throw;
        }
    }

    /// <summary>
    /// 记录领域服务操作
    /// </summary>
    /// <param name="operation">操作名称</param>
    /// <param name="parameters">操作参数（可选）</param>
    protected virtual void LogDomainOperation(string operation, object? parameters = null)
    {
        if (parameters is null)
        {
            Logger.LogInformation("Domain service operation: {Operation}", operation);
        }
        else
        {
            Logger.LogInformation("Domain service operation: {Operation} with parameters: {@Parameters}", 
                operation, parameters);
        }
    }

    /// <summary>
    /// 记录领域服务操作完成
    /// </summary>
    /// <param name="operation">操作名称</param>
    /// <param name="duration">操作耗时</param>
    /// <param name="result">操作结果（可选）</param>
    protected virtual void LogDomainOperationCompleted(string operation, TimeSpan duration, object? result = null)
    {
        if (result is null)
        {
            Logger.LogInformation("Domain service operation completed: {Operation} in {Duration}ms", 
                operation, duration.TotalMilliseconds);
        }
        else
        {
            Logger.LogInformation("Domain service operation completed: {Operation} in {Duration}ms with result: {@Result}", 
                operation, duration.TotalMilliseconds, result);
        }
    }

    /// <summary>
    /// 使用性能监控执行操作
    /// </summary>
    /// <param name="operation">操作名称</param>
    /// <param name="action">要执行的操作</param>
    /// <param name="parameters">操作参数（可选）</param>
    protected virtual void ExecuteWithPerformanceMonitoring(string operation, Action action, object? parameters = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            LogDomainOperation(operation, parameters);
            action();
            stopwatch.Stop();
            LogDomainOperationCompleted(operation, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Domain service operation failed: {Operation} after {Duration}ms", 
                operation, stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// 使用性能监控执行操作并返回结果
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="operation">操作名称</param>
    /// <param name="func">要执行的操作</param>
    /// <param name="parameters">操作参数（可选）</param>
    /// <returns>操作结果</returns>
    protected virtual T ExecuteWithPerformanceMonitoring<T>(string operation, Func<T> func, object? parameters = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            LogDomainOperation(operation, parameters);
            var result = func();
            stopwatch.Stop();
            LogDomainOperationCompleted(operation, stopwatch.Elapsed, result);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Domain service operation failed: {Operation} after {Duration}ms", 
                operation, stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// 使用性能监控异步执行操作
    /// </summary>
    /// <param name="operation">操作名称</param>
    /// <param name="func">要执行的异步操作</param>
    /// <param name="parameters">操作参数（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作任务</returns>
    protected virtual async Task ExecuteWithPerformanceMonitoringAsync(
        string operation, 
        Func<CancellationToken, Task> func, 
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            LogDomainOperation(operation, parameters);
            await func(cancellationToken);
            stopwatch.Stop();
            LogDomainOperationCompleted(operation, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Domain service operation failed: {Operation} after {Duration}ms", 
                operation, stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// 使用性能监控异步执行操作并返回结果
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="operation">操作名称</param>
    /// <param name="func">要执行的异步操作</param>
    /// <param name="parameters">操作参数（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    protected virtual async Task<T> ExecuteWithPerformanceMonitoringAsync<T>(
        string operation, 
        Func<CancellationToken, Task<T>> func, 
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            LogDomainOperation(operation, parameters);
            var result = await func(cancellationToken);
            stopwatch.Stop();
            LogDomainOperationCompleted(operation, stopwatch.Elapsed, result);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Domain service operation failed: {Operation} after {Duration}ms", 
                operation, stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}
