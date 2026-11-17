// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Google.Protobuf;
using Grpc.Core;
using XiHan.Framework.Plugins.Protocol.V1;
using Error = XiHan.Framework.Plugins.Protocol.V1.Error;
using Status = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace EchoPlugin;

/// <summary>
/// <c>xihan.plugin.v1.Plugin</c> 的实现
/// </summary>
/// <remarks>
/// 五个操作（回显、经宿主回调中继、经宿主发布、故意崩溃、列出收到的事件）、两个订阅与一个健康项。插件一侧的次序、就绪、健康、停止与投递按 <c>spec/plugin/lifecycle.md</c> §7 与 <c>invocation.md</c> §1、§4
/// </remarks>
internal sealed partial class EchoPluginService(PluginState state, HostClient host, IHostApplicationLifetime lifetime, ILogger<EchoPluginService> logger) : Plugin.PluginBase
{
    /// <summary>
    /// 清单 ID
    /// </summary>
    public const string PluginId = "xihan.samples.echo";

    /// <summary>
    /// 健康项名
    /// </summary>
    private const string HealthItem = "echo";

    /// <summary>
    /// 竞争消费的投递方式
    /// </summary>
    private const string Competing = "competing";

    /// <summary>
    /// 广播的投递方式
    /// </summary>
    private const string Broadcast = "broadcast";

    /// <summary>
    /// 可服务期间重新求健康的间隔
    /// </summary>
    /// <remarks>
    /// 宿主 <c>HealthInterval</c> 的缺省值（lifecycle.md §7.4）
    /// </remarks>
    private static readonly TimeSpan _healthInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// <c>grace_seconds</c> 为 0 时的宽限
    /// </summary>
    private static readonly TimeSpan _defaultGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 握手声明的操作
    /// </summary>
    private static readonly string[] _operations = ["echoes.create", "echoes.relay", "echoes.publish", "echoes.crash", "events.list"];

    /// <summary>
    /// 握手声明的订阅，按声明顺序
    /// </summary>
    /// <remarks>
    /// 同一主题的另一个类型用广播：echo.cleared 以两种方式各到一次，插件靠投递带来的 mode 区分（ADR-0226）
    /// </remarks>
    private static readonly (string Topic, string? MessageType, string Mode)[] _subscriptions = [("echo.events", null, Competing), ("echo.events", "echo.cleared", Broadcast)];

    /// <inheritdoc />
    public override Task<HandshakeResponse> Handshake(HandshakeRequest request, ServerCallContext context)
    {
        // 选宿主列表里最高的自己也支持的版本（这里只支持 1）；没有共同版本时状态不变
        if (!request.ProtocolVersions.Contains(1u))
        {
            throw new RpcException(new Grpc.Core.Status(StatusCode.FailedPrecondition, "No common protocol version."));
        }

        if (!state.TryAdvance(LifecycleState.Declared, LifecycleState.Resolved))
        {
            throw OutOfOrder("Handshake");
        }

        var response = new HandshakeResponse
        {
            ProtocolVersion = 1,
            // 插件写清单的 primitives（handshake.md §3 第 2 条）
            Primitives = 1,
            Manifest = state.ManifestJson,
            Sdk = new SdkInfo { Name = "EchoPlugin", Version = "5.0.0-preview.1", Language = "csharp" },
        };
        response.Operations.Add(new OperationDeclaration
        {
            Id = "echoes.create",
            Stability = "experimental",
            Expose = { "http", "mcp", "commandline" },
            RequestSchema = """{"type":"object","properties":{"text":{"type":"string"},"delayMs":{"type":"integer"}},"required":["text"]}""",
            ResponseSchema = """{"type":"object","properties":{"text":{"type":"string"},"plugin":{"type":"string"},"greeting":{"type":"string"},"tenant":{"type":"string"}}}""",
        });
        response.Operations.Add(new OperationDeclaration { Id = "echoes.relay", Stability = "experimental", Expose = { "http" } });
        response.Operations.Add(new OperationDeclaration { Id = "echoes.publish", Stability = "experimental" });
        response.Operations.Add(new OperationDeclaration { Id = "echoes.crash", Stability = "internal" });
        response.Operations.Add(new OperationDeclaration { Id = "events.list", Stability = "experimental", Expose = { "http" } });
        // 订阅的方式总是写出 competing 或 broadcast，不写空（handshake.md §3 第 5 条）
        foreach (var (topic, messageType, mode) in _subscriptions)
        {
            response.Subscriptions.Add(new Subscription { Topic = topic, MessageType = messageType ?? string.Empty, Mode = mode });
        }

        var catalog = new MessageCatalog { Name = "echo" };
        catalog.Locales.Add(new LocaleTemplates { Locale = "en-US", Templates = { ["echo.text.empty"] = "The text must not be empty." } });
        catalog.Locales.Add(new LocaleTemplates { Locale = "zh-CN", Templates = { ["echo.text.empty"] = "文本不能为空。" } });
        response.Catalogs.Add(catalog);
        LogHandshake(logger, request.Host?.Sdk, request.Host?.FrameworkVersion, request.InstanceId);
        return Task.FromResult(response);
    }

