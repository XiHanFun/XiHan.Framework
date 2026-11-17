// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
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
using ProtoStatus = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace XiHan.Framework.Plugins.Conformance;

/// <summary>
/// 进程外插件协议 <c>xihan.plugin.v1</c> 的黑盒一致性套件
/// </summary>
/// <remarks>
/// RFC-0017，<c>spec/plugin/invocation.md</c> §7。以宿主身份启动插件目录里的进程，核对握手行、版本协商、清单一致性、生命周期、调用次序、调用、事件与退出约定。任何语言的插件实现都应以其构建产物目录继承本类运行。用例与 framework-spec 的运行器 <c>xihan-conformance plugin</c> 一一对应，每个用例的说明里写着它在运行器里的名字；插件都以两项的 <c>XIHAN_PLUGIN_HOST_ADDRESS</c> 启动（<c>handshake.md</c> §4），套件不提供 <c>Host</c> 服务，两项都没人监听
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class PluginProtocolConformance
{
    /// <summary>
    /// Start 之后健康应答里可接受的状态
    /// </summary>
    private static readonly ProtoLifecycleState[] _serving = [ProtoLifecycleState.Started, ProtoLifecycleState.Ready, ProtoLifecycleState.Degraded];

    /// <summary>
    /// 用例调用携带的 W3C traceparent
    /// </summary>
    private const string TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    /// <summary>
    /// 用例里调用的未声明操作
    /// </summary>
    private const string MissingOperation = "conformance.missing";

    /// <summary>
    /// 用例里带的错误会话令牌
    /// </summary>
    private const string WrongToken = "not-the-session-token";

    /// <summary>
    /// UTF-8 BOM 解码后的字符
    /// </summary>
    private const char ByteOrderMark = (char)0xFEFF;

    /// <summary>
    /// 投递给没有限定类型的订阅时用的消息类型
    /// </summary>
    private const string EventType = "conformance.event";

    /// <summary>
    /// 配置节不是 JSON 对象时的 <c>reason</c>
    /// </summary>
    /// <remarks>
    /// <c>lifecycle.md</c> §7.2、<c>plugin/plugin-messages.json</c>
    /// </remarks>
    private const string ConfigurationReason = "the configuration is not a JSON object";

    /// <summary>
    /// 本实例的会话令牌
    /// </summary>
    /// <remarks>
    /// 32 字节随机数（base64url），每个用例一个实例
    /// </remarks>
    private readonly string _token = Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// 本实例交给插件的回调端点列表
    /// </summary>
    private readonly Lazy<IReadOnlyList<PluginEndpoint>> _hostEndpoints = new(CreateHostEndpoints);

    /// <summary>
    /// 被测插件目录：含 <c>xihan.manifest.json</c> 与清单 <c>entry</c> 指向的可执行文件
    /// </summary>
    protected abstract string PluginDirectory { get; }

    /// <summary>
    /// 每次协议调用的时限
    /// </summary>
    /// <remarks>
    /// 默认 15 秒
    /// </remarks>
    protected virtual TimeSpan Timeout => TimeSpan.FromSeconds(15);

    /// <summary>
    /// <c>Configure</c> 下发的配置节 JSON
    /// </summary>
    /// <remarks>
    /// 默认空对象
    /// </remarks>
    protected virtual string ConfigurationJson => "{}";

    /// <summary>
    /// 调用用例使用的示例请求（操作 ID → JSON）
    /// </summary>
    /// <remarks>
    /// 没有示例的操作以 <c>{}</c> 调用
    /// </remarks>
    protected virtual IReadOnlyDictionary<string, string> SampleRequests => new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 调用用例跳过的操作（如故意崩溃或需要宿主回调的操作）
    /// </summary>
    protected virtual IReadOnlySet<string> ExcludedOperations => new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// 插件是否校验宿主调用带的会话令牌
    /// </summary>
    /// <remarks>
    /// 缺省 <see langword="true"/>，规范要求校验（<c>spec/plugin/handshake.md</c> §4，ADR-0202）；只在插件 SDK 还没有发布校验令牌的版本时临时覆写为 <see langword="false"/>，用例随之跳过
    /// </remarks>
    protected virtual bool VerifiesSessionToken => true;

    /// <summary>
    /// 插件是否实现了插件一侧的规则
    /// </summary>
    /// <remarks>
    /// 缺省 <see langword="true"/>，规范要求（<c>lifecycle.md</c> §7、<c>invocation.md</c> §1、§4，ADR-0237）；只在插件 SDK 还没有发布实现这些规则的版本时临时覆写为 <see langword="false"/>：用例 order、unavailable、unsubscribed、stream、configuration、stop-early 随之跳过，handshake 不做插件一侧写法的补充核对
    /// </remarks>
    protected virtual bool FollowsPluginSideRules => true;

    /// <summary>
    /// 插件一侧的规则还没实现时跳过的原因
    /// </summary>
    private const string PluginSideRulesPending = "The plugin SDK does not follow the plugin-side rules yet (ADR-0237).";

    /// <summary>
    /// 用例 handshake：握手与清单相符
    /// </summary>
    /// <remarks>
    /// 进程写出合法握手行、协商出宿主支持的版本、原语层版本相同、清单与目录里的文件一致、操作与订阅都在清单声明范围内；清单不写 <c>contributes.pipeline</c>（进程外插件不能贡献中间件，RFC-0017「范围」）。插件一侧的写法（<c>handshake.md</c> §3，ADR-0237）：<c>primitives</c> 取自清单，清单原文不以 BOM 开头，只声明一元操作，订阅的方式总是 <c>competing</c> 或 <c>broadcast</c>
    /// </remarks>
    [Fact]
    public async Task Handshake_PublishesAnEndpoint_NegotiatesTheVersion_AndMatchesTheManifest()
    {
        await using var session = await LaunchAsync().ConfigureAwait(false);

        Assert.True(session.Manifest.Contributes.Pipeline.Count == 0, "An out-of-process plugin cannot contribute middleware; the host rejects a manifest with 'contributes.pipeline' at discovery.");
        Assert.Equal(PluginProtocol.Version, session.Handshake.ProtocolVersion);
        Assert.NotNull(session.Process.Endpoint);
        Assert.All(session.Handshake.Operations, operation => Assert.Contains(operation.Contract.Id, session.Manifest.Contributes.Operations));
        Assert.All(session.Handshake.Catalogs, catalog => Assert.NotEmpty(catalog.Locales));
        if (!FollowsPluginSideRules)
        {
            return;
        }

        Assert.True(session.Response.Primitives == session.Manifest.Primitives, $"The handshake reports primitives {session.Response.Primitives}, want the manifest's {session.Manifest.Primitives}.");
        Assert.False(session.Response.Manifest.StartsWith(ByteOrderMark), "The manifest in the handshake must not start with a byte order mark.");
        foreach (var operation in session.Response.Operations)
        {
            Assert.False(operation.Streaming, $"Operation '{operation.Id}' is declared with streaming: true; plugins declare only unary operations.");
        }

        foreach (var subscription in session.Response.Subscriptions)
        {
            Assert.True(subscription.Mode is PluginSubscriptionModes.Competing or PluginSubscriptionModes.Broadcast, $"The subscription to '{subscription.Topic}' (type '{subscription.MessageType}') has mode '{subscription.Mode}'; want competing or broadcast.");
        }
    }

    /// <summary>
    /// 用例 refusal：宿主没有共同版本时握手必须以 <c>FailedPrecondition</c> 拒绝
    /// </summary>
    /// <remarks>
    /// 而不是返回宿主不支持的版本
    /// </remarks>
    [Fact]
    public async Task Handshake_WithoutACommonVersion_IsRefused()
    {
        await using var process = await StartProcessAsync().ConfigureAwait(false);
        var request = HandshakeRequest([uint.MaxValue]);

        await ExpectCodeAsync(async () => await process.Client.HandshakeAsync(request, deadline: Deadline()).ConfigureAwait(false), StatusCode.FailedPrecondition, "A handshake without a common version").ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 lifecycle：Configure 与 Start 成功
    /// </summary>
    /// <remarks>
    /// Health 与 WatchHealth 报可服务状态；Stop 成功后进程在宽限内退出
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
    /// 用例 order：调用次序不对的生命周期调用以 <c>FailedPrecondition</c> 拒绝、状态不变
    /// </summary>
    /// <remarks>
    /// <c>lifecycle.md</c> §7.1、§7.3、§7.4（ADR-0237）：握手前的 Configure 与 Stop、第二次 Handshake、没 Configure 就 Start、第二次 Configure、第二次 Start 都被拒；Start 之前的 Health 报本地状态（Resolved、Configured）且没有健康项；Start 应答时已经求过一次健康，之后是 Ready 或 Degraded
    /// </remarks>
    [Fact]
    public async Task Order_CallsOutOfSequence_AreFailedPrecondition()
    {
        Assert.SkipWhen(!FollowsPluginSideRules, PluginSideRulesPending);
        await using var process = await StartProcessAsync().ConfigureAwait(false);
        var client = process.Client;
        var manifest = ReadManifest();

        await ExpectCodeAsync(async () => await client.ConfigureAsync(ConfigureRequest(manifest, ConfigurationJson), deadline: Deadline()).ConfigureAwait(false), StatusCode.FailedPrecondition, "Configure before Handshake").ConfigureAwait(false);
        await ExpectCodeAsync(async () => await client.StopAsync(new StopRequest { GraceSeconds = 5, Reason = "unload" }, deadline: Deadline()).ConfigureAwait(false), StatusCode.FailedPrecondition, "Stop before Handshake").ConfigureAwait(false);

        var response = await client.HandshakeAsync(HandshakeRequest(PluginProtocol.SupportedVersions), deadline: Deadline()).ConfigureAwait(false);
        var validated = HandshakeValidator.Validate(manifest, response, 1, PluginIsolation.OutOfProcess);
        Assert.True(validated.IsSuccess, validated.Error?.Message);
        await ExpectCodeAsync(async () => await client.HandshakeAsync(HandshakeRequest(PluginProtocol.SupportedVersions), deadline: Deadline()).ConfigureAwait(false), StatusCode.FailedPrecondition, "A second Handshake").ConfigureAwait(false);
        await ExpectCodeAsync(async () => await client.StartAsync(new StartRequest(), deadline: Deadline()).ConfigureAwait(false), StatusCode.FailedPrecondition, "Start without Configure").ConfigureAwait(false);
        await ExpectHealthAsync(client, "after Handshake", noItems: true, ProtoLifecycleState.Resolved).ConfigureAwait(false);

        await ConfigureAsync(client, manifest).ConfigureAwait(false);
        await ExpectHealthAsync(client, "after Configure", noItems: true, ProtoLifecycleState.Configured).ConfigureAwait(false);
        await ExpectCodeAsync(async () => await client.ConfigureAsync(ConfigureRequest(manifest, ConfigurationJson), deadline: Deadline()).ConfigureAwait(false), StatusCode.FailedPrecondition, "A second Configure").ConfigureAwait(false);

        var started = await client.StartAsync(new StartRequest(), deadline: Deadline()).ConfigureAwait(false);
        Assert.True(started.Status?.Ok, $"Start must succeed, got {Describe(started.Status)}.");
        await ExpectCodeAsync(async () => await client.StartAsync(new StartRequest(), deadline: Deadline()).ConfigureAwait(false), StatusCode.FailedPrecondition, "A second Start").ConfigureAwait(false);
        // Start 应答之前插件已经按健康的结果进入 Ready、Degraded 或 Failed；Failed 不可服务，不算通过
        await ExpectHealthAsync(client, "after Start", noItems: false, ProtoLifecycleState.Ready, ProtoLifecycleState.Degraded).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 unknown-operation：未声明的操作以 gRPC <c>Unimplemented</c> 结束，而不是业务失败
    /// </summary>
    [Fact]
    public async Task Invoke_UnknownOperation_IsUnimplemented()
    {
        await using var session = await LaunchAsync().ConfigureAwait(false);
        await StartAsync(session).ConfigureAwait(false);

        await ExpectCodeAsync(async () => await session.Client.InvokeAsync(new InvokeRequest { Operation = MissingOperation, Payload = ByteString.CopyFromUtf8("{}") }, deadline: Deadline()).ConfigureAwait(false), StatusCode.Unimplemented, "Invoking an undeclared operation").ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 declared-operations：每个声明的一元操作都返回带 <c>status</c> 的应答
    /// </summary>
    /// <remarks>
    /// 成功时载荷是 JSON，失败时错误码合法；gRPC 状态保持 OK
    /// </remarks>
    [Fact]
    public async Task Invoke_DeclaredOperations_ReturnAStatus()
    {
        await using var session = await LaunchAsync().ConfigureAwait(false);
        await StartAsync(session).ConfigureAwait(false);

        foreach (var operation in session.Handshake.Operations.Where(o => !o.Streaming && !ExcludedOperations.Contains(o.Contract.Id)))
        {
            var response = await session.Client.InvokeAsync(InvokeRequest(operation.Contract.Id), deadline: Deadline()).ConfigureAwait(false);

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
    /// 用例 unavailable：Start 之前的调用与投递
    /// </summary>
    /// <remarks>
    /// <c>invocation.md</c> §1、§4，<c>lifecycle.md</c> §7.3（ADR-0237）：Configure 之后、Start 之前，未声明的操作仍先报 gRPC <c>Unimplemented</c>；声明的操作与第一个订阅的投递都以 <c>PLUGINS_UNAVAILABLE_001</c> + <c>plugins.process.unavailable</c> 失败，<c>state</c> 为 <c>Configured</c>
    /// </remarks>
    [Fact]
    public async Task Unavailable_BeforeStart_CallsAndDeliveriesAreRefused()
    {
        Assert.SkipWhen(!FollowsPluginSideRules, PluginSideRulesPending);
        await using var session = await LaunchAsync().ConfigureAwait(false);
        await ConfigureAsync(session.Client, session.Manifest).ConfigureAwait(false);

        // 操作是否声明先于就绪核对
        await ExpectCodeAsync(async () => await session.Client.InvokeAsync(new InvokeRequest { Operation = MissingOperation, Payload = ByteString.CopyFromUtf8("{}") }, deadline: Deadline()).ConfigureAwait(false), StatusCode.Unimplemented, "Invoking an undeclared operation before Start").ConfigureAwait(false);

        var details = Details(("id", session.Manifest.Id), ("state", "Configured"));
        foreach (var operation in session.Response.Operations.Where(o => !ExcludedOperations.Contains(o.Id)))
        {
            var response = await session.Client.InvokeAsync(InvokeRequest(operation.Id), deadline: Deadline()).ConfigureAwait(false);
            ExpectFailure(response.Status, PluginErrors.Unavailable, PluginMessages.ProcessUnavailable, details, $"Operation '{operation.Id}' invoked before Start");
        }

        if (session.Response.Subscriptions.Count > 0)
        {
            var subscription = session.Response.Subscriptions[0];
            var response = await session.Client.HandleEventAsync(EventRequest(subscription.Topic, TypeOf(subscription), DeliveryMode(subscription.Mode)), deadline: Deadline()).ConfigureAwait(false);
            ExpectFailure(response.Status, PluginErrors.Unavailable, PluginMessages.ProcessUnavailable, details, $"A delivery to the subscription '{subscription.Topic}' before Start");
        }
    }

    /// <summary>
    /// 用例 event：向声明的订阅投递一条消息必须得到确认（没有订阅时跳过）
    /// </summary>
    [Fact]
    public async Task HandleEvent_ForADeclaredSubscription_IsAcknowledged()
    {
        await using var session = await LaunchAsync().ConfigureAwait(false);
        var subscription = session.Response.Subscriptions.Count > 0 ? session.Response.Subscriptions[0] : null;
        Assert.SkipWhen(subscription is null, "The plugin declares no subscriptions.");
        await StartAsync(session).ConfigureAwait(false);

        // 与宿主一样按订阅声明的方式投递（ADR-0226）
        var response = await session.Client.HandleEventAsync(EventRequest(subscription!.Topic, TypeOf(subscription), DeliveryMode(subscription.Mode)), deadline: Deadline()).ConfigureAwait(false);

        Assert.True(response.Status?.Ok, $"The plugin must acknowledge a message on a declared subscription, got {Describe(response.Status)}.");
    }

    /// <summary>
    /// 用例 unsubscribed：投递找不到订阅，或方式与信封不合法
    /// </summary>
    /// <remarks>
    /// <c>invocation.md</c> §4（ADR-0237）：Start 之后向没人订阅的主题投递，以 <c>KERNEL_NOTFOUND_001</c> + <c>plugins.event.unsubscribed</c> 失败，方式为空时按 <c>competing</c>；未知的投递方式与没有信封的投递以 gRPC <c>InvalidArgument</c> 拒绝
    /// </remarks>
    [Fact]
    public async Task Unsubscribed_DeliveryWithoutASubscription_IsNotFound()
    {
        Assert.SkipWhen(!FollowsPluginSideRules, PluginSideRulesPending);
        await using var session = await LaunchAsync().ConfigureAwait(false);
        await StartAsync(session).ConfigureAwait(false);

        // 随机主题不会碰上插件的订阅
        var topic = "conformance.unsubscribed-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var details = Details(("id", session.Manifest.Id), ("topic", topic), ("type", EventType), ("mode", PluginSubscriptionModes.Competing));
        foreach (var mode in new[] { PluginSubscriptionModes.Competing, string.Empty })
        {
            var response = await session.Client.HandleEventAsync(EventRequest(topic, EventType, mode), deadline: Deadline()).ConfigureAwait(false);
            ExpectFailure(response.Status, KernelNotFound, PluginMessages.EventUnsubscribed, details, $"A delivery to the unsubscribed topic '{topic}' with mode '{mode}'");
        }

        await ExpectCodeAsync(async () => await session.Client.HandleEventAsync(EventRequest(topic, EventType, "bogus"), deadline: Deadline()).ConfigureAwait(false), StatusCode.InvalidArgument, "A delivery with mode 'bogus'").ConfigureAwait(false);
        var withoutEnvelope = EventRequest(topic, EventType, PluginSubscriptionModes.Competing);
        withoutEnvelope.Envelope = null;
        await ExpectCodeAsync(async () => await session.Client.HandleEventAsync(withoutEnvelope, deadline: Deadline()).ConfigureAwait(false), StatusCode.InvalidArgument, "A delivery without an envelope").ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 stream：<c>InvokeStream</c> 按第一帧结束
    /// </summary>
    /// <remarks>
    /// <c>invocation.md</c> §1（ADR-0237）：流式契约之前插件不实现流式调用。第一帧之前结束或第一帧不是 <c>start</c> 时以 gRPC <c>InvalidArgument</c> 结束流，第一帧是 <c>start</c> 时以 gRPC <c>Unimplemented</c> 结束流，不论操作是否声明
    /// </remarks>
    [Fact]
    public async Task InvokeStream_EndsOnTheFirstFrame()
    {
        Assert.SkipWhen(!FollowsPluginSideRules, PluginSideRulesPending);
        await using var session = await LaunchAsync().ConfigureAwait(false);
        await StartAsync(session).ConfigureAwait(false);

        await ExpectStreamAsync(session.Client, new StreamFrame { Chunk = ByteString.CopyFromUtf8("{}") }, StatusCode.InvalidArgument, "An InvokeStream whose first frame is a chunk").ConfigureAwait(false);
        await ExpectStreamAsync(session.Client, null, StatusCode.InvalidArgument, "An InvokeStream closed before its first frame").ConfigureAwait(false);
        var operations = new List<string> { MissingOperation };
        if (session.Response.Operations.FirstOrDefault(o => !ExcludedOperations.Contains(o.Id)) is { } declared)
        {
            operations.Add(declared.Id);
        }

        foreach (var operation in operations)
        {
            await ExpectStreamAsync(session.Client, new StreamFrame { Start = InvokeRequest(operation) }, StatusCode.Unimplemented, $"An InvokeStream that starts operation '{operation}'").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 用例 configuration：配置节不是 JSON 对象时插件进入 Failed
    /// </summary>
    /// <remarks>
    /// <c>lifecycle.md</c> §7.2、§7.5（ADR-0237）：数组与语法错误的配置节各开一个进程，Configure 以 <c>PLUGINS_UNEXPECTED_001</c> + <c>plugins.process.failed</c>（<c>phase</c> 为 <c>configure</c>）失败，之后 Health 报 Failed，Stop 应答 ok、进程在 5 秒内以退出码 0 退出
    /// </remarks>
    [Fact]
    public async Task Configuration_NotAJsonObject_FailsThePlugin()
    {
        Assert.SkipWhen(!FollowsPluginSideRules, PluginSideRulesPending);
        foreach (var configuration in new[] { "[]", """{"a":""" })
        {
            await using var session = await LaunchAsync().ConfigureAwait(false);
            var what = $"Configure with the configuration {configuration}";

            var response = await session.Client.ConfigureAsync(ConfigureRequest(session.Manifest, configuration), deadline: Deadline()).ConfigureAwait(false);

            ExpectFailure(response.Status, PluginErrors.Unexpected, PluginMessages.ProcessFailed, Details(("id", session.Manifest.Id), ("phase", "configure"), ("reason", ConfigurationReason)), what);
            await ExpectHealthAsync(session.Client, "after " + what, noItems: false, ProtoLifecycleState.Failed).ConfigureAwait(false);
            await StopAndExitAsync(session, 5, "after " + what).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 用例 stop-early：握手之后直接 Stop
    /// </summary>
    /// <remarks>
    /// <c>lifecycle.md</c> §7.5（ADR-0237）：启动回调没有成功过时不调停止回调，<c>grace_seconds</c> 为 0 的 Stop 应答 ok，进程在 5 秒内以退出码 0 退出
    /// </remarks>
    [Fact]
    public async Task Stop_BeforeStart_AnswersAndExits()
    {
        Assert.SkipWhen(!FollowsPluginSideRules, PluginSideRulesPending);
        await using var session = await LaunchAsync().ConfigureAwait(false);

        await StopAndExitAsync(session, 0, "right after the handshake").ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 session：不带会话令牌、令牌不对或带了多个值的调用以 Unauthenticated 拒绝
    /// </summary>
    /// <remarks>
    /// 本机别的进程连上插件端点也冒充不了宿主；元数据里必须恰好一个值，两个值即使有一个对（不论先后）、两个都对也拒绝；带对了照常握手（<c>spec/plugin/handshake.md</c> §4，ADR-0202、ADR-0237）
    /// </remarks>
    [Fact]
    public async Task Calls_WithoutTheSessionToken_AreUnauthenticated()
    {
        Assert.SkipWhen(!VerifiesSessionToken, "The plugin SDK does not verify the session token yet (ADR-0202).");
        await using var process = await StartProcessAsync().ConfigureAwait(false);
        using var channel = PluginChannelFactory.Create(process.Endpoint!);
        var anonymous = new Plugin.PluginClient(channel);
        (string[] Tokens, string What)[] attempts =
        [
            ([], "A handshake without the session token"),
            ([WrongToken], "A handshake with a wrong session token"),
            ([_token, WrongToken], "A handshake with two session token values (the right one, then a wrong one)"),
            ([WrongToken, _token], "A handshake with two session token values (a wrong one, then the right one)"),
            ([_token, _token], "A handshake with the right session token twice"),
        ];

        foreach (var (tokens, what) in attempts)
        {
            var headers = new Metadata();
            foreach (var token in tokens)
            {
                headers.Add(PluginProtocol.SessionMetadataKey, token);
            }

            await ExpectCodeAsync(async () => await anonymous.HandshakeAsync(HandshakeRequest(PluginProtocol.SupportedVersions), headers, Deadline()).ConfigureAwait(false), StatusCode.Unauthenticated, what).ConfigureAwait(false);
        }

        Assert.NotNull(await process.Client.HandshakeAsync(HandshakeRequest(PluginProtocol.SupportedVersions), deadline: Deadline()).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 stdin：宿主关闭 stdin 后插件必须自行退出（不留孤儿进程）
    /// </summary>
    [Fact]
    public async Task Process_ExitsWhenStdinCloses()
    {
        await using var session = await LaunchAsync().ConfigureAwait(false);

        session.Process.CloseInput();

        Assert.True(await session.Process.WaitForExitAsync(Timeout, TimeProvider.System, CancellationToken.None).ConfigureAwait(false), "The plugin must exit after its stdin is closed.");
    }

    /// <summary>
    /// 投递找不到订阅的错误码
    /// </summary>
    /// <remarks>
    /// 内核的 <c>KERNEL_NOTFOUND_001</c>（<c>invocation.md</c> §8）
    /// </remarks>
    private const string KernelNotFound = "KERNEL_NOTFOUND_001";

    /// <summary>
    /// 启动进程并读握手行
    /// </summary>
    private async Task<PluginProcess> StartProcessAsync()
    {
        var manifest = ReadManifest();
        var executable = OutOfProcessPluginHost.ResolveExecutable(PluginDirectory, manifest.Entry ?? string.Empty)
            ?? throw new InvalidOperationException($"The plugin executable '{manifest.Entry}' was not found in '{PluginDirectory}'.");
        var launch = new PluginLaunch(manifest.Id, executable, Path.GetFullPath(PluginDirectory), _token, _hostEndpoints.Value, manifest.Id + "#conformance", 1, "en-US", Timeout);
        var launched = await PluginProcess.LaunchAsync(launch, TimeProvider.System, NullLogger.Instance, CancellationToken.None).ConfigureAwait(false);
        return launched.ValueOrThrow();
    }

    /// <summary>
    /// 启动、握手并核对清单
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
            return new PluginSession(process, manifest, response, validated.ValueOrThrow());
        }
        catch
        {
            await process.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Configure 与 Start，都必须成功
    /// </summary>
    private async Task StartAsync(PluginSession session)
    {
        await ConfigureAsync(session.Client, session.Manifest).ConfigureAwait(false);
        var started = await session.Client.StartAsync(new StartRequest(), deadline: Deadline()).ConfigureAwait(false);
        Assert.True(started.Status?.Ok, $"Start must succeed, got {Describe(started.Status)}.");
    }

    /// <summary>
    /// 以 <see cref="ConfigurationJson"/> 执行 Configure，必须成功
    /// </summary>
    private async Task ConfigureAsync(Plugin.PluginClient client, Manifest manifest)
    {
        var configured = await client.ConfigureAsync(ConfigureRequest(manifest, ConfigurationJson), deadline: Deadline()).ConfigureAwait(false);
        Assert.True(configured.Status?.Ok, $"Configure must succeed, got {Describe(configured.Status)}.");
    }

    /// <summary>
    /// 组装 Configure 请求：配置节、清单声明的权限与可写的工作目录
    /// </summary>
    private static ConfigureRequest ConfigureRequest(Manifest manifest, string configuration)
    {
        var workDirectory = Path.Combine(Path.GetTempPath(), "xihan-plugins-conformance", manifest.Id);
        Directory.CreateDirectory(workDirectory);
        var request = new ConfigureRequest { Configuration = configuration, WorkDirectory = workDirectory };
        request.GrantedPermissions.AddRange(manifest.Permissions);
        return request;
    }

    /// <summary>
    /// 求一次 Health，核对状态是 <paramref name="states"/> 之一
    /// </summary>
    /// <remarks>
    /// <paramref name="noItems"/> 为真时另核对没有健康项：Start 之前报本地状态（<c>lifecycle.md</c> §7.4）
    /// </remarks>
    private async Task ExpectHealthAsync(Plugin.PluginClient client, string when, bool noItems, params ProtoLifecycleState[] states)
    {
        var health = await client.HealthAsync(new HealthRequest(), deadline: Deadline()).ConfigureAwait(false);
        Assert.True(states.Contains(health.State), $"Health {when} reported {health.State}, want {string.Join(" or ", states)}.");
        Assert.True(!noItems || health.Items.Count == 0, $"Health {when} must report no health items, got {health.Items.Count}.");
    }

    /// <summary>
    /// 以 <paramref name="grace"/> 秒的宽限调 Stop，核对应答 ok、进程在 5 秒内以退出码 0 退出
    /// </summary>
    private async Task StopAndExitAsync(PluginSession session, uint grace, string when)
    {
        var stopped = await session.Client.StopAsync(new StopRequest { GraceSeconds = grace, Reason = "unload" }, deadline: Deadline()).ConfigureAwait(false);
        Assert.True(stopped.Status?.Ok, $"Stop {when} must succeed, got {Describe(stopped.Status)}.");
        Assert.True(await session.Process.WaitForExitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None).ConfigureAwait(false), $"The plugin must exit within 5 seconds after Stop {when}.");
        var code = await session.Process.Exited.ConfigureAwait(false);
        Assert.True(code == 0, $"The plugin must exit with code 0 after Stop {when}, got {code}.");
    }

    /// <summary>
    /// 打开一个 InvokeStream、发出 <paramref name="first"/>，核对流以 <paramref name="want"/> 结束
    /// </summary>
    /// <remarks>
    /// <paramref name="first"/> 为 <see langword="null"/> 表示不发帧就关闭发送方向；发出第一帧后不关闭发送方向，插件读到第一帧就要作出判定
    /// </remarks>
    private async Task ExpectStreamAsync(Plugin.PluginClient client, StreamFrame? first, StatusCode want, string what)
    {
        using var call = client.InvokeStream(deadline: Deadline());
        try
        {
            if (first is null)
            {
                await call.RequestStream.CompleteAsync().ConfigureAwait(false);
            }
            else
            {
                await call.RequestStream.WriteAsync(first).ConfigureAwait(false);
            }
        }
        catch (RpcException)
        {
            // 服务端已经结束了流：结果在读取方向
        }
        catch (InvalidOperationException)
        {
            // 同上：写入方向随调用结束而关闭
        }

        await ExpectCodeAsync(async () =>
        {
            if (await call.ResponseStream.MoveNext(CancellationToken.None).ConfigureAwait(false))
            {
                Assert.Fail($"{what} must end with {want}, got a frame {call.ResponseStream.Current}.");
            }
        }, want, what).ConfigureAwait(false);
    }

    /// <summary>
    /// 读目录里的清单
    /// </summary>
    private Manifest ReadManifest()
        => ManifestReader.ReadFile(Path.Combine(PluginDirectory, ManifestReader.FileName)).ValueOrThrow();

    /// <summary>
    /// 以给定版本列表组装握手请求
    /// </summary>
    /// <remarks>
    /// <c>host_endpoint</c> 是端点列表的第一项，只供诊断（<c>handshake.md</c> §3）
    /// </remarks>
    private HandshakeRequest HandshakeRequest(IEnumerable<uint> versions)
    {
        var first = _hostEndpoints.Value[0];
        var request = new HandshakeRequest
        {
            Primitives = 1,
            InstanceId = "conformance",
            Host = new HostInfo { FrameworkVersion = FrameworkInfo.Version, Sdk = "dotnet", SdkVersion = Environment.Version.ToString(), Application = "conformance" },
            HostEndpoint = new Endpoint { Network = first.Network, Address = first.Address },
            DefaultLocale = "en-US",
        };
        request.ProtocolVersions.AddRange(versions);
        return request;
    }

    /// <summary>
    /// 组装一次调用
    /// </summary>
    /// <remarks>
    /// 载荷取 <see cref="SampleRequests"/>（没有时为 <c>{}</c>），带 traceparent 与已登记的 Baggage
    /// </remarks>
    private InvokeRequest InvokeRequest(string operation)
    {
        var payload = SampleRequests.TryGetValue(operation, out var sample) ? sample : "{}";
        return new InvokeRequest
        {
            Operation = operation,
            Payload = ByteString.CopyFromUtf8(payload),
            Metadata = new InvocationMetadata { TraceParent = TraceParent, Baggage = { [BaggageKeys.Correlation] = "conformance" } },
        };
    }

    /// <summary>
    /// 组装一次投递
    /// </summary>
    /// <remarks>
    /// 信封带随机 ID 与空对象的数据，<paramref name="mode"/> 是这次投递的方式
    /// </remarks>
    private static HandleEventRequest EventRequest(string topic, string messageType, string mode) => new()
    {
        Envelope = new Envelope
        {
            Topic = topic,
            Id = Guid.NewGuid().ToString("N"),
            Source = "urn:xihan:conformance",
            Type = messageType,
            SpecVersion = "1.0",
            DataContentType = "application/json",
            Data = ByteString.CopyFromUtf8("{}"),
        },
        Metadata = new InvocationMetadata { TraceParent = TraceParent },
        Attempt = 1,
        Mode = mode,
    };

    /// <summary>
    /// 投递给订阅的消息类型
    /// </summary>
    /// <remarks>
    /// 订阅没有限定类型时用一个任意的类型
    /// </remarks>
    private static string TypeOf(Subscription subscription) => string.IsNullOrEmpty(subscription.MessageType) ? EventType : subscription.MessageType;

    /// <summary>
    /// 订阅声明的方式换成投递时的方式
    /// </summary>
    /// <remarks>
    /// 宿主总是填写，声明为空即竞争消费
    /// </remarks>
    private static string DeliveryMode(string declared) => string.IsNullOrEmpty(declared) ? PluginSubscriptionModes.Competing : declared;

    /// <summary>
    /// 核对一次调用以 gRPC 状态 <paramref name="want"/> 结束
    /// </summary>
    private static async Task ExpectCodeAsync(Func<Task> call, StatusCode want, string what)
    {
        try
        {
            await call().ConfigureAwait(false);
        }
        catch (RpcException exception)
        {
            Assert.True(exception.StatusCode == want, $"{what} must be refused with {want}, got {exception.StatusCode}: {exception.Status.Detail}");
            return;
        }

        Assert.Fail($"{what} must be refused with {want}, got gRPC status OK.");
    }

    /// <summary>
    /// 核对 Status 是带消息键的业务失败
    /// </summary>
    /// <remarks>
    /// 码与键相等、<c>text</c> 为空（由宿主按目录翻译，<c>invocation.md</c> §3）、<c>details</c> 按 JSON 值等于 <paramref name="details"/>
    /// </remarks>
    private static void ExpectFailure(ProtoStatus? status, string code, string key, JsonObject details, string what)
    {
        Assert.True(status is { Ok: false, Error: not null }, $"{what} must fail with {code} and message key '{key}', got {Describe(status)}.");
        var failure = status!.Error!;
        Assert.True(failure.Code == code && failure.MessageKey == key, $"{what} must fail with {code} and message key '{key}', got {failure.Code} and '{failure.MessageKey}'.");
        Assert.True(failure.Text.Length == 0, $"{what}: an error with a message key must leave text empty, got '{failure.Text}'.");
        Assert.True(SameDetails(failure.Details, details), $"{what}: details must equal {details.ToJsonString()} as a JSON value, got '{failure.Details}'.");
    }

    /// <summary>
    /// 按 JSON 值比较 <c>details</c> 文本与期望的对象
    /// </summary>
    /// <remarks>
    /// 成员顺序与空白可以不同，重复成员与多余成员都不相等
    /// </remarks>
    private static bool SameDetails(string text, JsonObject expected)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { AllowDuplicateProperties = false });
            return JsonNode.DeepEquals(JsonNode.Parse(document.RootElement.GetRawText()), expected);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 期望的 <c>details</c>：成员都是字符串
    /// </summary>
    private static JsonObject Details(params (string Name, string Value)[] members)
    {
        var details = new JsonObject();
        foreach (var (name, value) in members)
        {
            details[name] = value;
        }

        return details;
    }

    /// <summary>
    /// 把一个 Status 描述成可读文本：错误码、消息键、文案与细节
    /// </summary>
    private static string Describe(ProtoStatus? status) => status switch
    {
        null => "no status",
        { Ok: true } => "ok",
        { Error: null } => "failure without error",
        { Error: { } error } => $"{error.Code} (key '{error.MessageKey}', text '{error.Text}', details '{error.Details}')",
    };

    /// <summary>
    /// 交给插件的回调端点列表
    /// </summary>
    /// <remarks>
    /// 与 .NET 宿主一样两项、回环 TCP 在最后：Windows 上是命名管道加回环 TCP，其他系统是 Unix 域套接字加回环 TCP（临时目录的路径写不进列表时换成第二个回环 TCP）。套件不提供 Host 服务，两项都没人监听，只用来让每个用例都考到插件解析列表
    /// </remarks>
    private static IReadOnlyList<PluginEndpoint> CreateHostEndpoints()
    {
        var ports = UnusedPorts(2);
        var suffix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var loopback = new PluginEndpoint(PluginEndpoint.Tcp, $"127.0.0.1:{ports[0]}");
        if (OperatingSystem.IsWindows())
        {
            return [new PluginEndpoint(PluginEndpoint.NamedPipe, @"\\.\pipe\xihan-conformance-" + suffix), loopback];
        }

        var socket = Path.Combine(Path.GetTempPath(), "xihan-conformance-" + suffix, "host.sock");
        return PluginEndpoint.TryParseList($"{PluginEndpoint.Unix} {socket}", out var unix)
            ? [unix[0], loopback]
            : [new PluginEndpoint(PluginEndpoint.Tcp, $"127.0.0.1:{ports[1]}"), loopback];
    }

    /// <summary>
    /// 取 <paramref name="count"/> 个此刻没人监听的回环端口
    /// </summary>
    /// <remarks>
    /// 同时监听端口 0 让系统分配互不相同的端口，再全部关掉
    /// </remarks>
    private static int[] UnusedPorts(int count)
    {
        var listeners = Enumerable.Range(0, count).Select(static _ => new TcpListener(IPAddress.Loopback, 0)).ToList();
        try
        {
            foreach (var listener in listeners)
            {
                listener.Start();
            }

            return [.. listeners.Select(static l => ((IPEndPoint)l.LocalEndpoint).Port)];
        }
        finally
        {
            foreach (var listener in listeners)
            {
                listener.Dispose();
            }
        }
    }

    /// <summary>
    /// base64url，不带填充
    /// </summary>
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// 本次调用的截止时间
    /// </summary>
    private DateTime Deadline() => DateTime.UtcNow + Timeout;
}
