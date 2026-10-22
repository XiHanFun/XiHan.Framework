// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Authorization.Conditions;
using XiHan.Framework.Authorization.Extensions.DependencyInjection;
using static XiHan.Framework.Authorization.Tests.Infrastructure.ConditionTestKit;

namespace XiHan.Framework.Authorization.Tests.Conditions;

/// <summary>
/// 条件模块的契约：运算符整数值、属性取值包装、记忆来源与服务注册
/// </summary>
public class ConditionContractTests
{
    /// <summary>
    /// 运算符整数值是持久化契约，应用把它存进库里，不得调整
    /// </summary>
    [Fact]
    public void ConditionOperator_IntegerValues_AreFixed()
    {
        ConditionOperator[] expected =
        [
            ConditionOperator.Equals, ConditionOperator.NotEquals, ConditionOperator.GreaterThan, ConditionOperator.GreaterThanOrEquals,
            ConditionOperator.LessThan, ConditionOperator.LessThanOrEquals, ConditionOperator.Contains, ConditionOperator.NotContains,
            ConditionOperator.In, ConditionOperator.NotIn, ConditionOperator.Between, ConditionOperator.StartsWith,
            ConditionOperator.EndsWith, ConditionOperator.IsNull, ConditionOperator.IsNotNull
        ];

        Assert.Equal(Enumerable.Range(0, expected.Length), expected.Select(item => (int)item));
        Assert.Equal(expected.Length, Enum.GetValues<ConditionOperator>().Length);
    }

    /// <summary>
    /// 取值包装：null 是取不到；字符串不展开；可枚举对象展开且丢掉 null 元素
    /// </summary>
    [Fact]
    public void ConditionAttributeValue_Of()
    {
        Assert.Same(ConditionAttributeValue.Missing, ConditionAttributeValue.Of(null));
        Assert.Equal(["abc"], ConditionAttributeValue.Of("abc").Values);
        Assert.Equal(["a", "b"], ConditionAttributeValue.Of(new[] { "a", null, "b" }).Values);
        Assert.Equal(ConditionAttributeValueState.Present, ConditionAttributeValue.Of(Array.Empty<string>()).State);
        Assert.Equal([42], ConditionAttributeValue.Of(42).Values);
    }

    /// <summary>
    /// 记忆来源：同一属性只向内层取一次（不区分大小写），暂不可得不记忆
    /// </summary>
    [Fact]
    public async Task MemoizingSource_CachesExceptDeferred()
    {
        var calls = new List<string>();
        var deferred = true;
        var inner = new DelegateSource(name =>
        {
            calls.Add(name);
            return name == "resource.amount"
                ? deferred ? ConditionAttributeValue.Deferred : ConditionAttributeValue.Of(5)
                : ConditionAttributeValue.Of("admin");
        });
        var source = new MemoizingConditionAttributeSource(inner);

        await source.GetAsync("subject.userName");
        await source.GetAsync("SUBJECT.USERNAME");
        Assert.Equal(ConditionAttributeValueState.Deferred, (await source.GetAsync("resource.amount")).State);
        deferred = false;
        Assert.Equal([5], (await source.GetAsync("resource.amount")).Values);
        await source.GetAsync("resource.amount");

        Assert.Equal(["subject.userName", "resource.amount", "resource.amount"], calls);
    }

    /// <summary>
    /// 注册：登记表、编译器、求值器都是单例；AddXiHanConditionAttributes 追加的登记项进登记表
    /// </summary>
    [Fact]
    public void Services_AreRegisteredAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddXiHanAuthorization(new ConfigurationBuilder().Build());
        services.AddXiHanConditionAttributes(Attributes);
        services.AddXiHanConditionAttributes(new ConditionAttributeDescriptor("environment.weekday", ConditionValueKind.Number));

        Assert.Equal(ServiceLifetime.Singleton, services.Single(descriptor => descriptor.ServiceType == typeof(IConditionAttributeRegistry)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, services.Single(descriptor => descriptor.ServiceType == typeof(IConditionCompiler)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, services.Single(descriptor => descriptor.ServiceType == typeof(IConditionEvaluator)).Lifetime);

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IConditionAttributeRegistry>();
        Assert.True(registry.TryResolve("environment.weekday", out _));
        Assert.True(registry.TryResolve("subject.role", out _));
        Assert.Same(registry, provider.GetRequiredService<IConditionAttributeRegistry>());
        Assert.Empty(provider.GetRequiredService<IConditionCompiler>()
            .Compile(Single(new ConditionClause("environment.weekday", ConditionOperator.In, "1,2,3,4,5"))).Errors);
    }

    /// <summary>
    /// 宿主先注册的实现优先，重复调用不重复注册
    /// </summary>
    [Fact]
    public void Services_TryAddSemantics()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConditionEvaluator, StubEvaluator>();
        services.AddXiHanAuthorization(new ConfigurationBuilder().Build());
        services.AddXiHanAuthorization(new ConfigurationBuilder().Build());

        Assert.Equal(typeof(StubEvaluator), services.Single(descriptor => descriptor.ServiceType == typeof(IConditionEvaluator)).ImplementationType);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IConditionCompiler));
    }

    /// <summary>
    /// 以委托取值的来源
    /// </summary>
    private sealed class DelegateSource(Func<string, ConditionAttributeValue> getter) : IConditionAttributeSource
    {
        public ValueTask<ConditionAttributeValue> GetAsync(string canonicalName, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(getter(canonicalName));
        }
    }

    /// <summary>
    /// 宿主自带的求值器
    /// </summary>
    private sealed class StubEvaluator : IConditionEvaluator
    {
        public ValueTask<ConditionEvaluationResult> EvaluateAsync(CompiledConditionSet set, IConditionAttributeSource source, bool explain = false, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new ConditionEvaluationResult(ConditionOutcome.NotMatched, []));
        }
    }
}