    /// <inheritdoc />
    public override Task<ConfigureResponse> Configure(ConfigureRequest request, ServerCallContext context)
    {
        // 配置节不是 JSON 对象：不采纳，插件进入 Failed（lifecycle.md §7.2）
        var configuration = ConfigurationText.Parse(request.Configuration);
        if (!state.TryConfigure(configuration, request.GrantedPermissions))
        {
            throw OutOfOrder("Configure");
        }

        return Task.FromResult(new ConfigureResponse
        {
            Status = configuration is null ? Failure("PLUGINS_UNEXPECTED_001", "plugins.process.failed", new JsonObject { ["id"] = PluginId, ["phase"] = "configure", ["reason"] = "the configuration is not a JSON object" }) : Ok(),
        });
    }

    /// <inheritdoc />
    public override Task<StartResponse> Start(StartRequest request, ServerCallContext context)
    {
        // 启动回调成功后立即求一次健康，按结果进入 Ready / Degraded / Failed，然后才应答（lifecycle.md §7.3）
        if (!state.TryStart(StartHealth))
        {
            throw OutOfOrder("Start");
        }

        return Task.FromResult(new StartResponse { Status = Ok() });
    }

    /// <inheritdoc />
    public override async Task<StopResponse> Stop(StopRequest request, ServerCallContext context)
    {
        if (!state.TryBeginStop(out var started))
        {
            throw OutOfOrder("Stop");
        }

        // 启动回调没有成功过：不调停止逻辑，应答后以退出码 0 退出（lifecycle.md §7.5）
        if (started)
        {
            // 新到的调用已经按 Draining 拒绝；等在途调用，最长到宽限期，到时只经令牌通知剩下的
            await state.DrainAsync(request.GraceSeconds == 0 ? _defaultGrace : TimeSpan.FromSeconds(request.GraceSeconds)).ConfigureAwait(false);
            if (state.ClaimStop())
            {
                LogStop(logger, request.Reason, request.GraceSeconds);
            }

            state.CompleteStop();
        }

        // 先应答，再退出进程
        _ = Task.Run(async () =>
        {
            await Task.Delay(100).ConfigureAwait(false);
            lifetime.StopApplication();
        });
        return new StopResponse { Status = Ok() };
    }

    /// <inheritdoc />
    public override Task<HealthResponse> Health(HealthRequest request, ServerCallContext context) => Task.FromResult(CurrentHealth());

    /// <inheritdoc />
    public override async Task WatchHealth(HealthRequest request, IServerStreamWriter<HealthResponse> responseStream, ServerCallContext context)
    {
        // 先发当前状态，之后状态变化时发一条；可服务期间每 10 秒重新求一次健康，结果变了才发
        var gate = new SemaphoreSlim(0);
        using var subscription = state.Watch(() =>
        {
            gate.Release();
            return Task.CompletedTask;
        });
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, lifetime.ApplicationStopping);
        var last = CurrentHealth();
        await responseStream.WriteAsync(last, stopping.Token).ConfigureAwait(false);
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                await gate.WaitAsync(_healthInterval, stopping.Token).ConfigureAwait(false);
                var current = CurrentHealth();
                if (!current.Equals(last))
                {
                    last = current;
                    await responseStream.WriteAsync(current, stopping.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 宿主取消或进程停止：正常结束流
        }
    }

    /// <inheritdoc />
    public override async Task<InvokeResponse> Invoke(InvokeRequest request, ServerCallContext context)
    {
        // 核对顺序：会话令牌（拦截器）→ 操作是否声明 → 是否可服务（invocation.md §1）
        if (!_operations.Contains(request.Operation, StringComparer.Ordinal))
        {
            throw new RpcException(new Grpc.Core.Status(StatusCode.Unimplemented, $"Operation '{request.Operation}' is not served by this plugin."));
        }

        if (!state.TryEnter(out var current))
        {
            return new InvokeResponse { Status = Unavailable(current) };
        }

        try
        {
            // 在途调用随停止与收尾收到取消，与 gRPC 自己的取消合在一起
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, state.Stopping);
            return await InvokeCoreAsync(request, cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            state.Exit();
        }
    }

