// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Wasmtime;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Plugins.Wasm;
using XiHan.Framework.Plugins.Wasm.Instances;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using Xunit;
using ProtoLifecycleState = XiHan.Framework.Plugins.Protocol.V1.LifecycleState;

namespace XiHan.Framework.Plugins.Conformance;

/// <summary>
/// Wasm 载体的黑盒一致性套件（RFC-0018，<c>spec/plugin/wasm.md</c> §7）。
/// </summary>
/// <remarks>
/// 以模块文件为输入，核对导出表与 ABI 版本、握手一致性、生命周期、调用、事件与中间件钩子。任何语言编译出的插件都应以其产物目录继承本类运行。
/// </remarks>
[ApiLevel(Stability.Experimental, "5.0")]
public abstract class WasmPluginConformance
{
    /// <summary>
    /// 可服务的状态集合。
    /// </summary>
    private static readonly ProtoLifecycleState[] _serving = [ProtoLifecycleState.Started, ProtoLifecycleState.Ready, ProtoLifecycleState.Degraded];

    /// <summary>
    /// 被测插件目录：含 <c>xihan.manifest.json</c> 与清单 <c>entry</c> 指向的模块文件。
    /// </summary>
    protected abstract string PluginDirectory { get; }

    /// <summary>
    /// <c>Configure</c> 下发的配置节 JSON。
    /// </summary>
    /// <remarks>
    /// 默认空对象。
    /// </remarks>
    protected virtual string ConfigurationJson => "{}";

    /// <summary>
    /// 调用用例使用的示例请求（操作 ID → JSON）。
    /// </summary>
    /// <remarks>
    /// 没有示例的操作以 <c>{}</c> 调用。
    /// </remarks>
    protected virtual IReadOnlyDictionary<string, string> SampleRequests => new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 调用用例跳过的操作（如故意陷阱、忙等或需要宿主回调的操作）。
    /// </summary>
    protected virtual IReadOnlySet<string> ExcludedOperations => new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// 用例 1：模块是 reactor、导出表与导入表符合 ABI v1、<c>xihan_abi_version</c> 返回 1。
    /// </summary>
    [Fact]
    public void Module_ExportsFollowTheAbi()
    {
        using var session = Open();

        Assert.Equal(PluginProtocol.Version, session.Instance.AbiVersion);
        Assert.All(WasmModule.RequiredCalls, call => Assert.True(session.Instance.Supports(call), $"missing export '{call}'"));
    }

    /// <summary>
    /// 用例 2：握手与清单相符。
    /// </summary>
    /// <remarks>
    /// 握手协商出版本 1、清单与目录里的文件一致、操作与订阅在清单声明范围内、没有流式操作。
    /// </remarks>
    [Fact]
    public void Handshake_MatchesTheManifest()
    {
        using var session = Open();

        Assert.Equal(PluginProtocol.Version, session.Handshake.ProtocolVersion);
        Assert.All(session.Handshake.Operations, operation => Assert.Contains(operation.Contract.Id, session.Manifest.Contributes.Operations));
        Assert.DoesNotContain(session.Handshake.Operations, static o => o.Streaming);
        if (session.Handshake.Subscriptions.Count > 0)
        {
            Assert.True(session.Module.HasEventHandler, "a plugin declaring subscriptions must export 'xihan_handle_event'");
        }

        if (session.Manifest.Contributes.Pipeline.Count > 0)
        {
            Assert.True(session.Module.HasMiddleware, "a plugin contributing middleware must export 'xihan_middleware'");
        }
    }

    /// <summary>
    /// 用例 3：生命周期按协议进行。
    /// </summary>
    /// <remarks>
    /// Configure 与 Start 成功、Health 报可服务状态且健康项都已在清单声明、Stop 成功。
    /// </remarks>
    [Fact]
    public void Lifecycle_ConfigureStartHealthStop_FollowTheProtocol()
    {
        using var session = Open();
        Start(session);

        var health = session.Call<HealthResponse>("xihan_health", new HealthRequest());
        Assert.Contains(health.State, _serving);
        Assert.All(health.Items, item => Assert.Contains(item.Name, session.Manifest.Contributes.Health));

        var stopped = session.Call<StopResponse>("xihan_stop", new StopRequest { GraceSeconds = 5, Reason = "unload" });
        Assert.True(stopped.Status?.Ok, "Stop must succeed.");
    }

    /// <summary>
    /// 用例 4：未声明的操作以 <c>KERNEL_NOTFOUND_001</c> 的失败状态应答，而不是陷阱。
    /// </summary>
    [Fact]
    public void Invoke_UnknownOperation_IsNotFound()
    {
        using var session = Open();
        Start(session);

        var response = session.Call<InvokeResponse>("xihan_invoke", new InvokeRequest { Operation = "conformance.missing", Payload = ByteString.CopyFromUtf8("{}") });

        Assert.False(response.Status?.Ok);
        Assert.Equal(KernelErrors.UnknownOperation, response.Status?.Error?.Code);
    }

