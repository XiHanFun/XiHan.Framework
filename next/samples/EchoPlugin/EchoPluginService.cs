// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json.Nodes;
using Google.Protobuf;
using Grpc.Core;
using XiHan.Framework.Plugins.Protocol.V1;
using Error = XiHan.Framework.Plugins.Protocol.V1.Error;
using Status = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace EchoPlugin;

/// <summary>
/// <c>xihan.plugin.v1.Plugin</c> 的实现。
/// </summary>
/// <remarks>
/// 四个操作（回显、经宿主回调中继、故意崩溃、列出收到的事件）、一个订阅与一个健康项。
/// </remarks>
internal sealed partial class EchoPluginService(PluginState state, HostClient host, IHostApplicationLifetime lifetime, ILogger<EchoPluginService> logger) : Plugin.PluginBase
{
    /// <summary>
    /// 清单 ID。
    /// </summary>
    private const string PluginId = "xihan.samples.echo";

    /// <inheritdoc />
    public override Task<HandshakeResponse> Handshake(HandshakeRequest request, ServerCallContext context)
    {
        // 选宿主列表里最高的自己也支持的版本（这里只支持 1）
        if (!request.ProtocolVersions.Contains(1u))
        {
            throw new RpcException(new Grpc.Core.Status(StatusCode.FailedPrecondition, "No common protocol version."));
        }

        var response = new HandshakeResponse
        {
            ProtocolVersion = 1,
            Primitives = request.Primitives,
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
        response.Operations.Add(new OperationDeclaration { Id = "echoes.crash", Stability = "internal" });
        response.Operations.Add(new OperationDeclaration { Id = "events.list", Stability = "experimental", Expose = { "http" } });
        response.Subscriptions.Add(new Subscription { Topic = "echo.events" });
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
        state.Configure(request.Configuration, request.GrantedPermissions);
        return Task.FromResult(new ConfigureResponse { Status = Ok() });
    }

    /// <inheritdoc />
    public override async Task<StartResponse> Start(StartRequest request, ServerCallContext context)
    {
        await state.TransitionAsync((int)LifecycleState.Ready).ConfigureAwait(false);
        return new StartResponse { Status = Ok() };
    }

    /// <inheritdoc />
    public override async Task<StopResponse> Stop(StopRequest request, ServerCallContext context)
    {
        LogStop(logger, request.Reason, request.GraceSeconds);
        await state.TransitionAsync((int)LifecycleState.Draining).ConfigureAwait(false);
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
        // 先发当前状态，之后每次状态变化发一条，直到宿主取消或进程停止
        var gate = new SemaphoreSlim(0);
        using var subscription = state.Watch(() =>
        {
            gate.Release();
            return Task.CompletedTask;
        });
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, lifetime.ApplicationStopping);
        await responseStream.WriteAsync(CurrentHealth(), stopping.Token).ConfigureAwait(false);
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                await gate.WaitAsync(stopping.Token).ConfigureAwait(false);
                await responseStream.WriteAsync(CurrentHealth(), stopping.Token).ConfigureAwait(false);
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
        var payload = JsonNode.Parse(request.Payload.Length == 0 ? "{}" : request.Payload.ToStringUtf8()) as JsonObject ?? [];
        switch (request.Operation)
        {
            case "echoes.create":
                var text = payload["text"]?.GetValue<string>();
                var delay = payload["delayMs"]?.GetValue<int>() ?? 0;
                if (delay > 0)
                {
                    await Task.Delay(delay, context.CancellationToken).ConfigureAwait(false);
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    return Failure("ECHO_VALIDATION_001", "echo.text.empty");
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
                return await RelayAsync(payload, request.Metadata, context.CancellationToken).ConfigureAwait(false);
            case "echoes.crash":
                LogCrash(logger);
                await Console.Error.FlushAsync().ConfigureAwait(false);
                Environment.Exit(3);
                return new InvokeResponse();
            case "events.list":
                return Success(new JsonObject { ["ids"] = new JsonArray(state.Events.Select(static id => (JsonNode)id).ToArray()) });
            default:
                throw new RpcException(new Grpc.Core.Status(StatusCode.Unimplemented, $"Operation '{request.Operation}' is not served by this plugin."));
        }
    }

    /// <inheritdoc />
    public override Task<HandleEventResponse> HandleEvent(HandleEventRequest request, ServerCallContext context)
    {
        state.Record(request.Envelope.Id);
        LogEvent(logger, request.Envelope.Id, request.Envelope.Type, request.Envelope.Topic, request.Attempt);
        return Task.FromResult(new HandleEventResponse { Status = Ok() });
    }

    /// <summary>
    /// 经宿主回调调用 <c>payload.operation</c>，把宿主的结果原样返回。
    /// </summary>
    /// <remarks>
    /// 回调被拒时把尾部元数据里的错误码作为失败返回。
    /// </remarks>
    private async Task<InvokeResponse> RelayAsync(JsonObject payload, InvocationMetadata? metadata, CancellationToken cancellationToken)
    {
        var operation = payload["operation"]?.GetValue<string>() ?? string.Empty;
        var inner = payload["payload"]?.ToJsonString() ?? "{}";
        try
        {
            var relayed = await host.InvokeAsync(operation, ByteString.CopyFromUtf8(inner), metadata, cancellationToken).ConfigureAwait(false);
            return relayed;
        }
        catch (RpcException exception)
        {
            var code = exception.Trailers.GetValue("xihan-error-code") ?? "ECHO_UNAVAILABLE_001";
            return new InvokeResponse { Status = new Status { Ok = false, Error = new Error { Code = code, Text = exception.Status.Detail } } };
        }
    }

    /// <summary>
    /// 当前健康：自报状态与一个始终健康的 <c>echo</c> 项。
    /// </summary>
    private HealthResponse CurrentHealth() => new()
    {
        State = (LifecycleState)state.State,
        Items = { new HealthItem { Name = "echo", Status = HealthStatus.Healthy } },
    };

    /// <summary>
    /// 成功状态。
    /// </summary>
    private static Status Ok() => new() { Ok = true };

    /// <summary>
    /// 成功应答，载荷为 JSON 对象。
    /// </summary>
    private static InvokeResponse Success(JsonObject payload)
        => new() { Status = Ok(), Payload = ByteString.CopyFrom(Encoding.UTF8.GetBytes(payload.ToJsonString())) };

    /// <summary>
    /// 业务失败：错误码 + 消息键（文案在握手时交给宿主的目录里）。
    /// </summary>
    private static InvokeResponse Failure(string code, string messageKey)
        => new() { Status = new Status { Ok = false, Error = new Error { Code = code, MessageKey = messageKey } } };

    [LoggerMessage(Level = LogLevel.Information, Message = "Handshake from host {Sdk} {Version} (instance {Instance}).")]
    private static partial void LogHandshake(ILogger logger, string? sdk, string? version, string instance);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stop requested ({Reason}, grace {Grace}s).")]
    private static partial void LogStop(ILogger logger, string reason, uint grace);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Crashing on request.")]
    private static partial void LogCrash(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Received event {Id} of type {Type} on {Topic} (attempt {Attempt}).")]
    private static partial void LogEvent(ILogger logger, string id, string type, string topic, uint attempt);
}
