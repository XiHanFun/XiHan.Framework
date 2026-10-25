// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol.Proxies;

/// <summary>
/// 插件操作在宿主登记表里的代理：载荷原样搬运，执行时经载体的运行时发给插件。
/// </summary>
internal sealed class ProxyOperationDescriptor(OperationContract contract, string ownerId, IPluginInvoker invoker, OperationSchema? schema)
    : OperationDescriptor(contract, ownerId, typeof(ProxyOperationDescriptor), typeof(PluginPayload), typeof(PluginPayload), schema)
{
    /// <inheritdoc />
    public override Result<object> DecodeRequest(ReadOnlyMemory<byte> payload) => Result.Success<object>(new PluginPayload(payload));

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> EncodeResponse(object response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response is PluginPayload payload ? payload.Bytes : throw new ArgumentException($"The response of proxy operation '{Id}' must be a plugin payload.", nameof(response));
    }

    /// <inheritdoc />
    public override ValueTask<Result<object>> ExecuteAsync(InvocationContext context, object request)
    {
        ArgumentNullException.ThrowIfNull(context);
        return request is PluginPayload payload
            ? invoker.InvokeAsync(Id, payload.Bytes, context)
            : ValueTask.FromResult(Result.Failure<object>(Error.Validation(KernelErrors.OperationTypeMismatch, KernelMessages.OperationTypeMismatch, MessageArguments.Of(("id", Id), ("expected", nameof(PluginPayload)), ("actual", request?.GetType().Name)))));
    }
}
