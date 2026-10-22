// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wasmtime;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using Error = XiHan.Framework.Primitives.Error;

namespace XiHan.Framework.Plugins.Wasm;

/// <summary>
/// 宿主导入 <c>xihan.log</c> / <c>invoke</c> / <c>publish</c>（<c>spec/plugin/wasm.md</c> §4）：读客体内存里的请求，经 <c>xihan_alloc</c> 写回响应；权限按清单授予判定，回调嵌套深度有上限。
/// 回调期间管道再次进入同一插件的调用经 <see cref="Pump"/> 回到执行线程上同步完成。
/// </summary>
internal sealed partial class WasmHostFunctions(Manifest manifest, PermissionGrant permissions, WasmHostContext host, ILogger logger)
{
    /// <summary>
    /// 本插件的调用泵：外层调用登记执行线程，回调引发的再入在这条线程上泵出。
    /// </summary>
    public WasmCallPump Pump { get; } = new();

    /// <summary>
    /// 回调 <c>Host.Invoke</c> 需要的权限。
    /// </summary>
    public const string PipelinePermission = "capability:kernel.pipeline";

    /// <summary>
    /// 回调 <c>Host.Publish</c> 需要的权限。
    /// </summary>
    public const string MessagingPermission = "capability:messaging.queue";

    /// <summary>
    /// 回调时宿主所用主体的认证方案。
    /// </summary>
    public const string PluginScheme = "Plugin";

    /// <summary>
    /// 回调嵌套深度上限。
    /// </summary>
    public const int MaxDepth = 8;

    /// <summary>
    /// 当前回调嵌套深度；调用串行，无需同步。
    /// </summary>
    private int _depth;

    /// <summary>
    /// <c>xihan.log(level, ptr, len)</c>。
    /// </summary>
    public void Log(Caller caller, int level, int pointer, int length)
    {
        if (!TryRead(caller, pointer, length, out var bytes))
        {
            return;
        }

        var text = System.Text.Encoding.UTF8.GetString(bytes);
        LogGuest(logger, level switch
        {
            0 => LogLevel.Trace,
            1 => LogLevel.Debug,
            3 => LogLevel.Warning,
            4 => LogLevel.Error,
            5 => LogLevel.Critical,
            _ => LogLevel.Information,
        }, manifest.Id, text);
    }

    /// <summary>
    /// <c>xihan.invoke(ptr, len) -> i64</c>：以插件身份经宿主完整管道调用操作。
    /// </summary>
    public long Invoke(Caller caller, int pointer, int length)
    {
        if (!TryRead(caller, pointer, length, out var bytes))
        {
            return 0;
        }

        var request = PluginJsonCodec.Decode<InvokeRequest>(bytes);
        var response = request is null
            ? new InvokeResponse { Status = PluginErrorMapper.ToStatus(Error.Validation(PluginErrors.InvalidModule, PluginMessages.ModuleInvalid, MessageArguments.Of(("id", manifest.Id), ("reason", "xihan.invoke received an invalid InvokeRequest")))) }
            : InvokeCore(request);
        return Write(caller, PluginJsonCodec.Encode(response));
    }

    /// <summary>
    /// <c>xihan.publish(ptr, len) -> i64</c>：经宿主消息发布器发布。
    /// </summary>
    public long Publish(Caller caller, int pointer, int length)
    {
        if (!TryRead(caller, pointer, length, out var bytes))
        {
            return 0;
        }

        var request = PluginJsonCodec.Decode<PublishRequest>(bytes);
        var response = new PublishResponse();
        if (request is null)
        {
            response.Results.Add(PluginErrorMapper.ToStatus(Error.Validation(PluginErrors.InvalidModule, PluginMessages.ModuleInvalid, MessageArguments.Of(("id", manifest.Id), ("reason", "xihan.publish received an invalid PublishRequest")))));
        }
        else
        {
            PublishCore(request, response);
        }

        return Write(caller, PluginJsonCodec.Encode(response));
    }

