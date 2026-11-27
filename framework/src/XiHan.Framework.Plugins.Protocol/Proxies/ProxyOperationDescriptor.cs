// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Json;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.Hosting;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol.Proxies;

/// <summary>
/// 插件操作在宿主登记表里的代理：载荷原样搬运，执行时经载体的运行时发给插件
/// </summary>
/// <remarks>
/// 插件交回的应答载荷不为空时先过 RFC-0001「JSON 文本」的共同检查再交出去（<c>spec/plugin/invocation.md</c> §1，ADR-0315）：进程外与 Wasm 两种载体都经这里，检查只有这一处
/// </remarks>
internal sealed class ProxyOperationDescriptor(OperationContract contract, string ownerId, IPluginInvoker invoker, OperationSchema? schema)
    : OperationDescriptor(contract, ownerId, typeof(ProxyOperationDescriptor), typeof(PluginPayload), typeof(PluginPayload), schema)
{
    /// <summary>
    /// 核对应答的载体运行时
    /// </summary>
    private readonly IPluginInvoker _invoker = new CheckedInvoker(invoker, ownerId);

    /// <inheritdoc />
    public override Result<object> DecodeRequest(ReadOnlyMemory<byte> payload) => Result.Success<object>(new PluginPayload(payload));

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> EncodeResponse(object response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response is PluginPayload payload ? payload.Bytes : throw new ArgumentException($"The response of proxy operation '{Id}' must be a plugin payload.", nameof(response));
    }

    /// <inheritdoc />
    /// <remarks>
    /// 载荷原样包成插件载荷，与 <see cref="EncodeResponse"/> 互逆。幂等回放的记录可能来自另一个版本的宿主，与调用时同样核对：没过「JSON 文本」的检查返回 <see cref="KernelErrors.InvalidOperationPayload"/>，<c>reason</c> 是问题的路径
    /// </remarks>
    public override Result<object> DecodeResponse(ReadOnlyMemory<byte> payload)
        => ProblemPathOf(payload) is { } path
            ? Error.Validation(KernelErrors.InvalidOperationPayload, KernelMessages.InvalidOperationPayload, MessageArguments.Of(("id", Id), ("reason", path)))
            : Result.Success<object>(new PluginPayload(payload));

    /// <inheritdoc />
    /// <remarks>
    /// 调用记一条 <c>xihan.plugin.invoke</c> Span，归属（插件的清单 ID）与操作 ID 作标签（<c>invocation.md</c> §6，ADR-0269）；应答的核对在 Span 之内，没过时 Span 的状态同样是 Error
    /// </remarks>
    public override ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request)
    {
        ArgumentNullException.ThrowIfNull(context);
        return request is PluginPayload payload
            ? PluginTracing.InvokeAsync(_invoker, OwnerId, Id, payload.Bytes, context)
            : ValueTask.FromResult(Result.Failure<object>(Error.Validation(KernelErrors.OperationTypeMismatch, KernelMessages.OperationTypeMismatch, MessageArguments.Of(("id", Id), ("expected", nameof(PluginPayload)), ("actual", request?.GetType().Name)))));
    }

    /// <summary>
    /// 应答载荷没过「JSON 文本」检查时问题的路径
    /// </summary>
    /// <remarks>
    /// 空的载荷不查：以前就原样交回，协议适配器按没有响应体处理
    /// </remarks>
    /// <returns>为空或通过时为 <see langword="null"/>；否则语法错误是 <c>$</c>，重复成员是第一处重复的路径</returns>
    private static string? ProblemPathOf(ReadOnlyMemory<byte> payload)
        => !payload.IsEmpty && JsonText.FindProblem(payload.Span).TryGetValue(out var problem) ? problem.Path : null;

    /// <summary>
    /// 在载体的运行时外面核对交给插件的元数据与插件交回的应答
    /// </summary>
    /// <remarks>
    /// 元数据写不出（幂等键有孤立的代理项，<c>invocation.md</c> §2，ADR-0334）时不交给载体：不启动进程、不取实例。
    /// 应答没过检查是插件写来的内容不合规，与错误码不合文法同一类处置（<c>invocation.md</c> §1、§3）：这次调用失败，进程与实例照常服务，不按崩溃处理
    /// </remarks>
    /// <param name="inner">载体的运行时</param>
    /// <param name="pluginId">插件的清单 ID</param>
    private sealed class CheckedInvoker(IPluginInvoker inner, string pluginId) : IPluginInvoker
    {
        /// <inheritdoc />
        public async ValueTask<Result<object>> InvokeAsync(string operation, ReadOnlyMemory<byte> payload, InvocationContext context)
        {
            // 两种载体都经这里：没有 Idempotency 阶段的宿主不核对幂等键，gRPC 载体会把孤立的代理项宽松地写成 U+FFFD，Wasm 载体的写出器会抛出
            var writable = PluginMetadata.Check(context, pluginId, operation);
            if (writable.IsFailure)
            {
                return writable.Error!;
            }

            var result = await inner.InvokeAsync(operation, payload, context).ConfigureAwait(false);

            // 载荷不交给协议适配器：之后 MCP 与命令行宽松地解码、gRPC 载体把转义出的孤立代理项写进 protobuf、幂等记录应答时解析抛出，调用方拿不到插件写坏的那份文本
            return result.TryGetValue(out var response) && response is PluginPayload answered && ProblemPathOf(answered.Bytes) is { } path
                ? Error.Unexpected(PluginErrors.Unexpected, PluginMessages.ProcessFailed, MessageArguments.Of(("id", pluginId), ("phase", operation), ("reason", $"the response payload is not valid JSON text at '{path}'")))
                : result;
        }
    }
}
