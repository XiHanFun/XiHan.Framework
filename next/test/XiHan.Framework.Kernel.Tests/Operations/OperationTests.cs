// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Operations;

/// <summary>
/// 操作契约与 XGI
/// </summary>
/// <remarks>
/// 登记表、契约校验、JSON 与类型化调用、描述进入上下文、失败与异常翻译、管道内执行
/// </remarks>
public sealed class OperationTests
{
    /// <summary>
    /// 构建并启动只装配回声特性的应用
    /// </summary>
    private static async Task<(ServiceProvider Provider, App App)> StartAsync(params IFeature[] features)
    {
        var (provider, app) = Hosts.BuildApp(b =>
        {
            foreach (var feature in features.Length == 0 ? [new EchoFeature()] : features)
            {
                b.UseFeature(feature);
            }
        });
        await app.StartAsync(CancellationToken.None);
        return (provider, app);
    }

    /// <summary>
    /// UTF-8 字节
    /// </summary>
    private static ReadOnlyMemory<byte> Utf8(string json) => Encoding.UTF8.GetBytes(json);

    /// <summary>
    /// 解析响应载荷里的字段
    /// </summary>
    private static string Field(OperationResponse response, string name)
    {
        using var document = JsonDocument.Parse(response.Payload);
        return document.RootElement.GetProperty(name).GetString()!;
    }

