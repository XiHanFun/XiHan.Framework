// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Operations;

/// <summary>
/// 登记表的变更令牌与整组核对
/// </summary>
/// <remarks>
/// ADR-0251：内容每变一次令牌触发一次、在锁外同步触发；整组核对看得到已登记的与组内的其他项，不通过即整组不登记
/// </remarks>
public sealed class OperationRegistryChangeTests
{
    /// <summary>
    /// 插件的清单 ID
    /// </summary>
    private const string Plugin = "xihan.test.plugin";

    /// <summary>
    /// 描述一个回声操作
    /// </summary>
    private static OperationDescriptor<EchoOperation, EchoRequest, EchoResponse> Describe(string id, string owner = Plugin)
        => new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(new OperationContract(id), owner, EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);

    /// <summary>
    /// 定义一个权限码
    /// </summary>
    private static PermissionDefinition Define(string code) => new(code, code);

    /// <summary>
    /// 资源段相同即冲突的整组核对，记下每次收到的两组 ID
    /// </summary>
    /// <param name="calls">每次调用收到的已登记与新增的操作 ID</param>
    private static Func<IReadOnlyList<OperationDescriptor>, IReadOnlyList<OperationDescriptor>, Error?> SameResourceConflicts(List<(string[] Existing, string[] Added)> calls)
        => (existing, added) =>
        {
            calls.Add(([.. existing.Select(static d => d.Id)], [.. added.Select(static d => d.Id)]));
            var resources = existing.Select(static d => d.Id[..d.Id.IndexOf('.', StringComparison.Ordinal)]).ToHashSet(StringComparer.Ordinal);
            foreach (var descriptor in added)
            {
                if (!resources.Add(descriptor.Id[..descriptor.Id.IndexOf('.', StringComparison.Ordinal)]))
                {
                    return new Error(KernelErrors.DuplicateOperation, KernelMessages.DuplicateOperation, MessageArguments.Of(("id", descriptor.Id), ("owner", descriptor.OwnerId)));
                }
            }

            return null;
        };

