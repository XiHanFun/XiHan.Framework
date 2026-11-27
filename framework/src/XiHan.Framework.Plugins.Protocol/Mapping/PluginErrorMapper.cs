// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Grpc.Core;
using XiHan.Framework.Kernel;
using XiHan.Framework.Primitives;
using ProtoError = XiHan.Framework.Plugins.Protocol.V1.Error;
using ProtoStatus = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace XiHan.Framework.Plugins.Protocol.Mapping;

/// <summary>
/// 原语层 <see cref="Error"/> / <see cref="Result"/> 与协议 <c>Error</c> / <c>Status</c> 之间的转换
/// </summary>
/// <remarks>
/// 以及 gRPC 状态到宿主错误的映射（<c>spec/plugin/invocation.md</c>）
/// </remarks>
internal static class PluginErrorMapper
{
    /// <summary>
    /// 结果 → 协议状态
    /// </summary>
    public static ProtoStatus ToStatus(Result result)
        => result.IsSuccess ? new ProtoStatus { Ok = true } : new ProtoStatus { Ok = false, Error = ToProto(result.Error!) };

    /// <summary>
    /// 错误 → 协议错误（含内部错误链）
    /// </summary>
    /// <remarks>
    /// 文案（<c>text</c>）里的孤立代理项换成 U+FFFD 写出，其余字段照写（<c>spec/plugin/invocation.md</c> §3「写出 <c>Error</c> 的文案」，ADR-0339）；细节写不出时不带细节（ADR-0320）
    /// </remarks>
    public static ProtoError ToProto(Error error)
    {
        var proto = new ProtoError
        {
            Code = error.Code,
            MessageKey = error.MessageKey ?? string.Empty,
            Text = WellFormed(error.Text ?? string.Empty),
            Details = error.Details is { Count: > 0 } details ? PluginJson.SerializeDetails(details) : string.Empty,
        };
        if (error.Inner is { } inner)
        {
            proto.Inner = ToProto(inner);
        }

        return proto;
    }

    /// <summary>
    /// 把文案里的每个孤立代理项换成一个 U+FFFD
    /// </summary>
    /// <remarks>
    /// 文案是自由文本，不拿来查找、比较或散列：编不成 UTF-8 的部分换掉照写，不让整个应答、回调或钩子因它写不出（ADR-0339）。以前 gRPC 载体的 protobuf 编码同样换成 U+FFFD，Wasm 载体的 JSON 写出器抛出。高代理项紧跟低代理项是平面以外的一个字符，照常保留；替换之后长度不变，与 <see cref="System.Text.Encoding.UTF8"/> 的替换相同
    /// </remarks>
    private static string WellFormed(string text)
    {
        if (!UnicodeText.HasLoneSurrogate(text))
        {
            return text;
        }

        return string.Create(text.Length, text, static (target, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                var current = source[index];
                if (char.IsHighSurrogate(current) && index + 1 < source.Length && char.IsLowSurrogate(source[index + 1]))
                {
                    // 成对的两个码元照抄
                    target[index] = current;
                    index++;
                    target[index] = source[index];
                }
                else
                {
                    // 其余的代理项不论高低、在哪里，都是孤立的
                    target[index] = char.IsSurrogate(current) ? '\uFFFD' : current;
                }
            }
        });
    }

    /// <summary>
    /// 协议状态 → 结果
    /// </summary>
    /// <remarks>
    /// <c>ok</c> 为假却没有错误时按未预期失败处理
    /// </remarks>
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
    /// 协议错误 → 错误
    /// </summary>
    /// <remarks>
    /// 有消息键按键还原，否则按文案字面还原；错误码不合法时整体视为插件的未预期失败
    /// </remarks>
    public static Error FromProto(ProtoError error, string pluginId)
    {
        if (!ErrorCode.IsValid(error.Code))
        {
            return Error.Unexpected(PluginErrors.Unexpected, PluginMessages.ProcessFailed, details: MessageArguments.Of(("id", pluginId), ("phase", "invoke"), ("reason", $"invalid error code '{error.Code}'")));
        }

        var details = string.IsNullOrEmpty(error.Details) ? null : PluginJson.ParseDetails(error.Details);
        var inner = error.Inner is { } nested ? FromProto(nested, pluginId) : null;
        // text 为空或只有空白（RFC-0001「空白字符」，与 char.IsWhiteSpace 同一集合）时文案取错误码：Error 的文案不能只有空白，
        // 原先只查空串，插件写来只有空白的 text 会让 Error.Literal 抛出（ADR-0245）
        return !string.IsNullOrEmpty(error.MessageKey) && MessageTemplate.IsValidKey(error.MessageKey)
            ? new Error(error.Code, error.MessageKey, details, inner)
            : Error.Literal(error.Code, string.IsNullOrWhiteSpace(error.Text) ? error.Code : error.Text, details, inner);
    }

    /// <summary>
    /// 调用插件时的 gRPC 异常 → 宿主错误
    /// </summary>
    /// <remarks>
    /// 按 <c>invocation.md</c> §3：<c>Unavailable</c>、连接失败与进程已退出归为插件不可用，截止沿用内核错误码，<c>Unimplemented</c> / <c>InvalidArgument</c> 映射为内核的未知操作 / 载荷不合法，其余状态（含 <c>Internal</c>、<c>Unknown</c>）都是 <c>PLUGINS_UNEXPECTED_001</c>，细节带状态码与消息。<c>Cancelled</c> 看调用方的令牌：已触发是取消，不按时钟判断是不是超时，截止时间到了的由 Resilience 阶段的 <c>DeadlineMiddleware</c> 按截止令牌改报超时（RFC-0004）；没触发是插件停机或连接释放切断了调用，归为插件不可用（ADR-0127）
    /// </remarks>
    public static Error FromRpc(RpcException exception, string pluginId, string phase, LifecycleState state, bool processExited = false, bool callerCancelled = false) => exception.StatusCode switch
    {
        StatusCode.DeadlineExceeded => new Error(KernelErrors.DeadlineExceeded, KernelMessages.DeadlineExceeded),
        StatusCode.Cancelled when callerCancelled => new Error(KernelErrors.Cancelled, KernelMessages.Cancelled),
        // 调用方没取消：调用是被插件停机或连接释放切断的，调用方可以重试，说成取消会误导它
        StatusCode.Cancelled => Error.Unavailable(PluginErrors.Unavailable, PluginMessages.ProcessUnavailable, MessageArguments.Of(("id", pluginId), ("state", state.ToString()))),
        // 进程已经退出时连接上报出的任何状态都只是退出的后果
        _ when processExited => Error.Unavailable(PluginErrors.Unavailable, PluginMessages.ProcessUnavailable, MessageArguments.Of(("id", pluginId), ("state", state.ToString()))),
        StatusCode.Unimplemented => Error.NotFound(KernelErrors.UnknownOperation, KernelMessages.UnknownOperation, MessageArguments.Of(("id", phase))),
        StatusCode.InvalidArgument => Error.Validation(KernelErrors.InvalidOperationPayload, KernelMessages.InvalidOperationPayload, MessageArguments.Of(("id", phase), ("reason", exception.Status.Detail))),
        StatusCode.Unavailable => Error.Unavailable(PluginErrors.Unavailable, PluginMessages.ProcessUnavailable, MessageArguments.Of(("id", pluginId), ("state", state.ToString()))),
        _ => Error.Unexpected(PluginErrors.Unexpected, PluginMessages.ProcessFailed, details: MessageArguments.Of(("id", pluginId), ("phase", phase), ("reason", $"{exception.StatusCode}: {exception.Status.Detail}"))),
    };
}
