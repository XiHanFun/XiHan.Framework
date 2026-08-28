// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Domain.Exceptions;
using XiHan.Framework.Domain.Rules;
using XiHan.Framework.Domain.Services.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Services;

/// <summary>
/// 领域服务基类测试
/// </summary>
/// <remarks>
/// 领域服务的规则校验和性能监控包装器都会先记日志再抛异常，因此必须挂上真实的
/// ITransientCachedServiceProvider（内含 ILoggerFactory），否则 Logger 取值时就会炸。
/// 这里用真实 ServiceCollection + AddLogging 组装，不引入替身框架。
/// </remarks>
public class DomainServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly SampleDomainService _service;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DomainServiceTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();
        _service = new SampleDomainService
        {
            TransientCachedServiceProvider = new TransientCachedServiceProvider(_serviceProvider)
        };
    }

    /// <summary>
    /// 释放服务提供器
    /// </summary>
    public void Dispose()
    {
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 规则未被违反时校验静默通过
    /// </summary>
    [Fact]
    public void CheckBusinessRule_WhenRuleIsSatisfied_DoesNotThrow()
    {
        var rule = new SampleBusinessRule("ok", false);

        _service.RunCheckBusinessRule(rule, "下单");

        Assert.Equal(1, rule.CheckedCount);
    }

    /// <summary>
    /// 规则被违反时把业务规则异常原样抛出
    /// </summary>
    [Fact]
    public void CheckBusinessRule_WhenRuleIsBroken_Rethrows()
    {
        var rule = new SampleBusinessRule("坏了", true);

        var exception = Assert.Throws<BusinessRuleValidationException>(() => _service.RunCheckBusinessRule(rule));

        Assert.Equal("坏了", exception.Message);
    }

    /// <summary>
    /// 规则为空时抛出参数异常
    /// </summary>
    [Fact]
    public void CheckBusinessRule_WhenRuleIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _service.RunCheckBusinessRule(null!));
    }

    /// <summary>
    /// 批量校验把违反项合并为一条消息抛出
    /// </summary>
    [Fact]
    public void CheckBusinessRules_WhenBroken_ThrowsCombinedMessage()
    {
        var rules = new List<IBusinessRule>
        {
            new SampleBusinessRule("坏了一", true),
            new SampleBusinessRule("坏了二", true)
        };

        var exception = Assert.Throws<BusinessRuleValidationException>(() => _service.RunCheckBusinessRules(rules));

        Assert.Equal("坏了一; 坏了二", exception.Message);
    }

    /// <summary>
    /// 规则集合为空引用时抛出参数异常
    /// </summary>
    [Fact]
    public void CheckBusinessRules_WhenCollectionIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _service.RunCheckBusinessRules(null!));
    }

    /// <summary>
    /// 异步校验被违反时抛出业务规则异常
    /// </summary>
    [Fact]
    public async Task CheckBusinessRuleAsync_WhenRuleIsBroken_Throws()
    {
        var rule = new SampleBusinessRule("坏了", true);

        await Assert.ThrowsAsync<BusinessRuleValidationException>(
            () => _service.RunCheckBusinessRuleAsync(rule, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 异步校验在令牌已取消时立即中断
    /// </summary>
    [Fact]
    public async Task CheckBusinessRuleAsync_WhenCancelled_Throws()
    {
        var rule = new SampleBusinessRule("ok", false);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.RunCheckBusinessRuleAsync(rule, cancellation.Token));

        // 取消发生在规则求值之前，规则不应被检查
        Assert.Equal(0, rule.CheckedCount);
    }

    /// <summary>
    /// 异步批量校验在令牌已取消时立即中断
    /// </summary>
    [Fact]
    public async Task CheckBusinessRulesAsync_WhenCancelled_Throws()
    {
        var rules = new List<IBusinessRule> { new SampleBusinessRule("ok", false) };
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.RunCheckBusinessRulesAsync(rules, cancellation.Token));
    }

    /// <summary>
    /// 异步批量校验全部通过时正常完成
    /// </summary>
    [Fact]
    public async Task CheckBusinessRulesAsync_WhenAllSatisfied_Completes()
    {
        var rules = new List<IBusinessRule>
        {
            new SampleBusinessRule("a", false),
            new SampleBusinessRule("b", false)
        };

        await _service.RunCheckBusinessRulesAsync(rules, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 性能监控包装器执行传入的操作
    /// </summary>
    [Fact]
    public void ExecuteWithPerformanceMonitoring_RunsAction()
    {
        var executed = false;

        _service.RunMonitored("op", () => executed = true);

        Assert.True(executed);
    }

    /// <summary>
    /// 性能监控包装器把操作异常原样抛出
    /// </summary>
    [Fact]
    public void ExecuteWithPerformanceMonitoring_WhenActionThrows_Rethrows()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => _service.RunMonitored("op", () => throw new InvalidOperationException("炸了")));

        Assert.Equal("炸了", exception.Message);
    }

    /// <summary>
    /// 带返回值的性能监控包装器透传结果
    /// </summary>
    [Fact]
    public void ExecuteWithPerformanceMonitoring_WithResult_ReturnsValue()
    {
        var result = _service.RunMonitoredResult("op", () => 42);

        Assert.Equal(42, result);
    }

    /// <summary>
    /// 异步性能监控包装器执行传入的异步操作
    /// </summary>
    [Fact]
    public async Task ExecuteWithPerformanceMonitoringAsync_RunsFunc()
    {
        var executed = false;

        await _service.RunMonitoredAsync(
            "op",
            _ =>
            {
                executed = true;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.True(executed);
    }

    /// <summary>
    /// 异步性能监控包装器把取消令牌传给被包装的操作
    /// </summary>
    [Fact]
    public async Task ExecuteWithPerformanceMonitoringAsync_PassesCancellationTokenToFunc()
    {
        using var cancellation = new CancellationTokenSource();
        CancellationToken received = default;

        await _service.RunMonitoredAsync(
            "op",
            token =>
            {
                received = token;
                return Task.CompletedTask;
            },
            cancellation.Token);

        Assert.Equal(cancellation.Token, received);
    }

    /// <summary>
    /// 带返回值的异步性能监控包装器透传结果
    /// </summary>
    [Fact]
    public async Task ExecuteWithPerformanceMonitoringAsync_WithResult_ReturnsValue()
    {
        var result = await _service.RunMonitoredResultAsync(
            "op",
            _ => Task.FromResult("done"),
            TestContext.Current.CancellationToken);

        Assert.Equal("done", result);
    }

    /// <summary>
    /// 异步性能监控包装器把操作异常原样抛出
    /// </summary>
    [Fact]
    public async Task ExecuteWithPerformanceMonitoringAsync_WhenFuncThrows_Rethrows()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RunMonitoredAsync(
                "op",
                _ => Task.FromException(new InvalidOperationException("炸了")),
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 领域服务实现瞬时依赖标记接口，可被自动注册发现
    /// </summary>
    [Fact]
    public void DomainService_ImplementsDomainServiceContract()
    {
        Assert.IsAssignableFrom<IDomainService>(_service);
    }
}