    /// <summary>
    /// 回调的核心：权限、嵌套深度、宿主容器，然后在当前线程同步等待管道（Wasm 调用本身是同步的）。
    /// </summary>
    private InvokeResponse InvokeCore(InvokeRequest request)
    {
        if (!permissions.IsGranted(PipelinePermission))
        {
            return new InvokeResponse { Status = PluginErrorMapper.ToStatus(Error.Denied(PluginErrors.CallbackDenied, PluginMessages.CallbackDenied, MessageArguments.Of(("id", manifest.Id), ("permission", PipelinePermission)))) };
        }

        if (host.Services is not { } services)
        {
            return new InvokeResponse { Status = PluginErrorMapper.ToStatus(Error.Unavailable(PluginErrors.Unavailable, PluginMessages.ProcessUnavailable, MessageArguments.Of(("id", manifest.Id), ("state", "Resolved")))) };
        }

        if (_depth >= MaxDepth)
        {
            return new InvokeResponse { Status = PluginErrorMapper.ToStatus(Error.Unavailable(PluginErrors.ResourceExhausted, PluginMessages.SandboxExhausted, MessageArguments.Of(("id", manifest.Id), ("reason", $"callback nesting exceeds {MaxDepth}")))) };
        }

        _depth++;
        try
        {
            // 标记回调流：管道里再次进入本插件的调用据此识别再入，并在本线程等待期间被泵出
            using var flow = Pump.EnterCallback();
            using var scope = services.CreateScope();
            var context = PluginMetadata.CreateContext(scope.ServiceProvider, request.Metadata, manifest.Id, CancellationToken.None);
            // 宿主以插件身份执行：主体 ID 为清单 ID、方案 Plugin、无声明
            context.SetSecurity(new SecurityContext(new Principal(manifest.Id, PluginScheme), null));
            var result = Pump.Wait(scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync(context, new OperationRequest(request.Operation, request.Payload.Memory)).AsTask());
            return new InvokeResponse
            {
                Status = PluginErrorMapper.ToStatus(result.Outcome),
                Payload = result.Outcome.IsSuccess ? ByteString.CopyFrom(result.Payload.Span) : ByteString.Empty,
            };
        }
        finally
        {
            _depth--;
        }
    }

    /// <summary>
    /// 发布的核心：权限、宿主容器、逐项发布。
    /// </summary>
    private void PublishCore(PublishRequest request, PublishResponse response)
    {
        if (!permissions.IsGranted(MessagingPermission))
        {
            var denied = PluginErrorMapper.ToStatus(Error.Denied(PluginErrors.CallbackDenied, PluginMessages.CallbackDenied, MessageArguments.Of(("id", manifest.Id), ("permission", MessagingPermission))));
            foreach (var _ in request.Envelopes)
            {
                response.Results.Add(denied);
            }

            return;
        }

        // 标记回调流：订阅了该主题的本插件会在发布期间被再次进入
        using var flow = Pump.EnterCallback();
        using var scope = host.Services?.CreateScope();
        var publisher = scope?.ServiceProvider.GetService<IMessagePublisher>();
        foreach (var envelope in request.Envelopes)
        {
            if (publisher is null)
            {
                response.Results.Add(PluginErrorMapper.ToStatus(Error.Unavailable(PluginErrors.Unavailable, PluginMessages.MessagingUnavailable, MessageArguments.Of(("id", manifest.Id)))));
                continue;
            }

            if (!TopicName.IsValid(envelope.Topic))
            {
                response.Results.Add(PluginErrorMapper.ToStatus(Error.Validation(MessagingErrors.InvalidTopic, MessagingMessages.InvalidTopic, MessageArguments.Of(("topic", envelope.Topic)))));
                continue;
            }

            var converted = PluginEnvelopeMapper.FromProto(envelope);
            if (!converted.TryGetValue(out var message))
            {
                response.Results.Add(PluginErrorMapper.ToStatus(converted.Error!));
                continue;
            }

            response.Results.Add(PluginErrorMapper.ToStatus(Pump.Wait(publisher.PublishAsync(envelope.Topic, message, CancellationToken.None).AsTask())));
        }
    }

    /// <summary>
    /// 读客体内存里的一段字节；越界返回假。
    /// </summary>
    private static bool TryRead(Caller caller, int pointer, int length, out byte[] bytes)
    {
        var memory = caller.GetMemory("memory");
        if (memory is null || pointer < 0 || length < 0 || pointer + (long)length > memory.GetLength())
        {
            bytes = [];
            return false;
        }

        bytes = memory.GetSpan(pointer, length).ToArray();
        return true;
    }

    /// <summary>
    /// 经客体的 <c>xihan_alloc</c> 写回响应，返回 <c>(ptr &lt;&lt; 32) | len</c>；分配失败返回 0。
    /// </summary>
    private static long Write(Caller caller, byte[] bytes)
    {
        var alloc = caller.GetFunction("xihan_alloc")?.WrapFunc<int, int>();
        var memory = caller.GetMemory("memory");
        if (alloc is null || memory is null)
        {
            return 0;
        }

        var pointer = alloc(bytes.Length);
        if (pointer <= 0 || pointer + (long)bytes.Length > memory.GetLength())
        {
            return 0;
        }

        bytes.CopyTo(memory.GetSpan(pointer, bytes.Length));
        return ((long)pointer << 32) | (uint)bytes.Length;
    }

    /// <summary>
    /// 记录客体经 <c>xihan.log</c> 写出的一行。
    /// </summary>
    [LoggerMessage(Message = "[plugins.{PluginId}] {Line}")]
    private static partial void LogGuest(ILogger logger, LogLevel level, string pluginId, string line);
}
