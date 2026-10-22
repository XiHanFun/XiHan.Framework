// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Grpc.Core;
using XiHan.Framework.Kernel;
using XiHan.Framework.Primitives;
using ProtoError = XiHan.Framework.Plugins.Protocol.V1.Error;
using ProtoStatus = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 原语层 <see cref="Error"/> / <see cref="Result"/> 与协议 <c>Error</c> / <c>Status</c> 之间的转换，以及 gRPC 状态到宿主错误的映射（<c>spec/plugin/invocation.md</c>）。
/// </summary>
internal static class PluginErrorMapper
{
    /// <summary>
    /// 结果 → 协议状态。
    /// </summary>
    public static ProtoStatus ToStatus(Result result)
        => result.IsSuccess ? new ProtoStatus { Ok = true } : new ProtoStatus { Ok = false, Error = ToProto(result.Error!) };

    /// <summary>
    /// 错误 → 协议错误（含内部错误链）。
    /// </summary>
    public static ProtoError ToProto(Error error)
    {
        var proto = new ProtoError
        {
            Code = error.Code,
            MessageKey = error.MessageKey ?? string.Empty,
            Text = error.Text ?? string.Empty,
            Details = error.Details is { Count: > 0 } details ? PluginJson.SerializeDetails(details) : string.Empty,
        };
        if (error.Inner is { } inner)
        {
            proto.Inner = ToProto(inner);
        }

        return proto;
    }

    /// <summary>
    /// 协议状态 → 结果；<c>ok</c> 为假却没有错误时按未预期失败处理。
    /// </summary>
    public static Result FromStatus(ProtoStatus? status, string pluginId, string phase)
    {
        if (status is { Ok: true })
        {
            return Result.Success();
        }

        return status?.Error is { } error
            ? Result.Failure(FromProto(error, pluginId))
            : Result.Failure(Error.Unexpected(PluginErrors.Unexpected, PluginMessages.ProcessFailed, details: MessageArguments.Of(("id", pluginId), ("phase", phase), ("reason", "the plugin reported a failure without an error"))));
    }

    /// <summary>
    /// 协议错误 → 错误：有消息键按键还原，否则按文案字面还原；错误码不合法时整体视为插件的未预期失败。
    /// </summary>
    public static Error FromProto(ProtoError error, string pluginId)
    {
        if (!ErrorCode.IsValid(error.Code))
        {
            return Error.Unexpected(PluginErrors.Unexpected, PluginMessages.ProcessFailed, details: MessageArguments.Of(("id", pluginId), ("phase", "invoke"), ("reason", $"invalid error code '{error.Code}'")));
        }

        var details = string.IsNullOrEmpty(error.Details) ? null : PluginJson.ParseDetails(error.Details);
        var inner = error.Inner is { } nested ? FromProto(nested, pluginId) : null;
        return !string.IsNullOrEmpty(error.MessageKey) && MessageTemplate.IsValidKey(error.MessageKey)
            ? new Error(error.Code, error.MessageKey, details, inner)
            : Error.Literal(error.Code, string.IsNullOrEmpty(error.Text) ? error.Code : error.Text, details, inner);
    }

    /// <summary>
    /// 调用插件时的 gRPC 异常 → 宿主错误：传输层失败归为插件不可用，截止与取消沿用内核错误码；截止时间已过的取消按超时报告。
    /// </summary>
    public static Error FromRpc(RpcException exception, string pluginId, string phase, LifecycleState state, bool deadlineExpired = false) => exception.StatusCode switch
    {
        StatusCode.DeadlineExceeded => new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineExceeded),
        StatusCode.Cancelled when deadlineExpired => new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineExceeded),
        StatusCode.Cancelled => new Error(KernelErrors.Cancelled, KernelMessages.Cancelled),
        StatusCode.Unimplemented => Error.NotFound(KernelErrors.UnknownOperation, KernelMessages.UnknownOperation, MessageArguments.Of(("id", phase))),
        StatusCode.InvalidArgument => Error.Validation(KernelErrors.InvalidOperationPayload, KernelMessages.InvalidOperationPayload, MessageArguments.Of(("id", phase), ("reason", exception.Status.Detail))),
        StatusCode.Unavailable or StatusCode.Internal or StatusCode.Unknown => Error.Unavailable(PluginErrors.Unavailable, PluginMessages.ProcessUnavailable, MessageArguments.Of(("id", pluginId), ("state", state.ToString()))),
        _ => Error.Unexpected(PluginErrors.Unexpected, PluginMessages.ProcessFailed, details: MessageArguments.Of(("id", pluginId), ("phase", phase), ("reason", $"{exception.StatusCode}: {exception.Status.Detail}"))),
    };
}
