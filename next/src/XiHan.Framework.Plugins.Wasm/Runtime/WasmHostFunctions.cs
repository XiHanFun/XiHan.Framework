// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wasmtime;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Plugins.Wasm.Instances;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using Error = XiHan.Framework.Primitives.Error;
using ProtoStatus = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace XiHan.Framework.Plugins.Wasm.Runtime;

/// <summary>
/// 宿主导入 <c>xihan.log</c> / <c>invoke</c> / <c>publish</c>（<c>spec/plugin/wasm.md</c> §4）。
/// </summary>
/// <remarks>
/// 读客体内存里的请求，经 <c>xihan_alloc</c> 写回响应；权限要在清单里声明并且已被宿主授予，回调嵌套深度有上限。回调期间管道再次进入同一插件的调用经 <see cref="Pump"/> 回到执行线程上同步完成。
/// </remarks>
internal sealed partial class WasmHostFunctions(Manifest manifest, PermissionGrant permissions, WasmHostContext host, ILogger logger)
{
    /// <summary>
    /// 本插件的调用泵：外层调用登记执行线程，回调引发的再入在这条线程上泵出。
    /// </summary>
    public WasmCallPump Pump { get; } = new();

    /// <summary>
    /// 回调 <c>Host.Invoke</c> 需要的权限。
    /// </summary>
    public const string PipelinePermission = PluginCallbackPermissions.Pipeline;

    /// <summary>
    /// 回调 <c>Host.Publish</c> 需要的权限。
    /// </summary>
    public const string MessagingPermission = PluginCallbackPermissions.Messaging;

    /// <summary>
    /// 回调时宿主所用主体的认证方案。
    /// </summary>
    public const string PluginScheme = "Plugin";

    /// <summary>
    /// 回调嵌套深度上限。
    /// </summary>
    public const int MaxDepth = 8;

    /// <summary>
    /// 当前回调嵌套深度。
    /// </summary>
    /// <remarks>
    /// 调用串行，无需同步。
    /// </remarks>
    private int _depth;

    /// <summary>
    /// 当前调用（宿主进入模块的那次导出调用）的截止时间。
    /// </summary>
    /// <remarks>
    /// 由 <see cref="RunCall{T}"/> 设置，回调据此约束新调用与发布；没有当前调用（如实例化时的 <c>_initialize</c>）为空。
    /// </remarks>
    private DateTimeOffset? _deadline;

    /// <summary>
    /// 当前调用的取消令牌。
    /// </summary>
    private CancellationToken _cancellation;

    /// <summary>
    /// 以一次导出调用的截止时间与取消运行它。
    /// </summary>
    /// <remarks>
    /// 期间的导入回调都受这两者约束（<c>spec/plugin/wasm.md</c> §4）。调用串行：执行线程上同一时刻只有一次外层调用，再入不经过这里、沿用外层的约束。
    /// </remarks>
    public T RunCall<T>(Func<T> call, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        var (previousDeadline, previousCancellation) = (_deadline, _cancellation);
        (_deadline, _cancellation) = (deadline, cancellationToken);
        try
        {
            return call();
        }
        finally
        {
            (_deadline, _cancellation) = (previousDeadline, previousCancellation);
        }
    }

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
    /// 回调的核心。
    /// </summary>
    /// <remarks>
    /// 权限、嵌套深度、宿主容器，然后在当前线程同步等待管道（Wasm 调用本身是同步的）。新调用的截止时间取请求元数据与当前调用中较早者、取消随当前调用；到期不再等待，返回超时（<c>spec/plugin/wasm.md</c> §4）。
    /// </remarks>
    private InvokeResponse InvokeCore(InvokeRequest request)
    {
        if (!PluginCallbackPermissions.IsAllowed(manifest, permissions, PipelinePermission))
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

        var (deadline, remaining) = Budget();
        if (remaining <= TimeSpan.Zero)
        {
            return new InvokeResponse { Status = PluginErrorMapper.ToStatus(new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineExceeded)) };
        }