    /// <inheritdoc />
    public override async Task InvokeStream(IAsyncStreamReader<StreamFrame> requestStream, IServerStreamWriter<StreamFrame> responseStream, ServerCallContext context)
    {
        // 流式操作等流式契约的 RFC（invocation.md §1）：只看第一帧，不论操作是否声明、插件是否就绪
        if (!await requestStream.MoveNext(context.CancellationToken).ConfigureAwait(false) || requestStream.Current.FrameCase != StreamFrame.FrameOneofCase.Start)
        {
            throw new RpcException(new Grpc.Core.Status(StatusCode.InvalidArgument, "The first frame of InvokeStream must be 'start'."));
        }

        throw new RpcException(new Grpc.Core.Status(StatusCode.Unimplemented, "This plugin serves no streaming operations."));
    }

    /// <inheritdoc />
    public override Task<HandleEventResponse> HandleEvent(HandleEventRequest request, ServerCallContext context)
    {
        // 核对顺序：会话令牌（拦截器）→ 信封与方式 → 订阅 → 是否可服务（invocation.md §4）
        if (request.Envelope is not { } envelope || request.Mode is not ("" or Competing or Broadcast))
        {
            throw new RpcException(new Grpc.Core.Status(StatusCode.InvalidArgument, "HandleEvent needs an envelope and a mode of 'competing' or 'broadcast'."));
        }

        var mode = request.Mode.Length == 0 ? Competing : request.Mode;
        var matched = _subscriptions.Where(s => s.Topic == envelope.Topic && s.Mode == mode && (s.MessageType is null || s.MessageType == envelope.Type)).ToList();
        if (matched.Count == 0)
        {
            return Task.FromResult(new HandleEventResponse { Status = Failure("KERNEL_NOTFOUND_001", "plugins.event.unsubscribed", new JsonObject { ["id"] = PluginId, ["topic"] = envelope.Topic, ["type"] = envelope.Type, ["mode"] = mode }) });
        }

        if (!state.TryEnter(out var current))
        {
            return Task.FromResult(new HandleEventResponse { Status = Unavailable(current) });
        }

        try
        {
            // 匹配的订阅按声明顺序依次处理，第一个失败即为结果（与 RFC-0009 的派发器相同）；这里的处理器只记下投递
            foreach (var _ in matched)
            {
                state.Record(envelope.Id, mode);
                LogEvent(logger, envelope.Id, envelope.Type, envelope.Topic, mode, request.Attempt);
            }

            return Task.FromResult(new HandleEventResponse { Status = Ok() });
        }
        finally
        {
            state.Exit();
        }
    }

