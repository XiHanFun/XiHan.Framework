// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine;

/// <summary>
/// <c>invoke &lt;operation-id&gt;</c>：把命令行翻译为 XGI 调用。载荷来自 <c>--json</c>（JSON 对象）、<c>--data &lt;file&gt;</c>（文件里的 JSON 对象）
/// 或其余 <c>--&lt;field&gt; &lt;value&gt;</c> 选项（字符串值，重复的选项成数组；请求类型从字符串读数字需启用 <c>NumberHandling = AllowReadingFromString</c>）。
/// 调用经完整内核管道；成功把响应 JSON 写到 stdout，失败把 Problem 写到 stderr 并按错误 KIND 返回退出码。只接受暴露给命令行且非内部的操作。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InvokeCommand : ICommand
{
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

        var payload = await PayloadAsync(context).ConfigureAwait(false);
        if (!payload.TryGetValue(out var bytes))
        {
            return await CommandLineFailure.WriteAsync(context.Error, payload.Error!, context.Services, null, CommandLineExitCodes.Usage).ConfigureAwait(false);
        }

        var invocation = CommandLineInvocation.Create(context);
        if (!invocation.TryGetValue(out var invocationContext))
        {
            return await CommandLineFailure.WriteAsync(context.Error, invocation.Error!, context.Services, null, CommandLineExitCodes.Usage).ConfigureAwait(false);
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
    /// 载荷：<c>--json</c> 优先，其次 <c>--data</c> 文件，否则由字段选项拼成对象（没有任何字段时为空对象）。
    /// </summary>
    private static async ValueTask<Result<ReadOnlyMemory<byte>>> PayloadAsync(CommandContext context)
    {
        if (context.Option("json").TryGetValue(out var json))
        {
            return ObjectOrError(Encoding.UTF8.GetBytes(json));
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

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (name, values) in context.CommandLine.Options)
            {
                if (CommandLineInvocation.ReservedOptions.Contains(name))
                {
                    continue;
                }

                // 重复的选项是数组，单个是字符串；类型转换交给请求类型的序列化上下文
                if (values.Count > 1)
                {
                    writer.WriteStartArray(name);
                    foreach (var value in values)
                    {
                        writer.WriteStringValue(value);
                    }

                    writer.WriteEndArray();
                }
                else
                {
                    writer.WriteString(name, values[0]);
                }
            }

            writer.WriteEndObject();
        }

        return Result.Success(buffer.WrittenMemory);
    }

    /// <summary>
    /// 必须是 JSON 对象。
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