    /// <summary>
    /// 用例 5：每个声明的操作都返回带 <c>status</c> 的应答。
    /// </summary>
    /// <remarks>
    /// 成功时载荷是 JSON，失败时错误码合法。
    /// </remarks>
    [Fact]
    public void Invoke_DeclaredOperations_ReturnAStatus()
    {
        using var session = Open();
        Start(session);

        foreach (var operation in session.Handshake.Operations.Where(o => !ExcludedOperations.Contains(o.Contract.Id)))
        {
            var payload = SampleRequests.TryGetValue(operation.Contract.Id, out var sample) ? sample : "{}";
            var metadata = new InvocationMetadata { TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", Baggage = { ["xihan.correlation"] = "conformance" } };
            var response = session.Call<InvokeResponse>("xihan_invoke", new InvokeRequest { Operation = operation.Contract.Id, Payload = ByteString.CopyFromUtf8(payload), Metadata = metadata });

            Assert.True(response.Status is not null, $"Operation '{operation.Contract.Id}' must return a status.");
            if (response.Status.Ok)
            {
                if (response.Payload.Length > 0)
                {
                    using var document = JsonDocument.Parse(response.Payload.Memory);
                    Assert.NotEqual(JsonValueKind.Undefined, document.RootElement.ValueKind);
                }
            }
            else
            {
                Assert.NotNull(response.Status.Error);
                Assert.True(ErrorCode.IsValid(response.Status.Error.Code), $"Operation '{operation.Contract.Id}' returned an invalid error code '{response.Status.Error.Code}'.");
            }
        }
    }

    /// <summary>
    /// 用例 6：向声明的订阅投递一条消息必须得到确认（没有订阅时跳过）。
    /// </summary>
    [Fact]
    public void HandleEvent_ForADeclaredSubscription_IsAcknowledged()
    {
        using var session = Open();
        var subscription = session.Handshake.Subscriptions.Count > 0 ? session.Handshake.Subscriptions[0] : null;
        Assert.SkipWhen(subscription is null, "The plugin declares no subscriptions.");
        Start(session);

        var request = new HandleEventRequest
        {
            Envelope = new Envelope
            {
                Topic = subscription!.Topic,
                Id = Guid.NewGuid().ToString("N"),
                Source = "urn:xihan:conformance",
                Type = subscription.MessageType ?? "conformance.event",
                SpecVersion = "1.0",
                DataContentType = "application/json",
                Data = ByteString.CopyFromUtf8("{}"),
            },
            Metadata = new InvocationMetadata { TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
            Attempt = 1,
        };

        var response = session.Call<HandleEventResponse>("xihan_handle_event", request);

        Assert.True(response.Status?.Ok, "The plugin must acknowledge a message on a declared subscription.");
    }

    /// <summary>
    /// 用例 7：每个贡献的中间件在 before 阶段返回合法动作（短路时带失败结果）。
    /// </summary>
    /// <remarks>
    /// 在 after 阶段也应答（没有中间件贡献时跳过）。
    /// </remarks>
    [Fact]
    public void Middleware_BeforeAndAfter_ReturnValidDecisions()
    {
        using var session = Open();
        Assert.SkipWhen(session.Manifest.Contributes.Pipeline.Count == 0, "The plugin contributes no middleware.");
        Start(session);

        foreach (var contribution in session.Manifest.Contributes.Pipeline)
        {
            var metadata = new InvocationMetadata { TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" };
            var before = session.Call<MiddlewareResponse>("xihan_middleware", new MiddlewareRequest { Middleware = contribution.Middleware, Stage = contribution.Stage.ToManifestName(), Phase = "before", Metadata = metadata, Operation = "conformance.probe" });
            Assert.True(before.Action is MiddlewareAction.Continue or MiddlewareAction.ShortCircuit, $"Middleware '{contribution.Middleware}' must return CONTINUE or SHORT_CIRCUIT.");
            if (before.Action == MiddlewareAction.ShortCircuit)
            {
                Assert.True(before.Outcome is { Ok: false, Error: not null }, $"Middleware '{contribution.Middleware}' short-circuits without a failure outcome.");
            }

            var after = session.Call<MiddlewareResponse>("xihan_middleware", new MiddlewareRequest { Middleware = contribution.Middleware, Stage = contribution.Stage.ToManifestName(), Phase = "after", Metadata = metadata, Operation = "conformance.probe", Outcome = new Status { Ok = true } });
            Assert.NotNull(after);
        }
    }

    /// <summary>
    /// 编译、校验、实例化并握手。
    /// </summary>
    private WasmSession Open()
    {
        // 套件会故意触发陷阱：影子栈强制的测试进程先以配置错误失败，而不是整个进程 fail-fast
        ShadowStackGuard.EnsureTrapsAreSafe();
        var manifest = ManifestReader.ReadFile(Path.Combine(PluginDirectory, ManifestReader.FileName)).ValueOrThrow();
        var modulePath = Path.GetFullPath(Path.Combine(PluginDirectory, manifest.Entry ?? string.Empty));
        var engine = new Engine(new Config().WithEpochInterruption(true));
        var module = WasmModule.Load(engine, modulePath, manifest.Id);
        Assert.True(module.IsSuccess, module.Error?.Message);
        var workDirectory = Path.Combine(Path.GetTempPath(), "xihan-plugins-conformance", manifest.Id);
        Directory.CreateDirectory(workDirectory);
        var context = new WasmHostContext(engine, new WasmPluginOptions(), new ConfigurationBuilder().Build(), TimeProvider.System, 1, "en-US", "conformance", workDirectory);
        var functions = new WasmHostFunctions(manifest, new PermissionGrant(manifest.Permissions), context, NullLogger.Instance);
        var wasi = new WasiConfiguration().WithStandardOutput(Path.Combine(workDirectory, "stdout.log")).WithStandardError(Path.Combine(workDirectory, "stderr.log"));
        var instance = new WasmInstance(engine, module.ValueOrThrow(), functions, wasi, 64L * 1024 * 1024, TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(30));
        var session = new WasmSession(engine, module.ValueOrThrow(), instance, manifest, workDirectory);
        try
        {
            var request = new HandshakeRequest { Primitives = 1, InstanceId = "conformance", Host = new HostInfo { FrameworkVersion = FrameworkInfo.Version, Sdk = "dotnet", SdkVersion = Environment.Version.ToString(), Application = "conformance" }, DefaultLocale = "en-US" };
            request.ProtocolVersions.AddRange(PluginProtocol.SupportedVersions);
            var response = session.Call<HandshakeResponse>("xihan_handshake", request);
            var validated = HandshakeValidator.Validate(manifest, response, 1, PluginIsolation.Wasm);
            Assert.True(validated.IsSuccess, validated.Error?.Message);
            session.Handshake = validated.ValueOrThrow();
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Configure 与 Start，都必须成功。
    /// </summary>
    private void Start(WasmSession session)
    {
        var configure = new ConfigureRequest { Configuration = ConfigurationJson, WorkDirectory = session.WorkDirectory };
        configure.GrantedPermissions.AddRange(session.Manifest.Permissions);
        Assert.True(session.Call<ConfigureResponse>("xihan_configure", configure).Status?.Ok, "Configure must succeed.");
        Assert.True(session.Call<StartResponse>("xihan_start", new StartRequest()).Status?.Ok, "Start must succeed.");
    }

    /// <summary>
    /// 一个已握手的被测实例。
    /// </summary>
    private sealed class WasmSession(Engine engine, WasmModule module, WasmInstance instance, Manifest manifest, string workDirectory) : IDisposable
    {
        /// <summary>
        /// 模块。
        /// </summary>
        public WasmModule Module { get; } = module;

        /// <summary>
        /// 实例。
        /// </summary>
        public WasmInstance Instance { get; } = instance;

        /// <summary>
        /// 目录里的清单。
        /// </summary>
        public Manifest Manifest { get; } = manifest;

        /// <summary>
        /// 工作目录。
        /// </summary>
        public string WorkDirectory { get; } = workDirectory;

        /// <summary>
        /// 核对后的握手结果。
        /// </summary>
        public HandshakeInfo Handshake { get; set; } = null!;

        /// <summary>
        /// 一次调用。
        /// </summary>
        /// <remarks>
        /// 编码请求、调用导出、解码响应；陷阱或无响应即失败。
        /// </remarks>
        public TResponse Call<TResponse>(string export, IMessage request)
            where TResponse : IMessage<TResponse>, new()
        {
            var outcome = Instance.Call(export, PluginJsonCodec.Encode(request), TimeSpan.FromSeconds(30));
            Assert.True(outcome.Status == WasmCallStatus.Success, $"'{export}' failed: {outcome.Status} {outcome.Detail}");
            var response = PluginJsonCodec.Decode<TResponse>(outcome.Response);
            Assert.True(response is not null, $"'{export}' returned a response that is not a valid JSON message.");
            return response!;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Instance.Dispose();
            Module.Dispose();
            engine.Dispose();
        }
    }
}