    /// <summary>
    /// 执行一个声明的操作
    /// </summary>
    private async Task<InvokeResponse> InvokeCoreAsync(InvokeRequest request, CancellationToken cancellationToken)
    {
        var payload = JsonNode.Parse(request.Payload.Length == 0 ? "{}" : request.Payload.ToStringUtf8()) as JsonObject ?? [];
        switch (request.Operation)
        {
            case "echoes.create":
                var text = payload["text"]?.GetValue<string>();
                var delay = payload["delayMs"]?.GetValue<int>() ?? 0;
                if (delay > 0)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    return new InvokeResponse { Status = Failure("ECHO_VALIDATION_001", "echo.text.empty", null) };
                }

                var echoed = new JsonObject { ["text"] = text, ["plugin"] = PluginId };
                if (state.Greeting is { } greeting)
                {
                    echoed["greeting"] = greeting;
                }

                if (request.Metadata?.Baggage.TryGetValue("xihan.tenant", out var tenant) == true)
                {
                    echoed["tenant"] = tenant;
                }

                return Success(echoed);
            case "echoes.relay":
                return await RelayAsync(payload, request.Metadata, cancellationToken).ConfigureAwait(false);
            case "echoes.publish":
                return await PublishAsync(payload, request.Metadata, cancellationToken).ConfigureAwait(false);
            case "echoes.crash":
                LogCrash(logger);
                await Console.Error.FlushAsync(cancellationToken).ConfigureAwait(false);
                Environment.Exit(3);
                return new InvokeResponse();
            case "events.list":
                return Success(new JsonObject
                {
                    ["ids"] = new JsonArray(state.Events.Select(static id => (JsonNode)id).ToArray()),
                    ["deliveries"] = new JsonArray(state.Deliveries.Select(static delivery => (JsonNode)delivery).ToArray()),
                });
            default:
                // 调用方已经按声明的操作过滤过
                throw new UnreachableException($"Operation '{request.Operation}' is declared but not implemented.");
        }
    }

    /// <summary>
    /// 经宿主回调调用 <c>payload.operation</c>，把宿主的结果原样返回
    /// </summary>
    /// <remarks>
    /// 转发收到的元数据，<c>payload.tenant</c> 给了时改写其中的租户（测试用：宿主拒绝插件自报的租户）；回调被拒时把尾部元数据里的错误码作为失败返回
    /// </remarks>
    private async Task<InvokeResponse> RelayAsync(JsonObject payload, InvocationMetadata? metadata, CancellationToken cancellationToken)
    {
        var operation = payload["operation"]?.GetValue<string>() ?? string.Empty;
        var inner = payload["payload"]?.ToJsonString() ?? "{}";
        try
        {
            return await host.InvokeAsync(operation, ByteString.CopyFromUtf8(inner), Forwarded(payload, metadata), cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException exception)
        {
            return new InvokeResponse { Status = Rejected(exception) };
        }
    }

    /// <summary>
    /// 经宿主发布 <c>payload.envelopes</c>，返回逐项结果
    /// </summary>
    /// <remarks>
    /// 每项给 <c>topic</c>、<c>id</c>、<c>type</c>，可选 <c>extensions</c>；转发收到的元数据，<c>payload.tenant</c> 给了时改写其中的租户（测试用）。没有信封时不回调。结果的每一项是 <c>{"ok":true}</c> 或 <c>{"ok":false,"code":…,"messageKey":…}</c>
    /// </remarks>
    private async Task<InvokeResponse> PublishAsync(JsonObject payload, InvocationMetadata? metadata, CancellationToken cancellationToken)
    {
        var envelopes = (payload["envelopes"] as JsonArray ?? []).OfType<JsonObject>().Select(static item =>
        {
            var envelope = new Envelope
            {
                Topic = item["topic"]?.GetValue<string>() ?? string.Empty,
                Id = item["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                Source = "urn:xihan:samples:echo",
                Type = item["type"]?.GetValue<string>() ?? "echo.published",
                SpecVersion = "1.0",
                DataContentType = "application/json",
                Data = ByteString.CopyFromUtf8("{}"),
            };
            foreach (var (name, value) in item["extensions"] as JsonObject ?? [])
            {
                envelope.Extensions[name] = value?.GetValue<string>() ?? string.Empty;
            }

            return envelope;
        }).ToList();
        if (envelopes.Count == 0)
        {
            return Success(new JsonObject { ["results"] = new JsonArray() });
        }

        PublishResponse response;
        try
        {
            response = await host.PublishAsync(envelopes, Forwarded(payload, metadata), cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException exception)
        {
            return new InvokeResponse { Status = Rejected(exception) };
        }

        var results = new JsonArray();
        foreach (var result in response.Results)
        {
            results.Add(result.Ok ? new JsonObject { ["ok"] = true } : new JsonObject { ["ok"] = false, ["code"] = result.Error?.Code, ["messageKey"] = result.Error?.MessageKey });
        }

        return Success(new JsonObject { ["results"] = results });
    }

    /// <summary>
    /// 回调要转发的元数据
    /// </summary>
    /// <remarks>
    /// 原样转发收到的元数据；<c>payload.tenant</c> 给了时改写其中的租户，宿主只接受它随调用交给插件的那个租户（ADR-0195）
    /// </remarks>
    private static InvocationMetadata? Forwarded(JsonObject payload, InvocationMetadata? metadata)
    {
        if (payload["tenant"]?.GetValue<string>() is not { } tenant)
        {
            return metadata;
        }

        var rewritten = metadata?.Clone() ?? new InvocationMetadata();
        rewritten.Baggage["xihan.tenant"] = tenant;
        return rewritten;
    }

    /// <summary>
    /// 回调以 gRPC 状态失败时的结果：尾部元数据里的错误码，文案取状态的说明
    /// </summary>
    private static Status Rejected(RpcException exception)
        => new() { Ok = false, Error = new Error { Code = exception.Trailers.GetValue("xihan-error-code") ?? "ECHO_UNAVAILABLE_001", Text = exception.Status.Detail } };

    /// <summary>
    /// 当前健康
    /// </summary>
    /// <remarks>
    /// Start 之前报本地状态、没有健康项；可服务时求一次健康（<c>echo</c> 项始终健康）；其余状态照实报、不再求健康（lifecycle.md §7.4）
    /// </remarks>
    private HealthResponse CurrentHealth()
    {
        var current = state.State;
        if (current is not (LifecycleState.Ready or LifecycleState.Degraded))
        {
            return new HealthResponse { State = current };
        }

        LifecycleState reported;
        try
        {
            reported = EvaluateHealth();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // 健康回调抛出：这一次是 Degraded，带 health 阶段的错误、没有健康项，状态与回调报了 Degraded 一样迁移（lifecycle.md §7.4）
            state.ApplyHealth(LifecycleState.Degraded);
            return new HealthResponse
            {
                State = state.State,
                Error = Failure("PLUGINS_UNEXPECTED_001", "plugins.process.failed", new JsonObject { ["id"] = PluginId, ["phase"] = "health", ["reason"] = exception.GetType().Name }).Error,
            };
        }

        // 报 Ready、Degraded、Failed 的照办，报其他状态的不改变状态
        if (reported is LifecycleState.Ready or LifecycleState.Degraded or LifecycleState.Failed)
        {
            state.ApplyHealth(reported);
        }

        var response = new HealthResponse { State = state.State };
        response.Items.Add(new HealthItem { Name = HealthItem, Status = HealthStatus.Healthy });
        return response;
    }

    /// <summary>
    /// Start 应答之前求的那一次健康
    /// </summary>
    /// <remarks>
    /// 回调报 Ready、Degraded、Failed 的照办，报其他状态的按 Ready，抛出的按 Degraded（lifecycle.md §7.3）
    /// </remarks>
    private static LifecycleState StartHealth()
    {
        try
        {
            var reported = EvaluateHealth();
            return reported is LifecycleState.Ready or LifecycleState.Degraded or LifecycleState.Failed ? reported : LifecycleState.Ready;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return LifecycleState.Degraded;
        }
    }

    /// <summary>
    /// 健康回调：<c>echo</c> 项始终健康，所以是 Ready
    /// </summary>
    private static LifecycleState EvaluateHealth() => LifecycleState.Ready;

    /// <summary>
    /// 次序不对的生命周期调用
    /// </summary>
    /// <remarks>
    /// 以 <c>FailedPrecondition</c> 拒绝、什么都不做、状态不变（lifecycle.md §7.1）
    /// </remarks>
    private RpcException OutOfOrder(string rpc)
        => new(new Grpc.Core.Status(StatusCode.FailedPrecondition, $"'{rpc}' is not accepted in state '{state.State}'."));

    /// <summary>
    /// 不在可服务状态时的失败
    /// </summary>
    private static Status Unavailable(LifecycleState current)
        => Failure("PLUGINS_UNAVAILABLE_001", "plugins.process.unavailable", new JsonObject { ["id"] = PluginId, ["state"] = current.ToString() });

    /// <summary>
    /// 成功状态
    /// </summary>
    private static Status Ok() => new() { Ok = true };

    /// <summary>
    /// 成功应答，载荷为 JSON 对象
    /// </summary>
    private static InvokeResponse Success(JsonObject payload)
        => new() { Status = Ok(), Payload = ByteString.CopyFrom(Encoding.UTF8.GetBytes(payload.ToJsonString())) };

    /// <summary>
    /// 业务失败：错误码 + 消息键 + 参数
    /// </summary>
    /// <remarks>
    /// 带消息键的错误 <c>text</c> 留空，由宿主按目录翻译成调用方的语言（invocation.md §3）
    /// </remarks>
    private static Status Failure(string code, string messageKey, JsonObject? details)
        => new() { Ok = false, Error = new Error { Code = code, MessageKey = messageKey, Details = details?.ToJsonString() ?? string.Empty } };

    /// <summary>
    /// 记录握手
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Handshake from host {Sdk} {Version} (instance {Instance}).")]
    private static partial void LogHandshake(ILogger logger, string? sdk, string? version, string instance);

    /// <summary>
    /// 记录停止
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Stop requested ({Reason}, grace {Grace}s).")]
    private static partial void LogStop(ILogger logger, string reason, uint grace);

    /// <summary>
    /// 记录故意崩溃
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Crashing on request.")]
    private static partial void LogCrash(ILogger logger);

    /// <summary>
    /// 记录收到的事件
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Received event {Id} of type {Type} on {Topic} in mode '{Mode}' (attempt {Attempt}).")]
    private static partial void LogEvent(ILogger logger, string id, string type, string topic, string mode, uint attempt);
}
