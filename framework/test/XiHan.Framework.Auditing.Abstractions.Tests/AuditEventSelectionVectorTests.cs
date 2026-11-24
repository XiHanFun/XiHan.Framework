// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Auditing.Abstractions.Tests.Fixtures;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Auditing.Abstractions.Tests;

/// <summary>
/// 回放 <c>event-selection.json</c>：一次调用是否记成审计事件、记成什么
/// </summary>
/// <remarks>
/// 规范 RFC-0015「事件的产生」（ADR-0071、ADR-0080、ADR-0191、ADR-0277）：动作取显式声明的，否则取按选择规则挑出且没被排除的操作 ID；结果取管道里写入的，没有写入时按抛出与否；主体与租户先取安全上下文，再按规则回退到 Baggage。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class AuditEventSelectionVectorTests
{
    /// <summary>
    /// 结束方式为 <c>failure</c> 时管道里写入的错误码
    /// </summary>
    /// <remarks>
    /// 向量的 <c>$comment</c> 写明的那一个
    /// </remarks>
    private const string FailureCode = "ORDERS_VALIDATION_001";

    /// <summary>
    /// 归属用例里显式声明的动作
    /// </summary>
    /// <remarks>
    /// 归属与选择规则无关，声明一个动作让每次调用都记下事件
    /// </remarks>
    private const string AttributionAction = "vectors.attribute";

    /// <summary>
    /// 向量认得的结束方式
    /// </summary>
    private static readonly string[] _outcomes = ["success", "failure", "none", "throws", "canceled"];

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly SelectionVectors _vectors = SpecVectors.Read<SelectionVectors>("auditing", "event-selection.json");

    [Fact]
    public void Defaults_AreTheOnesInTheVectors()
    {
        var options = new AuditingOptions();

        Assert.Equal(_vectors.Defaults.Selection, options.Selection.ToString());
        Assert.Equal(_vectors.Defaults.IncludeSuccesses, options.IncludeSuccesses);
    }

    [Fact]
    public async Task Middleware_RecordsTheEventOfEverySelectionCase()
    {
        Assert.NotEmpty(_vectors.Selection);
        var failures = new List<string>();
        foreach (var vector in _vectors.Selection)
        {
            var options = new AuditingOptions
            {
                Selection = Enum.Parse<AuditSelection>(vector.Options.Selection),
                IncludeSuccesses = vector.Options.IncludeSuccesses,
                Exclude = vector.Options.Exclude,
            };
            var context = TestInvocationContext.Create();
            if (vector.Operation is { } operation)
            {
                context.SetOperation(new ContractOnlyOperationDescriptor(new OperationContract(operation.Id) { Permission = operation.Permission, Policy = operation.Policy }));
            }

            var failure = FailureOf(vector.Outcome);
            var (events, thrown) = await AuditReplay.InvokeAsync(options, context, StepOf(vector, failure));

            // 抛出的异常要原样传到外层：审计只记下它，处理它的是更外层的 Observe
            Assert.Same(failure, thrown);
            var actual = events.Count switch
            {
                0 => null,
                1 => new ExpectedEvent(events[0].Action, events[0].Outcome.IsSuccess, events[0].Outcome.Error?.Code),
                _ => throw new InvalidOperationException($"The case '{vector.Name}' recorded {events.Count} events."),
            };
            if (actual != vector.Expected)
            {
                failures.Add($"{vector.Name}: recorded '{Describe(actual)}', the vector has '{Describe(vector.Expected)}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Middleware_AttributesEveryCaseToThePrincipalAndTenant()
    {
        Assert.NotEmpty(_vectors.Attribution);
        var failures = new List<string>();
        foreach (var vector in _vectors.Attribution)
        {
            var baggage = new Dictionary<string, string>(StringComparer.Ordinal);
            if (vector.BaggagePrincipal is { } carriedPrincipal)
            {
                baggage[BaggageKeys.Principal] = carriedPrincipal;
            }

            if (vector.BaggageTenant is { } carriedTenant)
            {
                baggage[BaggageKeys.Tenant] = carriedTenant;
            }

            var (events, thrown) = await AuditReplay.InvokeAsync(new AuditingOptions(), TestInvocationContext.Create(baggage: baggage), context =>
            {
                // Authenticate 与 Tenant 阶段都在 Observe 之内：主体、租户与「裁定过」的标记是调用期间写进去的，中间件在调用结束后才读
                var principal = vector.Principal is { } id ? new Principal(id, "vectors") : Principal.Anonymous;
                context.SetSecurity(new SecurityContext { Principal = principal, Tenant = vector.Tenant is { } tenant ? new TenantContext { Id = tenant } : null });
                if (vector.TenantResolved)
                {
                    context.MarkTenantResolved();
                }

                context.SetAuditAction(AttributionAction);
                return ValueTask.CompletedTask;
            });

            Assert.Null(thrown);
            var recorded = Assert.Single(events);
            var actual = new ExpectedAttribution(recorded.PrincipalId, recorded.TenantId);
            if (actual != vector.Expected)
            {
                failures.Add($"{vector.Name}: recorded '{actual}', the vector has '{vector.Expected}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverEverySelectionOutcomeAndFallback()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过：每种选择规则（枚举加了成员而向量没跟上也在这里失败）、每种结束方式都要有用例
        Assert.Equal(Enum.GetNames<AuditSelection>().Order(StringComparer.Ordinal), _vectors.Selection.Select(static vector => vector.Options.Selection).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(_outcomes.Order(StringComparer.Ordinal), _vectors.Selection.Select(static vector => vector.Outcome).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

        // 不记的、被排除却因显式动作照记的、只记失败时照记的
        Assert.Contains(_vectors.Selection, static vector => vector.Expected is null);
        Assert.Contains(_vectors.Selection, static vector => vector.Options.Exclude.Length > 0 && vector.ExplicitAction is not null && vector.Expected is not null);
        Assert.Contains(_vectors.Selection, static vector => !vector.Options.IncludeSuccesses && vector.Expected is { Succeeded: false });

        // 归属：安全上下文优先于 Baggage；没有 Tenant 阶段时回退；Tenant 阶段裁定为无租户时不回退到调用方自报的租户（ADR-0191）
        Assert.Contains(_vectors.Attribution, static vector => vector.Principal is not null && vector.BaggagePrincipal is not null && vector.Expected.Principal == vector.Principal);
        Assert.Contains(_vectors.Attribution, static vector => !vector.TenantResolved && vector.BaggageTenant is not null && vector.Expected.Tenant == vector.BaggageTenant);
        Assert.Contains(_vectors.Attribution, static vector => vector.TenantResolved && vector.Tenant is null && vector.BaggageTenant is not null && vector.Expected.Tenant is null);
    }

    /// <summary>
    /// 这种结束方式下管道抛出的异常
    /// </summary>
    /// <returns>不抛出的结束方式为 <see langword="null"/></returns>
    private static Exception? FailureOf(string outcome) => outcome switch
    {
        "throws" => new InvalidOperationException("The operation crashed."),
        "canceled" => new OperationCanceledException("The invocation was canceled."),
        "success" or "failure" or "none" => null,
        _ => throw new InvalidOperationException($"The vectors use the unknown outcome '{outcome}'."),
    };

    /// <summary>
    /// 一个用例里中间件之后的管道
    /// </summary>
    /// <param name="vector">用例</param>
    /// <param name="failure">要抛出的异常；不抛出为 <see langword="null"/></param>
    private static PipelineStep StepOf(SelectionCase vector, Exception? failure) => context =>
    {
        // 显式动作是调用期间声明的
        if (vector.ExplicitAction is { } action)
        {
            context.SetAuditAction(action);
        }

        if (failure is not null)
        {
            throw failure;
        }

        switch (vector.Outcome)
        {
            case "success":
                context.SetOutcome(Result.Success());
                break;
            case "failure":
                context.SetOutcome(Error.Validation(FailureCode, "orders.amount.negative"));
                break;
        }

        // none：正常返回却没写结果
        return ValueTask.CompletedTask;
    };

    /// <summary>
    /// 失败信息里的事件
    /// </summary>
    private static string Describe(ExpectedEvent? recorded)
        => recorded?.ToString() ?? "no event";

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Defaults">两个选项的缺省值</param>
    /// <param name="Selection">事件选择与结果的用例</param>
    /// <param name="Attribution">主体与租户归属的用例</param>
    private sealed record SelectionVectors(SelectionDefaults Defaults, SelectionCase[] Selection, AttributionCase[] Attribution);

    /// <summary>
    /// 选项的缺省值
    /// </summary>
    /// <param name="Selection">选择规则的名字</param>
    /// <param name="IncludeSuccesses">是否记录成功的调用</param>
    private sealed record SelectionDefaults(string Selection, bool IncludeSuccesses);

    /// <summary>
    /// 一个事件选择用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Options">审计选项</param>
    /// <param name="Operation">当前操作；<see langword="null"/> 表示这次调用不是操作调用</param>
    /// <param name="ExplicitAction">调用期间显式声明的动作；<see langword="null"/> 表示没有</param>
    /// <param name="Outcome">调用怎样结束</param>
    /// <param name="Expected">记下的事件；<see langword="null"/> 表示不记</param>
    private sealed record SelectionCase(string Name, SelectionOptions Options, OperationCase? Operation, string? ExplicitAction, string Outcome, ExpectedEvent? Expected);

    /// <summary>
    /// 用例里的审计选项
    /// </summary>
    /// <param name="Selection">选择规则的名字</param>
    /// <param name="IncludeSuccesses">是否记录成功的调用</param>
    /// <param name="Exclude">排除列表</param>
    private sealed record SelectionOptions(string Selection, bool IncludeSuccesses, string[] Exclude);

    /// <summary>
    /// 用例里的当前操作
    /// </summary>
    /// <param name="Id">操作 ID</param>
    /// <param name="Permission">契约上的权限码</param>
    /// <param name="Policy">契约上的策略名</param>
    private sealed record OperationCase(string Id, string? Permission, string? Policy);

    /// <summary>
    /// 记下的事件
    /// </summary>
    /// <param name="Action">动作</param>
    /// <param name="Succeeded">结果是否成功</param>
    /// <param name="Code">失败时的错误码</param>
    private sealed record ExpectedEvent(string Action, bool Succeeded, string? Code);

    /// <summary>
    /// 一个归属用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Principal">安全上下文里已认证的主体；<see langword="null"/> 表示匿名</param>
    /// <param name="BaggagePrincipal">Baggage 的 <c>xihan.principal</c></param>
    /// <param name="TenantResolved">Tenant 阶段是否裁定过这次调用的租户</param>
    /// <param name="Tenant">安全上下文里的租户；<see langword="null"/> 表示无租户</param>
    /// <param name="BaggageTenant">Baggage 的 <c>xihan.tenant</c></param>
    /// <param name="Expected">事件上的主体与租户</param>
    private sealed record AttributionCase(string Name, string? Principal, string? BaggagePrincipal, bool TenantResolved, string? Tenant, string? BaggageTenant, ExpectedAttribution Expected);

    /// <summary>
    /// 事件上的主体与租户
    /// </summary>
    /// <param name="Principal">主体 ID</param>
    /// <param name="Tenant">租户 ID</param>
    private sealed record ExpectedAttribution(string? Principal, string? Tenant);
}
