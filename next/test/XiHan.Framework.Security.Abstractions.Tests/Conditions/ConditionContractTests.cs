// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Security.Abstractions.Conditions;
using static XiHan.Framework.Security.Abstractions.Tests.Fixtures.ConditionTestKit;

namespace XiHan.Framework.Security.Abstractions.Tests.Conditions;

/// <summary>
/// 条件模块的契约：运算符与值种类的整数值、属性取值包装、记忆来源与服务登记。
/// </summary>
public sealed class ConditionContractTests
{
    [Fact]
    public void OperatorAndKindIntegerValues_AreFixed()
    {
        ConditionOperator[] operators =
        [
            ConditionOperator.Equals, ConditionOperator.NotEquals, ConditionOperator.GreaterThan, ConditionOperator.GreaterThanOrEquals,
            ConditionOperator.LessThan, ConditionOperator.LessThanOrEquals, ConditionOperator.Contains, ConditionOperator.NotContains,
            ConditionOperator.In, ConditionOperator.NotIn, ConditionOperator.Between, ConditionOperator.StartsWith,
            ConditionOperator.EndsWith, ConditionOperator.IsNull, ConditionOperator.IsNotNull,
        ];
        Assert.Equal(Enumerable.Range(0, operators.Length), operators.Select(static item => (int)item));
        Assert.Equal(operators.Length, Enum.GetValues<ConditionOperator>().Length);

        ConditionValueKind[] kinds = [ConditionValueKind.String, ConditionValueKind.Number, ConditionValueKind.Boolean, ConditionValueKind.DateTime, ConditionValueKind.Date, ConditionValueKind.TimeOfDay, ConditionValueKind.IpAddress];
        Assert.Equal(Enumerable.Range(0, kinds.Length), kinds.Select(static item => (int)item));
    }

    [Fact]
    public void AttributeValueOf_WrapsScalarsAndExpandsEnumerables()
    {
        Assert.Same(ConditionAttributeValue.Missing, ConditionAttributeValue.Of(null));
        Assert.Equal(["abc"], ConditionAttributeValue.Of("abc").Values);
        Assert.Equal(["a", "b"], ConditionAttributeValue.Of(new[] { "a", null, "b" }).Values);
        Assert.Equal(ConditionAttributeValueState.Present, ConditionAttributeValue.Of(Array.Empty<string>()).State);
        Assert.Equal([42], ConditionAttributeValue.Of(42).Values);
    }

    [Fact]
    public async Task MemoizingSource_CachesEverythingButDeferred()
    {
        var calls = new List<string>();
        var deferred = true;
        var source = new MemoizingConditionAttributeSource(new DelegateSource(name =>
        {
            calls.Add(name);
            return name == "resource.amount" ? deferred ? ConditionAttributeValue.Deferred : ConditionAttributeValue.Of(5) : ConditionAttributeValue.Of("admin");
        }));

        await source.GetAsync("subject.userName", CancellationToken.None);
        await source.GetAsync("SUBJECT.USERNAME", CancellationToken.None);
        Assert.Equal(ConditionAttributeValueState.Deferred, (await source.GetAsync("resource.amount", CancellationToken.None)).State);
        deferred = false;
        Assert.Equal([5], (await source.GetAsync("resource.amount", CancellationToken.None)).Values);
        await source.GetAsync("resource.amount", CancellationToken.None);

        Assert.Equal(["subject.userName", "resource.amount", "resource.amount"], calls);
    }

    [Fact]
    public void Services_AreSingletons_AndAttributesAccumulateAcrossCalls()
    {
        var services = new ServiceCollection();
        services.AddConditionAttributes(Attributes);
        services.AddConditionAttributes(new ConditionAttributeDescriptor("environment.weekday", ConditionValueKind.Number));

        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, static d => d.ServiceType == typeof(IConditionAttributeRegistry)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, static d => d.ServiceType == typeof(IConditionCompiler)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, static d => d.ServiceType == typeof(IConditionEvaluator)).Lifetime);

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IConditionAttributeRegistry>();
        Assert.True(registry.TryResolve("environment.weekday", out _));
        Assert.True(registry.TryResolve("subject.role", out _));
        Assert.Same(registry, provider.GetRequiredService<IConditionAttributeRegistry>());
        Assert.Empty(provider.GetRequiredService<IConditionCompiler>().Compile(Single(new ConditionClause("environment.weekday", ConditionOperator.In, "1,2,3,4,5"))).Errors);
    }

    [Fact]
    public void Services_KeepImplementationsRegisteredFirst()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConditionEvaluator, StubEvaluator>();
        services.AddConditionAttributes();
        services.AddConditionAttributes();

        Assert.Equal(typeof(StubEvaluator), Assert.Single(services, static d => d.ServiceType == typeof(IConditionEvaluator)).ImplementationType);
        Assert.Single(services, static d => d.ServiceType == typeof(IConditionCompiler));
    }

    /// <summary>
    /// 以委托取值的来源。
    /// </summary>
    private sealed class DelegateSource(Func<string, ConditionAttributeValue> getter) : IConditionAttributeSource
    {
        /// <inheritdoc />
        public ValueTask<ConditionAttributeValue> GetAsync(string canonicalName, CancellationToken cancellationToken) => ValueTask.FromResult(getter(canonicalName));
    }

    /// <summary>
    /// 宿主自带的求值器。
    /// </summary>
    private sealed class StubEvaluator : IConditionEvaluator
    {
        /// <inheritdoc />
        public ValueTask<ConditionEvaluationResult> EvaluateAsync(CompiledConditionSet conditions, IConditionAttributeSource source, bool explain, CancellationToken cancellationToken)
            => ValueTask.FromResult(new ConditionEvaluationResult(ConditionOutcome.NotMatched, []));
    }
}
