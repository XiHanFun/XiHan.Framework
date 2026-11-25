// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.CommandLine.Runtime;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine.Commands;

/// <summary>
/// <c>invoke &lt;operation-id&gt;</c>
/// </summary>
/// <remarks>
/// 把命令行翻译为 XGI 调用。载荷来自 <c>--json</c>（JSON 对象）、<c>--data &lt;file&gt;</c>（文件里的 JSON 对象）或其余 <c>--&lt;field&gt; &lt;value&gt;</c> 选项（按操作的请求 Schema 写成对应类型，重复的选项成数组；操作没有 Schema 时值都写字符串）。调用经完整内核管道；成功把响应 JSON 写到 stdout，失败把 Problem 写到 stderr 并按错误 KIND 返回退出码。只接受暴露给命令行且非内部的操作；弃用的操作照常调用，进管道之前在 stderr 按调用方区域写一行提示（<see cref="CommandLineMessages.OperationDeprecated"/>）
/// </remarks>
internal sealed class InvokeCommand : ICommand
{
    /// <summary>
    /// 严格的 UTF-8 编码
    /// </summary>
    /// <remarks>
    /// 遇到孤立的代理项抛出，不悄悄换成 U+FFFD（ADR-0312）
    /// </remarks>
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <inheritdoc />
    public string Name => "invoke";

    /// <inheritdoc />
    public string DescriptionKey => CommandLineMessages.InvokeDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Arguments.Count == 0)
        {
            return await CommandLineFailure.WriteAsync(context.Error, Error.Validation(CommandLineErrors.UsageInvalid, CommandLineMessages.InvokeOperationMissing), context.Services, null, CommandLineExitCodes.Usage).ConfigureAwait(false);
        }

        var id = context.Arguments[0];
        var registry = context.Services.GetRequiredService<OperationRegistry>();
        if (!registry.Find(id).TryGetValue(out var descriptor) || !descriptor.Contract.IsExposedTo(Expose.CommandLine) || descriptor.Contract.Stability == Stability.Internal)
        {
            return await CommandLineFailure.WriteAsync(context.Error, Error.NotFound(CommandLineErrors.OperationNotExposed, CommandLineMessages.OperationNotExposed, MessageArguments.Of(("id", id))), context.Services, null).ConfigureAwait(false);
        }

        var payload = await PayloadAsync(context, descriptor).ConfigureAwait(false);
        if (!payload.TryGetValue(out var bytes))
        {
            return await CommandLineFailure.WriteAsync(context.Error, payload.Error!, context.Services, null, CommandLineExitCodes.Usage).ConfigureAwait(false);
        }

        var invocation = CommandLineInvocation.Create(context, id);
        if (!invocation.TryGetValue(out var invocationContext))
        {
            return await CommandLineFailure.WriteAsync(context.Error, invocation.Error!, context.Services, null, CommandLineExitCodes.Usage).ConfigureAwait(false);
        }

        // 弃用的操作照常调用，只在 stderr 提示一句：stdout 与退出码不变，脚本的管道不受影响（ADR-0280）。
        // 写在上下文建好之后，才知道调用方的区域（--locale）；用法错误在这之前已经返回，没有发起调用就不提示
        if (descriptor.Contract.Stability == Stability.Deprecated)
        {
            await context.Error.WriteLineAsync(invocationContext.Localize(CommandLineMessages.OperationDeprecated, MessageArguments.Of(("id", id)))).ConfigureAwait(false);
        }

        var response = await context.Services.GetRequiredService<IOperationInvoker>().InvokeAsync(invocationContext, new OperationRequest(id, bytes)).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return await CommandLineFailure.WriteAsync(context.Error, response.Error!, context.Services, invocationContext).ConfigureAwait(false);
        }

        await context.Output.WriteLineAsync(Encoding.UTF8.GetString(response.Payload.Span)).ConfigureAwait(false);
        return CommandLineExitCodes.Success;
    }

    /// <summary>
    /// 载荷
    /// </summary>
    /// <remarks>
    /// <c>--json</c> 优先，其次 <c>--data</c> 文件，否则由字段选项拼成对象（没有任何字段时为空对象）
    /// </remarks>
    private static async ValueTask<Result<ReadOnlyMemory<byte>>> PayloadAsync(CommandContext context, OperationDescriptor descriptor)
    {
        if (context.Option("json").TryGetValue(out var json))
        {
            // 命令行给的是字符串，严格编码成 UTF-8：孤立的代理项与不是 JSON 对象同样是用法错误，不换成 U+FFFD 照常调用（RFC-0001「JSON 文本」，ADR-0312）
            byte[] utf8;
            try
            {
                utf8 = _strictUtf8.GetBytes(json);
            }
            catch (EncoderFallbackException)
            {
                return Error.Validation(CommandLineErrors.UsageInvalid, CommandLineMessages.InvokeJsonInvalid, MessageArguments.Of(("reason", "the text has a lone surrogate and cannot be encoded as UTF-8")));
            }

            return ObjectOrError(utf8);
        }

        if (context.Option("data").TryGetValue(out var data))
        {
            // 允许 @file 的写法，与 curl 一致
            var path = data.StartsWith('@') ? data[1..] : data;
            if (!File.Exists(path))
            {
                return Error.Validation(CommandLineErrors.UsageInvalid, CommandLineMessages.InvokeDataMissing, MessageArguments.Of(("path", path)));
            }

            return ObjectOrError(await File.ReadAllBytesAsync(path, context.CancellationToken).ConfigureAwait(false));
        }

        return Result.Success(FieldOptionPayload.Write(context.CommandLine, descriptor.Schema));
    }

    /// <summary>
    /// 必须是 JSON 对象
    /// </summary>
    private static Result<ReadOnlyMemory<byte>> ObjectOrError(byte[] json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Error.Validation(CommandLineErrors.UsageInvalid, CommandLineMessages.InvokeJsonInvalid, MessageArguments.Of(("reason", "the root must be an object")));
            }
        }
        catch (JsonException exception)
        {
            return Error.Validation(CommandLineErrors.UsageInvalid, CommandLineMessages.InvokeJsonInvalid, MessageArguments.Of(("reason", exception.Message)));
        }

        return Result.Success<ReadOnlyMemory<byte>>(json);
    }
}
