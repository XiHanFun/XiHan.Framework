// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 消息键常量上的本地化简写。
/// </summary>
/// <remarks>
/// <c>KernelMessages.ManifestEmpty.L()</c>、<c>OrdersMessages.Placed.L(("name", name))</c>、<c>OrdersMessages.Total.L(count, amount)</c>。文案按 <see cref="MessageScope.Current"/>（调用链里是调用方区域，否则是进程默认区域）解析；没有作用域时返回键与参数的描述。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class MessageKeyExtensions
{
    /// <summary>
    /// 取当前区域的文案。
    /// </summary>
    public static string L(this string key) => L(key, (IReadOnlyDictionary<string, object?>?)null);

    /// <summary>
    /// 取当前区域的文案，参数按名字对应 <c>{name}</c> 占位符。
    /// </summary>
    public static string L(this string key, params ReadOnlySpan<(string Name, object? Value)> arguments)
        => L(key, MessageArguments.Of(arguments));

    /// <summary>
    /// 取当前区域的文案，参数按位置对应 <c>{0}</c>、<c>{1}</c> 占位符。
    /// </summary>
    public static string L(this string key, params ReadOnlySpan<object?> values)
        => L(key, MessageArguments.Positional(values));

    /// <summary>
    /// 取当前区域的文案，参数字典同时可作为 <see cref="Error.Details"/>。
    /// </summary>
    public static string L(this string key, IReadOnlyDictionary<string, object?>? arguments)
    {
        ArgumentNullException.ThrowIfNull(key);
        return MessageScope.Current is { } scope ? scope.Resolve(key, arguments) : MessageTemplate.Describe(key, arguments);
    }
}
