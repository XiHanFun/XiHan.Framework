// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.InMemory.Tests;

/// <summary>
/// 授权能力在启动核对里核对契约按名引用的策略
/// </summary>
/// <remarks>
/// 内存安全特性被选为授权能力时（ADR-0080、ADR-0085）
/// </remarks>
public sealed class PolicyReferenceTests
{
    /// <summary>
    /// 只带契约、不会被调用的操作描述
    /// </summary>
    /// <remarks>
    /// 核对只看契约，不需要可执行的操作类型
    /// </remarks>
    private sealed class ContractOnlyDescriptor(OperationContract contract)
        : OperationDescriptor(contract, "xihan.app.approvals", typeof(ContractOnlyDescriptor), typeof(object), typeof(object), null)
    {
        /// <inheritdoc />
        public override Result<object> DecodeRequest(ReadOnlyMemory<byte> payload) => throw new NotSupportedException("Contract-only descriptors are never invoked.");

        /// <inheritdoc />
        public override ReadOnlyMemory<byte> EncodeResponse(object response) => throw new NotSupportedException("Contract-only descriptors are never invoked.");

        /// <inheritdoc />
        public override ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request) => throw new NotSupportedException("Contract-only descriptors are never invoked.");
    }

    /// <summary>
    /// 按给定名字放行一切的策略
    /// </summary>
    private sealed class NamedPolicy(string name) : IPolicy
    {
        /// <inheritdoc />
        public string Name => name;

        /// <inheritdoc />
        public ValueTask<Result<AuthorizationResult>> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(Result.Success(AuthorizationResult.Allow()));
    }

    /// <summary>
    /// 登记给定契约的操作的桩特性
    /// </summary>
    private static StubFeature Approvals(params OperationContract[] contracts) => new(TestManifests.Create("xihan.app.approvals", requires: ["security.authorization@1"]))
    {
        OnConfigure = ctx =>
        {
            ctx.AddPermission(new PermissionDefinition("approvals.read", "Read approvals"));
            foreach (var contract in contracts)
            {
                ctx.Operations.Add(new ContractOnlyDescriptor(contract)).ThrowIfFailure();
            }
        },
    };

    [Fact]
    public async Task ContractReferencingAMissingPolicy_FailsTheStart()
    {
        var exception = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(static b => b.UseFeature<InMemorySecurityFeature>().UseFeature(Approvals(
            new OperationContract("approvals.list", policy: "authenticated"),
            new OperationContract("approvals.approve", policy: "approvers"),
            new OperationContract("approvals.export", policy: "permission:approvals.export")))));

        // 授权能力登记的启动核对：任何特性启动之前执行，策略不存在原样抛出、终止启动（不包成特性启动失败），一次列出全部
        Assert.Equal(SecurityErrors.UnregisteredPolicy, exception.Code);
        Assert.Contains("'approvals.approve' of 'xihan.app.approvals' requires policy 'approvers'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'approvals.export' of 'xihan.app.approvals' requires policy 'permission:approvals.export'", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("approvals.list", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContractReferencingAMissingPolicy_FailsTheStart_EvenUnderTheIsolatePolicy()
    {
        // 隔离策略只管特性启动失败：坏的策略名是装配错误，启动核对不归它管，照样拒绝启动，而不是隔离授权特性、让应用降级运行
        var exception = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(
            static b => b.UseFeature<InMemorySecurityFeature>().UseFeature(Approvals(new OperationContract("approvals.approve", policy: "approvers"))),
            [new("XiHan:Kernel:FailurePolicy", "IsolateFeature")]));

        Assert.Equal(SecurityErrors.UnregisteredPolicy, exception.Code);
        Assert.Contains("'approvals.approve' of 'xihan.app.approvals' requires policy 'approvers'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisteredPolicies_AndPermissionPolicies_Start_AndLaterRegistrationsAreChecked()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemorySecurityFeature>().UseFeature(Approvals(
                new OperationContract("approvals.list", policy: "authenticated"),
                new OperationContract("approvals.approve", policy: "approvers"),
                new OperationContract("approvals.get", policy: "permission:approvals.read"))),
            services: static s => s.AddSingleton<IPolicy>(new NamedPolicy("approvers")));

        // 启动之后登记的操作（插件握手之后）在登记时核对
        var registry = host.Services.GetRequiredService<OperationRegistry>();
        Assert.Equal(SecurityErrors.UnregisteredPolicy, registry.Add(new ContractOnlyDescriptor(new OperationContract("approvals.purge", policy: "auditors"))).Error?.Code);
        Assert.False(registry.Find("approvals.purge").HasValue);
        Assert.True(registry.Add(new ContractOnlyDescriptor(new OperationContract("approvals.export", policy: "approvers"))).IsSuccess);
    }

    [Fact]
    public async Task NotSelectedForAuthorization_DoesNotCheckPolicies()
    {
        // 显式 Provider 顶替了内存特性的授权能力，内存特性只剩认证：由那个 Provider 负责核对，内存特性不核对
        var authorization = new StubFeature(TestManifests.Create("xihan.app.authorization", provides: ["security.authorization@1"], requires: ["kernel.hosting@1"]));
        await using var host = await TestHost.StartAsync(b => b.UseFeature<InMemorySecurityFeature>().UseFeature(authorization).UseFeature(Approvals(new OperationContract("approvals.approve", policy: "approvers"))));

        Assert.Equal(["security.authentication"], host.App.Graph["xihan.security.inmemory"].SelectedCapabilities);
        Assert.True(host.Services.GetRequiredService<OperationRegistry>().Find("approvals.approve").HasValue);
    }
}
