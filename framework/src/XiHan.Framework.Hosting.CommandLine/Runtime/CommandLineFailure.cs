// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine.Runtime;

/// <summary>
/// 把失败写到 stderr
/// </summary>
/// <remarks>
/// 一行 JSON <c>{"code", "detail", "traceId"}</c>，<c>detail</c> 在装配了本地化时按调用区域翻译；没有发起调用就返回的失败按命令行给出的 <c>--locale</c> 翻译、不带 <c>traceId</c>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class CommandLineFailure
{
    /// <summary>
    /// 写失败并返回对应的退出码
    /// </summary>
    /// <remarks>
    /// 没有调用上下文时 <c>detail</c> 按默认区域；命令里没有发起调用就返回的失败改用 <see cref="WriteAsync(CommandContext, Error, int?)"/>，按 <c>--locale</c> 翻译
    /// </remarks>
    /// <param name="error">标准错误</param>
    /// <param name="failure">失败</param>
    /// <param name="services">服务；用于取本地化器</param>
    /// <param name="context">失败所属的调用上下文（区域与 trace-id）；没有发起调用时为空</param>
    /// <param name="exitCode">覆盖按 KIND 映射的退出码（用法错误用 <see cref="CommandLineExitCodes.Usage"/>）</param>
    public static async Task<int> WriteAsync(TextWriter error, Error failure, IServiceProvider services, InvocationContext? context, int? exitCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(services);
        return await WriteCoreAsync(error, failure, services, context, null, exitCode).ConfigureAwait(false);
    }

    /// <summary>
    /// 写没有发起调用就返回的失败并返回对应的退出码
    /// </summary>
    /// <remarks>
    /// 用法错误与找不到暴露的操作在建调用上下文之前就返回，没有 <c>traceId</c>。<c>detail</c> 按命令行给出的 <c>--locale</c> 翻译：是合法的区域名时规范化后与调用一样协商出启用的区域，没给或不合法时按默认区域（<c>spec/protocol/commandline.md</c>「操作映射」，ADR-0325）
    /// </remarks>
    /// <param name="command">命令环境：标准错误、服务与命令行选项</param>
    /// <param name="failure">失败</param>
    /// <param name="exitCode">覆盖按 KIND 映射的退出码（用法错误用 <see cref="CommandLineExitCodes.Usage"/>）；为空时按 KIND 映射</param>
    public static async Task<int> WriteAsync(CommandContext command, Error failure, int? exitCode)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(failure);
        var locale = CommandLineInvocation.FindLocale(command).TryGetValue(out var requested) ? requested : null;
        return await WriteCoreAsync(command.Error, failure, command.Services, null, locale, exitCode).ConfigureAwait(false);
    }

    /// <summary>
    /// 翻译并写出一行失败
    /// </summary>
    /// <remarks>
    /// 区域的先后：调用上下文里的区域，其次命令行给出的区域（经协商），都没有时为默认区域
    /// </remarks>
    private static async Task<int> WriteCoreAsync(TextWriter error, Error failure, IServiceProvider services, InvocationContext? context, string? locale, int? exitCode)
    {
        // 本地化是可选能力：没有装配时 detail 就是代码里的英文消息。命令行给出的区域与调用写进 Baggage 的是同一个规范化的值、同样协商，
        // 同一条命令行没发起调用的失败与发起了的调用拿到同一种语言
        var detail = failure.Message;
        if (services.GetService<IMessageLocalizer>() is { } localizer)
        {
            detail = context is not null
                ? localizer.Localize(failure, context)
                : localizer.Localize(failure, locale is null ? localizer.Options.DefaultLocale : localizer.Options.Negotiate([locale]));
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("code", failure.Code);
            writer.WriteString("detail", detail);
            if (context is not null)
            {
                writer.WriteString("traceId", context.TraceId);
            }

            writer.WriteEndObject();
        }

        await error.WriteLineAsync(Encoding.UTF8.GetString(buffer.WrittenSpan)).ConfigureAwait(false);
        return exitCode ?? CommandLineExitCodes.For(failure);
    }
}