        _depth++;
        var timer = new CancellationTokenSource(remaining, host.Clock);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(_cancellation, timer.Token);
        var scope = services.CreateScope();
        Task<OperationResponse>? pending = null;
        try
        {
            // 标记回调流：管道里再次进入本插件的调用据此识别再入，并在本线程等待期间被泵出
            using var flow = Pump.EnterCallback();
            var context = PluginMetadata.CreateContext(scope.ServiceProvider, request.Metadata, manifest.Id, linked.Token, deadline);
            // 宿主以插件身份执行：主体 ID 为清单 ID、方案 Plugin、无声明
            context.SetSecurity(new SecurityContext(new Principal(manifest.Id, PluginScheme), null));
            pending = scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync(context, new OperationRequest(request.Operation, request.Payload.Memory)).AsTask();
            var result = Pump.Wait(pending, linked.Token);
            // 外层调用的令牌也带着它的截止时间：截止时间已到时新调用报出的取消按超时交给插件，与不再等待那条路径同一判断
            var outcome = result.Outcome.Error?.Code == KernelErrors.Cancelled && DeadlineReached(timer, deadline)
                ? Result.Failure(new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineExceeded))
                : result.Outcome;
            return new InvokeResponse
            {
                Status = PluginErrorMapper.ToStatus(outcome),
                Payload = result.Outcome.IsSuccess ? ByteString.CopyFrom(result.Payload.Span) : ByteString.Empty,
            };
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // 当前调用到期或被取消：不再等待，新调用按同一令牌自行收尾
            return new InvokeResponse { Status = PluginErrorMapper.ToStatus(Abandoned(timer, deadline)) };
        }
        finally
        {
            _depth--;
            ReleaseWhenDone(pending, scope, linked, timer);
        }
    }

    /// <summary>
    /// 发布的核心：权限、宿主容器、按主题分组批量发布。
    /// </summary>
    /// <remarks>
    /// 发布受当前调用的截止时间与取消约束，到期不再等待，尚未完成的各项为超时（<c>spec/plugin/wasm.md</c> §4）。
    /// </remarks>
    private void PublishCore(PublishRequest request, PublishResponse response)
    {
        var statuses = new ProtoStatus[request.Envelopes.Count];
        if (!PluginCallbackPermissions.IsAllowed(manifest, permissions, MessagingPermission))
        {
            Array.Fill(statuses, PluginErrorMapper.ToStatus(Error.Denied(PluginErrors.CallbackDenied, PluginMessages.CallbackDenied, MessageArguments.Of(("id", manifest.Id), ("permission", MessagingPermission)))));
            response.Results.AddRange(statuses);
            return;
        }

        var (deadline, remaining) = Budget();
        if (remaining <= TimeSpan.Zero)
        {
            Array.Fill(statuses, PluginErrorMapper.ToStatus(new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineExceeded)));
            response.Results.AddRange(statuses);
            return;
        }

        var timer = new CancellationTokenSource(remaining, host.Clock);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(_cancellation, timer.Token);
        var scope = host.Services?.CreateScope();
        Task<IReadOnlyList<Result>>? pending = null;
        try
        {
            // 标记回调流：订阅了该主题的本插件会在发布期间被再次进入
            using var flow = Pump.EnterCallback();
            var publisher = scope?.ServiceProvider.GetService<IMessagePublisher>();
            // 逐项校验主题与信封，通过的按主题分组、每组一次批量发布；结果按原始位置回填
            var batches = PluginPublishBatches.Collect(request.Envelopes, statuses, publisher is null ? Error.Unavailable(PluginErrors.Unavailable, PluginMessages.MessagingUnavailable, MessageArguments.Of(("id", manifest.Id))) : null);
            foreach (var (topic, indexes, messages) in batches)
            {
                pending = publisher!.PublishManyAsync(topic, messages, linked.Token).AsTask();
                PluginPublishBatches.Scatter(statuses, indexes, Pump.Wait(pending, linked.Token));
            }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            var abandoned = PluginErrorMapper.ToStatus(Abandoned(timer, deadline));
            for (var index = 0; index < statuses.Length; index++)
            {
                statuses[index] ??= abandoned;
            }
        }
        finally
        {
            ReleaseWhenDone(pending, scope, linked, timer);
        }

        response.Results.AddRange(statuses);
    }

    /// <summary>
    /// 当前调用的截止时间与剩余时间。
    /// </summary>
    /// <remarks>
    /// 没有当前调用时（实例化期间的 <c>_initialize</c>）按调用时限从现在算。
    /// </remarks>
    private (DateTimeOffset Deadline, TimeSpan Remaining) Budget()
    {
        var now = host.Clock.GetUtcNow();
        var deadline = _deadline ?? now + host.Options.CallTimeout;
        return (deadline, deadline - now);
    }

    /// <summary>
    /// 不再等待的回调对应的错误：截止时间到了为超时，否则为外层取消。
    /// </summary>
    private Error Abandoned(CancellationTokenSource timer, DateTimeOffset deadline)
        => DeadlineReached(timer, deadline) ? new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineExceeded) : new Error(KernelErrors.Cancelled, KernelMessages.Cancelled);

    /// <summary>
    /// 回调的截止时间是否已到：回调的计时器已触发，或时钟已过截止时间。
    /// </summary>
    /// <remarks>
    /// 不再等待与回调已返回两条路径都用它，外层调用同时被取消也按超时报告（RFC-0004、<c>plugin/wasm.md</c> §4，ADR-0092）。计时器已触发时不再看时钟：定时器可能早于时钟读数到达截止时间。
    /// 外层调用的令牌在它的截止时间也会取消，可能先于本地计时器触发：那一刻宿主分不出是外层的截止时间还是外层调用方取消，只能看时钟；回调交给插件的结果不再经过任何中间件归因，这是宿主唯一能判断的地方。
    /// </remarks>
    private bool DeadlineReached(CancellationTokenSource timer, DateTimeOffset deadline)
        => timer.IsCancellationRequested || host.Clock.GetUtcNow() >= deadline;

    /// <summary>
    /// 回调用到的作用域与令牌源在它的任务结束后释放。
    /// </summary>
    /// <remarks>
    /// 不再等待的回调仍在后台收尾，不能先于它释放；没有任务或任务已结束则立即释放。
    /// </remarks>
    private static void ReleaseWhenDone(Task? pending, params IDisposable?[] resources)
    {
        if (pending is null || pending.IsCompleted)
        {
            Release(resources);
            return;
        }

        pending.ContinueWith(static (_, state) => Release((IDisposable?[])state!), resources, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>
    /// 依次释放。
    /// </summary>
    private static void Release(IDisposable?[] resources)
    {
        foreach (var resource in resources)
        {
            resource?.Dispose();
        }
    }

    /// <summary>
    /// 读客体内存里的一段字节。
    /// </summary>
    /// <remarks>
    /// 越界返回假。
    /// </remarks>
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
    /// 经客体的 <c>xihan_alloc</c> 写回响应，返回 <c>(ptr &lt;&lt; 32) | len</c>。
    /// </summary>
    /// <remarks>
    /// 分配失败返回 0。
    /// </remarks>
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
