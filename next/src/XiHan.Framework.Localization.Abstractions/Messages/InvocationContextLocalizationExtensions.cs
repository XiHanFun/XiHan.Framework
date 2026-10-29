// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions.Messages;

/// <summary>
/// 在调用上下文上直接取当前语言文案的便捷入口。
/// </summary>
/// <remarks>
/// <c>context.Localize(OrdersMessages.Placed, MessageArguments.Of(("name", name)))</c>。区域来自上下文的 Baggage，本地化器来自上下文的服务作用域；没有装配本地化时返回键与参数的描述。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class InvocationContextLocalizationExtensions
{
    /// <summary>
    /// 按上下文里的区域解析消息键。
    /// </summary>
    public static string Localize(this InvocationContext context, string key, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(key);
        // 本地化是可选能力：没有装配时不报错，退回键与参数的描述，让调用方代码在 Minimal 组合下也能运行
        return context.Services.GetService<IMessageLocalizer>() is { } localizer
            ? localizer.Localize(key, context, arguments)
            : MessageTemplate.Describe(key, arguments);
    }

    /// <summary>
    /// 按上下文里的区域翻译错误。
    /// </summary>
    public static string Localize(this InvocationContext context, Error failure)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(failure);
        return context.Services.GetService<IMessageLocalizer>() is { } localizer
            ? localizer.Localize(failure, context)
            : failure.Message;
    }
}