    [Fact]
    public void Registry_RejectsDuplicates_AndEnforcesCapacity()
    {
        var registry = new OperationRegistry(new PermissionRegistry(), capacity: 1);
        var first = new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(EchoOperation.Contract, "owner-a", EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);
        var duplicate = new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(EchoOperation.Contract, "owner-b", EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);
        var other = new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(new("echoes.list"), "owner-b", EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);

        Assert.True(registry.Add(first).IsSuccess);
        var duplicated = registry.Add(duplicate);
        Assert.Equal(KernelErrors.DuplicateOperation, duplicated.Error!.Code);
        Assert.Equal("owner-a", duplicated.Error.Details!["owner"]);
        Assert.Equal(KernelErrors.OperationRegistryFull, registry.Add(other).Error!.Code);
        Assert.Equal(1, registry.Count);
        Assert.Same(first, registry.Find(EchoOperation.Id).ValueOrThrow());
        Assert.False(registry.Find("echoes.list").HasValue);
        Assert.Equal([first], registry.ExposedTo(Expose.Http));
        Assert.Empty(registry.ExposedTo(Expose.Grpc));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new OperationRegistry(new PermissionRegistry(), 0));
        Assert.Throws<ArgumentNullException>(static () => new OperationRegistry(null!));
    }

    [Fact]
    public void Registry_AddRange_RegistersAllOrNothing()
    {
        var registry = new OperationRegistry(new PermissionRegistry(), capacity: 3);
        OperationDescriptor Describe(string id, string owner = "owner-a") => new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(new(id), owner, EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);

        // 同一批里重复：返回重复，登记表不变
        var duplicated = registry.AddRange("owner-a", [Describe("echoes.get"), Describe("echoes.list"), Describe("echoes.get")], []);
        Assert.Equal(KernelErrors.DuplicateOperation, duplicated.Error?.Code);
        Assert.Equal(0, registry.Count);

        // 整批进；与已登记的重复同样整批不进
        Assert.True(registry.AddRange("owner-a", [Describe("echoes.get"), Describe("echoes.list")], []).IsSuccess);
        Assert.Equal(KernelErrors.DuplicateOperation, registry.AddRange("owner-a", [Describe("echoes.create"), Describe("echoes.get")], []).Error?.Code);
        Assert.False(registry.Find("echoes.create").HasValue);

        // 整批超出容量：一个都不进
        Assert.Equal(KernelErrors.OperationRegistryFull, registry.AddRange("owner-a", [Describe("echoes.create"), Describe("echoes.delete")], []).Error?.Code);
        Assert.Equal(2, registry.Count);

        Assert.True(registry.AddRange("owner-a", [], []).IsSuccess);
        Assert.Throws<ArgumentException>(() => registry.AddRange("owner-a", [Describe("echoes.create"), null!], []));
        Assert.Throws<ArgumentException>(() => registry.AddRange("owner-a", [Describe("echoes.create")], [null!]));
        Assert.Equal(2, registry.Count);
    }

    [Fact]
    public void Registry_AddRange_WithPermissions_RegistersTheGroupAllOrNothing()
    {
        var permissions = new PermissionRegistry();
        Assert.True(permissions.Add("xihan.test.host", new PermissionDefinition("hosts.manage", "Manage hosts")).IsSuccess);
        var registry = new OperationRegistry(permissions);
        OperationDescriptor Describe(string id, string? permission = null) => new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(new(id, permission: permission), "xihan.test.plugin", EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);
        static PermissionDefinition Define(string code) => new(code, code);

        // 一组：归属定义的权限码与引用它们的操作一起进
        Assert.True(registry.AddRange("xihan.test.plugin", [Describe("echoes.get", "echoes.read")], [Define("echoes.read"), Define("echoes.write")]).IsSuccess);
        Assert.Equal("echoes.read", permissions.Find("echoes.read").ValueOrThrow().DisplayName);
        Assert.True(permissions.Find("echoes.write").HasValue);

        // 声明的码与已登记的重复（宿主的、或上一组声明的）：整组不进，操作也不进
        var withHost = registry.AddRange("xihan.test.plugin", [Describe("echoes.list")], [Define("echoes.purge"), Define("hosts.manage")]);
        Assert.Equal(KernelErrors.DuplicatePermission, withHost.Error?.Code);
        Assert.Equal("hosts.manage", withHost.Error?.Details?["code"]);
        Assert.False(permissions.Find("echoes.purge").HasValue);
        Assert.False(registry.Find("echoes.list").HasValue);
        Assert.Equal(KernelErrors.DuplicatePermission, registry.AddRange("xihan.test.plugin", [Describe("echoes.list")], [Define("echoes.write")]).Error?.Code);
        Assert.Equal(KernelErrors.DuplicatePermission, registry.AddRange("xihan.test.plugin", [Describe("echoes.list")], [Define("echoes.copy"), Define("echoes.copy")]).Error?.Code);
        Assert.False(permissions.Find("echoes.copy").HasValue);

        // 操作重名：权限码也不进
        Assert.Equal(KernelErrors.DuplicateOperation, registry.AddRange("xihan.test.plugin", [Describe("echoes.get")], [Define("echoes.purge")]).Error?.Code);
        Assert.False(permissions.Find("echoes.purge").HasValue);
        Assert.Equal(3, permissions.Count);
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void Registry_RemoveOwnedBy_WithdrawsTheOwnersOperationsAndCodes_AndTheOwnerCanRegisterAgain()
    {
        var permissions = new PermissionRegistry();
        Assert.True(permissions.Add("xihan.test.host", new PermissionDefinition("hosts.manage", "Manage hosts")).IsSuccess);
        var registry = new OperationRegistry(permissions);
        static OperationDescriptor Describe(string id, string owner, string? permission = null) => new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(new(id, permission: permission), owner, EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);
        static PermissionDefinition Define(string code) => new(code, code);
        static IReadOnlyList<OperationDescriptor> Group() => [Describe("echoes.get", "xihan.test.plugin", "echoes.read"), Describe("echoes.list", "xihan.test.plugin")];
        Assert.True(registry.AddRange("xihan.test.plugin", Group(), [Define("echoes.read"), Define("echoes.write")]).IsSuccess);
        // 别的归属的操作引用了这个插件定义的码
        Assert.True(registry.AddRange("xihan.test.other", [Describe("others.get", "xihan.test.other", "echoes.read")], []).IsSuccess);

        Assert.Equal(2, registry.RemoveOwnedBy("xihan.test.plugin"));

        // 本归属的操作与码都撤回；宿主的码与别的归属的操作不动（引用被撤回的码的操作照旧登记，授权时按策略不存在拒绝）
        Assert.False(registry.Find("echoes.get").HasValue);
        Assert.False(registry.Find("echoes.list").HasValue);
        Assert.False(permissions.Find("echoes.read").HasValue);
        Assert.False(permissions.Find("echoes.write").HasValue);
        Assert.True(permissions.Find("hosts.manage").HasValue);
        Assert.True(registry.Find("others.get").HasValue);
        Assert.Equal(0, registry.RemoveOwnedBy("xihan.test.plugin"));
        Assert.Equal(0, registry.RemoveOwnedBy("xihan.test.nobody"));

        // 同一归属可以直接再登记（插件卸载后再加载），不撞上自己上一次的登记
        Assert.True(registry.AddRange("xihan.test.plugin", Group(), [Define("echoes.read"), Define("echoes.write")]).IsSuccess);
        Assert.Equal(3, registry.Count);
        Assert.Equal(3, permissions.Count);
        Assert.Throws<ArgumentException>(() => registry.RemoveOwnedBy(" "));
    }

    [Fact]
    public void Registry_AddRange_RejectsOperationsOfAnotherOwner()
    {
        var permissions = new PermissionRegistry();
        var registry = new OperationRegistry(permissions);
        static OperationDescriptor Describe(string id, string owner) => new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(new(id), owner, EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);

        // 一组只有一个归属：混进别的归属的操作是编程错误，什么都不登记（否则按归属撤回会错撤或漏撤）
        var exception = Assert.Throws<ArgumentException>(() => registry.AddRange("xihan.test.plugin", [Describe("echoes.get", "xihan.test.plugin"), Describe("echoes.list", "xihan.test.other")], [new PermissionDefinition("echoes.read", "echoes.read")]));

        Assert.Contains("'echoes.list' is owned by 'xihan.test.other'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, registry.Count);
        Assert.Equal(0, permissions.Count);
        Assert.Throws<ArgumentException>(() => registry.AddRange(" ", [], []));
    }

    [Fact]
    public async Task Registry_RegisterAndWithdraw_RacingForTheSameNames_NeverLeavesHalfAGroup()
    {
        var permissions = new PermissionRegistry();
        var registry = new OperationRegistry(permissions);
        static OperationDescriptor Describe(string id, string owner) => new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(new(id, permission: id == "echoes.get" ? "echoes.read" : null), owner, EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);
        static PermissionDefinition Define(string code) => new(code, code);
        var successes = 0;

        // 四个归属争同一组名字（同一个插件的新旧版本那样）：各自反复登记、撤回自己的一组
        await Task.WhenAll(Enumerable.Range(0, 4).Select(index => Task.Run(() =>
        {
            var owner = $"xihan.test.plugin{index}";
            for (var round = 0; round < 300; round++)
            {
                var added = registry.AddRange(owner, [Describe("echoes.get", owner), Describe("echoes.list", owner)], [Define("echoes.read"), Define("echoes.write")]);
                if (added.IsFailure)
                {
                    // 撤回与登记都在锁里整组完成：别的归属的操作还在，它的码就一定还在，先撞上的总是操作重名，
                    // 不会出现操作已撤回、码还留着的半组状态（那样会是权限码重复）
                    Assert.Equal(KernelErrors.DuplicateOperation, added.Error?.Code);
                    continue;
                }

                Interlocked.Increment(ref successes);
                // 登记成功的一组整组归自己：两个操作都是自己的，撤回时整组撤回
                Assert.Equal(owner, registry.Find("echoes.get").ValueOrThrow().OwnerId);
                Assert.Equal(owner, registry.Find("echoes.list").ValueOrThrow().OwnerId);
                Assert.True(permissions.Find("echoes.read").HasValue);
                Assert.Equal(2, registry.RemoveOwnedBy(owner));
            }
        })));

        Assert.True(successes > 0);
        Assert.Equal(0, registry.Count);
        Assert.Equal(0, permissions.Count);
    }

    [Fact]
    public void Registry_Enforce_ReturnsFailingOperations_AndChecksEveryLaterRegistration()
    {
        var permissions = new PermissionRegistry();
        var registry = new OperationRegistry(permissions);
        OperationDescriptor Describe(string id, string? policy) => new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(new(id, policy: policy), "xihan.test.plugin", EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);
        Assert.True(registry.AddRange("xihan.test.plugin", [Describe("echoes.get", "known"), Describe("echoes.list", "unknown"), Describe("echoes.copy", "unknown")], []).IsSuccess);

        // 挂上核对时返回已登记的不通过项（按 ID 排序），由调用方决定怎样失败
        var failing = registry.Enforce(static d => d.Contract.Policy == "unknown" ? Error.Validation("TESTS_VALIDATION_001", "tests.policy.unknown") : null);
        Assert.Equal(["echoes.copy", "echoes.list"], failing.Select(static d => d.Id));

        // 此后每次登记都经过它：不通过整组不进，同组的权限码撤回
        var rejected = registry.AddRange("xihan.test.plugin", [Describe("echoes.create", "known"), Describe("echoes.delete", "unknown")], [new PermissionDefinition("echoes.audit", "echoes.audit")]);
        Assert.Equal("TESTS_VALIDATION_001", rejected.Error?.Code);
        Assert.False(registry.Find("echoes.create").HasValue);
        Assert.False(permissions.Find("echoes.audit").HasValue);
        Assert.True(registry.Add(Describe("echoes.create", "known")).IsSuccess);
        Assert.Throws<ArgumentNullException>(() => registry.Enforce(null!));
    }

    [Fact]
    public async Task Registry_AddRange_AfterAssembly_RollsBackDeclaredPermissionsWhenAnOperationIsRejected()
    {
        var (provider, app) = await StartAsync(new EchoFeature(registerPermission: true));
        var registry = provider.GetRequiredService<OperationRegistry>();
        var permissions = provider.GetRequiredService<PermissionRegistry>();
        OperationDescriptor Describe(string id, string? permission) => new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(new(id, permission: permission), "xihan.test.plugin", EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);

        // 装配之后：一项引用了谁都没定义的码，整组被拒，同组声明的码撤回
        var rejected = registry.AddRange("xihan.test.plugin", [Describe("echoes.get", "plugin.read"), Describe("echoes.purge", "plugin.purge")], [new PermissionDefinition("plugin.read", "plugin.read")]);
        Assert.Equal(KernelErrors.UnregisteredPermission, rejected.Error?.Code);
        Assert.Equal("plugin.purge", rejected.Error?.Details?["permission"]);
        Assert.False(permissions.Find("plugin.read").HasValue);
        Assert.False(registry.Find("echoes.get").HasValue);

        // 同组声明的码可以被同组的操作引用；宿主特性登记的码同样可以
        Assert.True(registry.AddRange("xihan.test.plugin", [Describe("echoes.get", "plugin.read"), Describe("echoes.list", "echoes.write")], [new PermissionDefinition("plugin.read", "plugin.read")]).IsSuccess);
        Assert.True(permissions.Find("plugin.read").HasValue);
        await app.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();
    }

    [Fact]
    public void AddOperation_WithUnregisteredPermission_FailsAtBuild_ListingEveryOffender()
    {
        var exception = Assert.Throws<FrameworkException>(static () => Hosts.BuildApp(b => b
            .UseFeature(new EchoFeature(registerPermission: false))
            .UseFeature(new EchoFeature("xihan.test.echo2", "echoes.copy", capture: false, registerPermission: false))));

        Assert.Equal(KernelErrors.UnregisteredPermission, exception.Code);
        Assert.Contains("'echoes.create' of 'xihan.test.echo' requires 'echoes.write'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'echoes.copy' of 'xihan.test.echo2' requires 'echoes.write'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Permissions_AreCheckedWhenAssemblyCompletes_AndLateRegistrationsAreRejected()
    {
        // 权限码由另一个特性登记：登记操作的那一刻还没有它，装配完成时已经有了
        var (provider, app) = await StartAsync(new EchoFeature(registerPermission: false), new PermissionsFeature("xihan.test.permissions", "echoes.write", "echoes.read"));
        var registry = provider.GetRequiredService<OperationRegistry>();
        OperationDescriptor Describe(string id, string? permission) => new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(new(id, permission: permission), "xihan.test.plugin", EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);

        // 装配之后登记（插件握手之后）：未登记的权限码在登记时被拒，整批不进
        var rejected = registry.AddRange("xihan.test.plugin", [Describe("echoes.get", "echoes.read"), Describe("echoes.purge", "echoes.purge")], []);
        Assert.Equal(KernelErrors.UnregisteredPermission, rejected.Error?.Code);
        Assert.Equal("VALIDATION", rejected.Error?.Kind);
        Assert.Equal("echoes.purge", rejected.Error?.Details?["id"]);
        Assert.Equal("xihan.test.plugin", rejected.Error?.Details?["owner"]);
        Assert.Equal("echoes.purge", rejected.Error?.Details?["permission"]);
        Assert.False(registry.Find("echoes.get").HasValue);

        // 已登记的权限码与没有权限要求的操作照常登记
        Assert.True(registry.AddRange("xihan.test.plugin", [Describe("echoes.get", "echoes.read"), Describe("echoes.list", null)], []).IsSuccess);
        Assert.Equal(KernelErrors.UnregisteredPermission, registry.Add(Describe("echoes.purge", "echoes.purge")).Error?.Code);
        await app.StopAsync(CancellationToken.None);
        await provider.DisposeAsync();
    }

    [Fact]
    public void DecodeResponse_RoundTripsEncodeResponse_AndRejectsNullOrInvalidJson()
    {
        var descriptor = new OperationDescriptor<EchoOperation, EchoRequest, EchoResponse>(EchoOperation.Contract, "owner", EchoJsonContext.Default.EchoRequest, EchoJsonContext.Default.EchoResponse);
        var response = new EchoResponse("hihi", "0af7651916cd43dd8448eb211c80319c");

        // 幂等回放靠它把记录的 XGI 载荷还原成响应对象（RFC-0006）
        Assert.Equal(response, descriptor.DecodeResponse(descriptor.EncodeResponse(response)).ValueOrThrow());
        Assert.Equal(KernelErrors.InvalidOperationPayload, descriptor.DecodeResponse("null"u8.ToArray()).Error?.Code);
        Assert.Equal(KernelErrors.InvalidOperationPayload, descriptor.DecodeResponse("{oops"u8.ToArray()).Error?.Code);
        Assert.Equal(KernelErrors.InvalidOperationPayload, descriptor.DecodeResponse(ReadOnlyMemory<byte>.Empty).Error?.Code);
    }

    [Fact]
    public void Contract_ValidatesIdAndAuthorization()
    {
        var contract = new OperationContract("order-lines.list", Stability.Preview, Expose.Http, policy: "owners");

        Assert.Equal("order-lines", contract.Resource);
        Assert.Equal("list", contract.Verb);
        Assert.True(contract.IsExposedTo(Expose.Http));
        Assert.False(contract.IsExposedTo(Expose.Http | Expose.Mcp));
        Assert.False(contract.IsExposedTo(Expose.None));
        Assert.Throws<ArgumentException>(static () => new OperationContract("Orders.Create"));
        Assert.Throws<ArgumentException>(static () => new OperationContract("orders.create", policy: "a", permission: "b"));
        Assert.Throws<ArgumentException>(static () => new OperationContract("orders.create", permission: " "));
        Assert.Throws<ArgumentException>(static () => new OperationRequest("bad"));
        Assert.Equal("echoes.create", new OperationAttribute("echoes.create").Id);
    }

    [Fact]
    public void Contract_IsReadOnly_ForGetAndList_AndDeclaredQueryVerbs()
    {
        // get / list 天生只读；非标准动词默认不是，声明后才是
        Assert.True(new OperationContract("orders.get").IsReadOnly);
        Assert.True(new OperationContract("orders.list").IsReadOnly);
        Assert.False(new OperationContract("departments.tree").IsReadOnly);
        Assert.True(new OperationContract("departments.tree", readOnly: true).IsReadOnly);
        Assert.True(new OperationContract("users.list-online", readOnly: true).IsReadOnly);
        Assert.False(new OperationContract("orders.cancel").IsReadOnly);

        // 写操作的动词不能声明只读
        foreach (var id in new[] { "orders.create", "orders.update", "orders.delete" })
        {
            Assert.False(new OperationContract(id).IsReadOnly);
            Assert.Throws<ArgumentException>(() => new OperationContract(id, readOnly: true));
        }

        // 特性只是生成器输入：只读与程序集缺省都有，缺省值分别是 false 与 All
        Assert.False(new OperationAttribute("departments.tree").ReadOnly);
        Assert.Equal(Expose.All, new OperationDefaultsAttribute().Expose);
    }

    [Fact]
    public async Task Invoke_Json_RoundTripsThroughPipeline()
    {
        var (provider, app) = await StartAsync();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var context = Hosts.Context(scope.ServiceProvider);
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        var response = await invoker.InvokeAsync(context, new OperationRequest(EchoOperation.Id, Utf8("""{"text":"hi","repeat":2}""")));

        Assert.True(response.IsSuccess);
        Assert.Equal("hihi", Field(response, "echo"));
        Assert.Equal(context.TraceId, Field(response, "traceId"));
        Assert.Equal(EchoOperation.Id, context.GetOperation().ValueOrThrow().Id);
        Assert.Equal("hi", ((EchoRequest)context.GetOperationRequest().ValueOrThrow()).Text);
        Assert.Equal("hihi", ((EchoResponse)context.GetOperationResponse().ValueOrThrow()).Echo);
        Assert.Equal([EchoOperation.Id], provider.GetRequiredService<OperationCapture>().Seen);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invoke_Json_EmptyPayloadIsEmptyObject_AndInvalidPayloadIsRejected()
    {
        var (provider, app) = await StartAsync();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        // 空载荷按 {} 解码：操作收到空文本并返回自己的校验错误
        var empty = await invoker.InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(EchoOperation.Id));
        Assert.Equal("ECHOES_VALIDATION_001", empty.Error!.Code);
        Assert.True(empty.Payload.IsEmpty);

        var invalid = await invoker.InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(EchoOperation.Id, Utf8("not json")));
        Assert.Equal(KernelErrors.InvalidOperationPayload, invalid.Error!.Code);
        Assert.Equal(EchoOperation.Id, invalid.Error.Details!["id"]);

        var nullPayload = await invoker.InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(EchoOperation.Id, Utf8("null")));
        Assert.Equal(KernelErrors.InvalidOperationPayload, nullPayload.Error!.Code);

        var unknown = await invoker.InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest("echoes.delete"));
        Assert.Equal(KernelErrors.UnknownOperation, unknown.Error!.Code);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invoke_Typed_Works_AndRejectsMismatchedTypes()
    {
        var (provider, app) = await StartAsync();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        var ok = await invoker.InvokeAsync<EchoRequest, EchoResponse>(Hosts.Context(scope.ServiceProvider), EchoOperation.Id, new EchoRequest("x", 3));
        Assert.Equal("xxx", ok.ValueOrThrow().Echo);

        var mismatch = await invoker.InvokeAsync<string, EchoResponse>(Hosts.Context(scope.ServiceProvider), EchoOperation.Id, "x");
        Assert.Equal(KernelErrors.OperationTypeMismatch, mismatch.Error!.Code);

        var unknown = await invoker.InvokeAsync<EchoRequest, EchoResponse>(Hosts.Context(scope.ServiceProvider), "echoes.get", new EchoRequest("x"));
        Assert.Equal(KernelErrors.UnknownOperation, unknown.Error!.Code);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invoke_TranslatesOperationFailureAndException()
    {
        var (provider, app) = await StartAsync();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var invoker = provider.GetRequiredService<IOperationInvoker>();

        var failed = await invoker.InvokeAsync<EchoRequest, EchoResponse>(Hosts.Context(scope.ServiceProvider), EchoOperation.Id, new EchoRequest(""));
        Assert.Equal("ECHOES_VALIDATION_001", failed.Error!.Code);

        // 操作抛出的异常由 Observe 阶段翻译为未预期错误，与普通调用一致
        var crashed = await invoker.InvokeAsync(Hosts.Context(scope.ServiceProvider), new OperationRequest(EchoOperation.Id, Utf8("""{"text":"throw"}""")));
        Assert.Equal(KernelErrors.Unexpected, crashed.Error!.Code);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Execute_RunsInsideAnExistingInvocation()
    {
        var (provider, app) = await StartAsync();
        await using var _ = provider;
        await using var scope = provider.CreateAsyncScope();
        var context = Hosts.Context(scope.ServiceProvider);
        var invoker = provider.GetRequiredService<IOperationInvoker>();
        OperationResponse? inner = null;
        Result<EchoResponse> typed = default;

        var outcome = await app.InvokeAsync(context, async ctx =>
        {
            inner = await invoker.ExecuteAsync(ctx, new OperationRequest(EchoOperation.Id, Utf8("""{"text":"in"}""")));
            typed = await invoker.ExecuteAsync<EchoRequest, EchoResponse>(ctx, EchoOperation.Id, new EchoRequest("side"));
        });

        Assert.True(outcome.IsSuccess);
        Assert.Equal("in", Field(inner!, "echo"));
        Assert.Equal("side", typed.ValueOrThrow().Echo);

        // 管道内执行未登记的操作：结果写进上下文，整次调用失败
        var missing = await app.InvokeAsync(Hosts.Context(scope.ServiceProvider), async ctx => await invoker.ExecuteAsync(ctx, new OperationRequest("echoes.get")));
        Assert.Equal(KernelErrors.UnknownOperation, missing.Error!.Code);
        await app.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void AddOperation_DuplicateIdAcrossFeatures_FailsAtBuild()
    {
        var exception = Assert.Throws<FrameworkException>(static () => Hosts.BuildApp(b => b.UseFeature(new EchoFeature()).UseFeature(new EchoFeature("xihan.test.echo2", capture: false))));

        Assert.Equal(KernelErrors.DuplicateOperation, exception.Code);
    }
}
