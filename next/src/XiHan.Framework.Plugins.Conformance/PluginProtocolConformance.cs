// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Plugins.OutOfProcess;
using XiHan.Framework.Plugins.OutOfProcess.Processes;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using Xunit;
using ProtoLifecycleState = XiHan.Framework.Plugins.Protocol.V1.LifecycleState;

namespace XiHan.Framework.Plugins.Conformance;

/// <summary>
/// 进程外插件协议 <c>xihan.plugin.v1</c> 的黑盒一致性套件。
/// </summary>
/// <remarks>
/// RFC-0017，<c>spec/plugin/invocation.md</c> §7。以宿主身份启动插件目录里的进程，核对握手行、版本协商、清单一致性、生命周期、调用、事件与退出约定。任何语言的插件实现都应以其构建产物目录继承本类运行。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class PluginProtocolConformance
{
    /// <summary>
    /// 可服务的状态集合。
    /// </summary>
    private static readonly ProtoLifecycleState[] _serving = [ProtoLifecycleState.Started, ProtoLifecycleState.Ready, ProtoLifecycleState.Degraded];

    /// <summary>
    /// 被测插件目录：含 <c>xihan.manifest.json</c> 与清单 <c>entry</c> 指向的可执行文件。
    /// </summary>
    protected abstract string PluginDirectory { get; }

    /// <summary>
    /// 每次协议调用的时限。
    /// </summary>
    /// <remarks>
    /// 默认 15 秒。
    /// </remarks>
    protected virtual TimeSpan Timeout => TimeSpan.FromSeconds(15);

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
    /// 调用用例跳过的操作（如故意崩溃或需要宿主回调的操作）。
    /// </summary>
    protected virtual IReadOnlySet<string> ExcludedOperations => new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// 用例 1：握手与清单相符。
    /// </summary>
    /// <remarks>
    /// 进程写出合法握手行、协商出宿主支持的版本、原语层版本相同、清单与目录里的文件一致、操作与订阅都在清单声明范围内。
    /// </remarks>
    [Fact]
    public async Task Handshake_PublishesAnEndpoint_NegotiatesTheVersion_AndMatchesTheManifest()
    {
        await using var session = await LaunchAsync().ConfigureAwait(false);

        Assert.Equal(PluginProtocol.Version, session.Handshake.ProtocolVersion);
        Assert.NotNull(session.Process.Endpoint);
        Assert.All(session.Handshake.Operations, operation => Assert.Contains(operation.Contract.Id, session.Manifest.Contributes.Operations));
        Assert.All(session.Handshake.Catalogs, catalog => Assert.NotEmpty(catalog.Locales));
    }

    /// <summary>
    /// 用例 2：宿主没有共同版本时握手必须以 <c>FailedPrecondition</c> 拒绝。
    /// </summary>
    /// <remarks>
    /// 而不是返回宿主不支持的版本。
    /// </remarks>
    [Fact]
    public async Task Handshake_WithoutACommonVersion_IsRefused()
    {
        await using var process = await StartProcessAsync().ConfigureAwait(false);
        var request = HandshakeRequest([uint.MaxValue]);

        var exception = await Assert.ThrowsAsync<RpcException>(async () => await process.Client.HandshakeAsync(request, deadline: Deadline()).ConfigureAwait(false)).ConfigureAwait(false);

        Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
    }

    /// <summary>
    /// 用例 3：Configure 与 Start 成功。
    /// </summary>
    /// <remarks>
    /// Health 与 WatchHealth 报可服务状态；Stop 成功后进程在宽限内退出。
    /// </remarks>
    [Fact]
    public async Task Lifecycle_ConfigureStartHealthStop_FollowTheProtocol()
    {
        await using var session = await LaunchAsync().ConfigureAwait(false);
        await StartAsync(session).ConfigureAwait(false);

        var health = await session.Client.HealthAsync(new HealthRequest(), deadline: Deadline()).ConfigureAwait(false);
        Assert.Contains(health.State, _serving);
        Assert.All(health.Items, item => Assert.Contains(item.Name, session.Manifest.Contributes.Health));

        using var watch = session.Client.WatchHealth(new HealthRequest(), deadline: Deadline());
        Assert.True(await watch.ResponseStream.MoveNext(CancellationToken.None).ConfigureAwait(false), "WatchHealth must push the current state first.");
        Assert.Contains(watch.ResponseStream.Current.State, _serving);

        var stopped = await session.Client.StopAsync(new StopRequest { GraceSeconds = 5, Reason = "unload" }, deadline: Deadline()).ConfigureAwait(false);
        Assert.True(stopped.Status?.Ok, "Stop must succeed.");
        Assert.True(await session.Process.WaitForExitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None).ConfigureAwait(false), "The plugin must exit within the grace period after Stop.");
    }

    /// <summary>
    /// 用例 4：未声明的操作以 gRPC <c>Unimplemented</c> 结束，而不是业务失败。
    /// </summary>
    [Fact]
    public async Task Invoke_UnknownOperation_IsUnimplemented()
    {
        await using var session = await LaunchAsync().ConfigureAwait(false);
        await StartAsync(session).ConfigureAwait(false);

        var exception = await Assert.ThrowsAsync<RpcException>(async () => await session.Client.InvokeAsync(new InvokeRequest { Operation = "conformance.missing", Payload = ByteString.CopyFromUtf8("{}") }, deadline: Deadline()).ConfigureAwait(false)).ConfigureAwait(false);

        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }

    /// <summary>
    /// 用例 5：每个声明的一元操作都返回带 <c>status</c> 的应答。
    /// </summary>
    /// <remarks>
    /// 成功时载荷是 JSON，失败时错误码合法；gRPC 状态保持 OK。
    /// </remarks>
    [Fact]
    public async Task Invoke_DeclaredOperations_ReturnAStatus()
    {
        await using var session = await LaunchAsync().ConfigureAwait(false);
        await StartAsync(session).ConfigureAwait(false);

        foreach (var operation in session.Handshake.Operations.Where(o => !o.Streaming && !ExcludedOperations.Contains(o.Contract.Id)))
        {
            var payload = SampleRequests.TryGetValue(operation.Contract.Id, out var sample) ? sample : "{}";
            var metadata = new InvocationMetadata { TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", Baggage = { ["xihan.correlation"] = "conformance" } };
            var response = await session.Client.InvokeAsync(new InvokeRequest { Operation = operation.Contract.Id, Payload = ByteString.CopyFromUtf8(payload), Metadata = metadata }, deadline: Deadline()).ConfigureAwait(false);

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
    public async Task HandleEvent_ForADeclaredSubscription_IsAcknowledged()
    {
        await using var session = await LaunchAsync().ConfigureAwait(false);
        var subscription = session.Handshake.Subscriptions.Count > 0 ? session.Handshake.Subscriptions[0] : null;
        Assert.SkipWhen(subscription is null, "The plugin declares no subscriptions.");
        await StartAsync(session).ConfigureAwait(false);

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

        var response = await session.Client.HandleEventAsync(request, deadline: Deadline()).ConfigureAwait(false);

        Assert.True(response.Status?.Ok, "The plugin must acknowledge a message on a declared subscription.");
    }

    /// <summary>
    /// 用例 7：宿主关闭 stdin 后插件必须自行退出（不留孤儿进程）。
    /// </summary>
    [Fact]
    public async Task Process_ExitsWhenStdinCloses()
    {
        await using var session = await LaunchAsync().ConfigureAwait(false);

        session.Process.CloseInput();

        Assert.True(await session.Process.WaitForExitAsync(Timeout, TimeProvider.System, CancellationToken.None).ConfigureAwait(false), "The plugin must exit after its stdin is closed.");
    }

    /// <summary>
    /// 启动进程并读握手行。
    /// </summary>
    private async Task<PluginProcess> StartProcessAsync()
    {
        var manifest = ReadManifest();
        var executable = OutOfProcessPluginHost.ResolveExecutable(PluginDirectory, manifest.Entry ?? string.Empty)
            ?? throw new InvalidOperationException($"The plugin executable '{manifest.Entry}' was not found in '{PluginDirectory}'.");
        var launch = new PluginLaunch(manifest.Id, executable, Path.GetFullPath(PluginDirectory), "conformance-session", HostEndpoint(), manifest.Id + "#conformance", 1, "en-US", Timeout);
        var launched = await PluginProcess.LaunchAsync(launch, TimeProvider.System, NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);
        return launched.ValueOrThrow();
    }

    /// <summary>
    /// 启动、握手并核对清单。
    /// </summary>
    private async Task<PluginSession> LaunchAsync()
    {
        var manifest = ReadManifest();
        var process = await StartProcessAsync().ConfigureAwait(false);
        try
        {
            var response = await process.Client.HandshakeAsync(HandshakeRequest(PluginProtocol.SupportedVersions), deadline: Deadline()).ConfigureAwait(false);
            var validated = HandshakeValidator.Validate(manifest, response, 1, PluginIsolation.OutOfProcess);
            Assert.True(validated.IsSuccess, validated.Error?.Message);
            return new PluginSession(process, manifest, validated.ValueOrThrow());
        }
        catch
        {
            await process.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Configure 与 Start，都必须成功。
    /// </summary>
    private async Task StartAsync(PluginSession session)
    {
        var workDirectory = Path.Combine(Path.GetTempPath(), "xihan-plugins-conformance", session.Manifest.Id);
        Directory.CreateDirectory(workDirectory);
        var configure = new ConfigureRequest { Configuration = ConfigurationJson, WorkDirectory = workDirectory };
        configure.GrantedPermissions.AddRange(session.Manifest.Permissions);
        var configured = await session.Client.ConfigureAsync(configure, deadline: Deadline()).ConfigureAwait(false);
        Assert.True(configured.Status?.Ok, "Configure must succeed.");

        var started = await session.Client.StartAsync(new StartRequest(), deadline: Deadline()).ConfigureAwait(false);
        Assert.True(started.Status?.Ok, "Start must succeed.");
    }

    /// <summary>
    /// 读目录里的清单。
    /// </summary>
    private Manifest ReadManifest()
        => ManifestReader.ReadFile(Path.Combine(PluginDirectory, ManifestReader.FileName)).ValueOrThrow();

    /// <summary>
    /// 以给定版本列表组装握手请求。
    /// </summary>
    private static HandshakeRequest HandshakeRequest(IEnumerable<uint> versions)
    {
        var request = new HandshakeRequest
        {
            Primitives = 1,
            InstanceId = "conformance",
            Host = new HostInfo { FrameworkVersion = FrameworkInfo.Version, Sdk = "dotnet", SdkVersion = Environment.Version.ToString(), Application = "conformance" },
            HostEndpoint = new Endpoint { Network = HostEndpoint().Network, Address = HostEndpoint().Address },
            DefaultLocale = "en-US",
        };
        request.ProtocolVersions.AddRange(versions);
        return request;
    }

    /// <summary>
    /// 占位的宿主回调端点：套件不提供回调服务，只验证插件侧。
    /// </summary>
    private static PluginEndpoint HostEndpoint()
        => OperatingSystem.IsWindows()
            ? new PluginEndpoint(PluginEndpoint.NamedPipe, @"\\.\pipe\xihan-conformance-host")
            : new PluginEndpoint(PluginEndpoint.Unix, Path.Combine(Path.GetTempPath(), "xihan-conformance-host.sock"));

    /// <summary>
    /// 本次调用的截止时间。
    /// </summary>
    private DateTime Deadline() => DateTime.UtcNow + Timeout;
}
