// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine.Runtime;

/// <summary>
/// 把失败写到 stderr。
/// </summary>
/// <remarks>
/// 一行 JSON <c>{"code", "detail", "traceId"}</c>，<c>detail</c> 在装配了本地化时按调用区域翻译。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class CommandLineFailure
{
    /// <summary>
    /// 写失败并返回对应的退出码。
    /// </summary>
    /// <param name="error">标准错误。</param>
    /// <param name="failure">失败。</param>
    /// <param name="services">服务；用于取本地化器。</param>
    /// <param name="context">失败所属的调用上下文（区域与 trace-id）；用法错误没有。</param>
    /// <param name="exitCode">覆盖按 KIND 映射的退出码（用法错误用 <see cref="CommandLineExitCodes.Usage"/>）。</param>
    public static async Task<int> WriteAsync(TextWriter error, Error failure, IServiceProvider services, InvocationContext? context, int? exitCode = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(services);

        // 本地化是可选能力：没有装配时 detail 就是代码里的英文消息；有调用上下文时按其区域，否则按默认区域
        var detail = failure.Message;
        if (services.GetService<IMessageLocalizer>() is { } localizer)
        {
            detail = context is not null ? localizer.Localize(failure, context) : localizer.Localize(failure, localizer.Options.DefaultLocale);
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