    [Fact]
    public void ChangeToken_FiresOncePerChange_AndIsRenewedAfterwards()
    {
        var registry = new OperationRegistry(new PermissionRegistry());
        var first = registry.GetChangeToken();
        Assert.True(first.ActiveChangeCallbacks);
        Assert.False(first.HasChanged);
        var fired = 0;
        using var registration = first.RegisterChangeCallback(_ => fired++, null);

        // 空组、没有登记过的归属都不算变化
        Assert.True(registry.AddRange(Plugin, [], []).IsSuccess);
        Assert.Equal(0, registry.RemoveOwnedBy("xihan.test.nobody"));
        Assert.False(first.HasChanged);

        Assert.True(registry.AddRange(Plugin, [Describe("echoes.get")], []).IsSuccess);
        Assert.True(first.HasChanged);
        Assert.Equal(1, fired);

        // 触发之后再取得到的是新令牌；登记失败不触发
        var second = registry.GetChangeToken();
        Assert.NotSame(first, second);
        Assert.Equal(KernelErrors.DuplicateOperation, registry.AddRange(Plugin, [Describe("echoes.get")], []).Error?.Code);
        Assert.False(second.HasChanged);

        // 只有权限码的一组也是变化
        Assert.True(registry.AddRange(Plugin, [], [Define("echoes.read")]).IsSuccess);
        Assert.True(second.HasChanged);

        var third = registry.GetChangeToken();
        Assert.Equal(1, registry.RemoveOwnedBy(Plugin));
        Assert.True(third.HasChanged);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void ChangeToken_FiresOutsideTheLock_SoCallbacksMayReadAndRegister()
    {
        var registry = new OperationRegistry(new PermissionRegistry());
        string[] seen = [];
        var registeredFromAnotherThread = false;
        registry.GetChangeToken().RegisterChangeCallback(_ =>
        {
            seen = [.. registry.All().Select(static d => d.Id)];
            // 在锁里触发的话，另一个线程的登记会一直等到这个回调返回：这里限时等它，等不到即失败
            var other = new Thread(() => registeredFromAnotherThread = registry.AddRange("xihan.test.other", [Describe("others.get", "xihan.test.other")], []).IsSuccess);
            other.Start();
            Assert.True(other.Join(TimeSpan.FromSeconds(10)));
        }, null);

        Assert.True(registry.AddRange(Plugin, [Describe("echoes.get")], []).IsSuccess);
        Assert.Equal(["echoes.get"], seen);
        Assert.True(registeredFromAnotherThread);
        Assert.True(registry.Find("others.get").HasValue);
    }

    [Fact]
    public void ChangeToken_SkipsDisposedCallbacks_AndRunsLateOnesAtOnce()
    {
        var registry = new OperationRegistry(new PermissionRegistry());
        var token = registry.GetChangeToken();
        var calls = 0;
        token.RegisterChangeCallback(_ => calls++, null).Dispose();

        Assert.True(registry.AddRange(Plugin, [Describe("echoes.get")], []).IsSuccess);
        Assert.Equal(0, calls);

        // 触发之后才登记的回调立即执行，不会错过这次变化
        using var late = token.RegisterChangeCallback(_ => calls++, null);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ChangeToken_CallbackFailures_SurfaceAfterTheRegistrationCompletes()
    {
        var registry = new OperationRegistry(new PermissionRegistry());
        var token = registry.GetChangeToken();
        var after = 0;
        using var failing = token.RegisterChangeCallback(static _ => throw new InvalidOperationException("Callback failed."), null);
        using var following = token.RegisterChangeCallback(_ => after++, null);

        var exception = Assert.Throws<AggregateException>(() => registry.AddRange(Plugin, [Describe("echoes.get")], []));
        Assert.IsType<InvalidOperationException>(Assert.Single(exception.InnerExceptions));
        // 一个回调失败不挡住其余回调，登记本身已经完成
        Assert.Equal(1, after);
        Assert.True(registry.Find("echoes.get").HasValue);
    }

    [Fact]
    public void EnforceAdditions_ChecksTheRegisteredOnce_ThenEachGroupAgainstTheRegistered()
    {
        var permissions = new PermissionRegistry();
        var registry = new OperationRegistry(permissions);
        Assert.True(registry.AddRange("xihan.test.host", [Describe("echoes.get", "xihan.test.host")], []).IsSuccess);
        var calls = new List<(string[] Existing, string[] Added)>();

        // 挂上时以（空、已登记的全部）核对一次
        Assert.Null(registry.EnforceAdditions(SameResourceConflicts(calls)));
        Assert.Empty(calls[0].Existing);
        Assert.Equal(["echoes.get"], calls[0].Added);

        // 组内两项冲突：整组不登记，已写进的权限码撤回，令牌不触发
        var token = registry.GetChangeToken();
        var inGroup = registry.AddRange(Plugin, [Describe("orders.list"), Describe("orders.get")], [Define("orders.read")]);
        Assert.Equal(KernelErrors.DuplicateOperation, inGroup.Error?.Code);
        Assert.Equal(["echoes.get"], calls[1].Existing);
        Assert.Equal(["orders.get", "orders.list"], calls[1].Added);
        Assert.False(registry.Find("orders.get").HasValue);
        Assert.False(permissions.Find("orders.read").HasValue);
        Assert.False(token.HasChanged);

        // 与已登记的冲突
        Assert.Equal(KernelErrors.DuplicateOperation, registry.AddRange(Plugin, [Describe("echoes.list")], []).Error?.Code);
        Assert.False(registry.Find("echoes.list").HasValue);

        // 不冲突的照常登记；撤回不经过核对
        Assert.True(registry.AddRange(Plugin, [Describe("orders.get")], [Define("orders.read")]).IsSuccess);
        Assert.True(permissions.Find("orders.read").HasValue);
        var before = calls.Count;
        Assert.Equal(1, registry.RemoveOwnedBy(Plugin));
        Assert.Equal(before, calls.Count);
    }

    [Fact]
    public void EnforceAdditions_ReportsTheRegisteredThatAlreadyConflict_AndStaysAttached()
    {
        var registry = new OperationRegistry(new PermissionRegistry());
        Assert.True(registry.AddRange(Plugin, [Describe("things.get"), Describe("things.list")], []).IsSuccess);
        var calls = new List<(string[] Existing, string[] Added)>();

        Assert.Equal(KernelErrors.DuplicateOperation, registry.EnforceAdditions(SameResourceConflicts(calls))?.Code);
        Assert.Equal(KernelErrors.DuplicateOperation, registry.AddRange("xihan.test.other", [Describe("things.delete", "xihan.test.other")], []).Error?.Code);
        Assert.Throws<ArgumentNullException>(() => registry.EnforceAdditions(null!));
    }
}
