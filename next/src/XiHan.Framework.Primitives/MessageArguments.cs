// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;

namespace XiHan.Framework.Primitives;

/// <summary>
/// 构造模板参数字典（也是 <see cref="Error.Details"/>）的简写。
/// </summary>
/// <remarks>
/// 按名字 <c>MessageArguments.Of(("id", id), ("reason", reason))</c>，或按位置 <c>MessageArguments.Positional(count, amount)</c>。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class MessageArguments
{
    /// <summary>
    /// 由名值对创建参数字典，对应 <c>{name}</c> 占位符。
    /// </summary>
    /// <remarks>
    /// 名字不能为空白、不能重复，值只允许 JSON 可表示的类型。
    /// </remarks>
    public static IReadOnlyDictionary<string, object?> Of(params ReadOnlySpan<(string Name, object? Value)> pairs)
    {
        var arguments = new Dictionary<string, object?>(pairs.Length, StringComparer.Ordinal);
        foreach (var (name, value) in pairs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (!arguments.TryAdd(name, value))
            {
                throw new ArgumentException($"Argument '{name}' is specified more than once.", nameof(pairs));
            }
        }

        return arguments;
    }

    /// <summary>
    /// 由位置值创建参数字典，键为 <c>0</c>、<c>1</c>…，对应 <c>{0}</c>、<c>{1}</c> 占位符。
    /// </summary>
    public static IReadOnlyDictionary<string, object?> Positional(params ReadOnlySpan<object?> values)
    {
        var arguments = new Dictionary<string, object?>(values.Length, StringComparer.Ordinal);
        for (var i = 0; i < values.Length; i++)
        {
            arguments[i.ToString(CultureInfo.InvariantCulture)] = values[i];
        }

        return arguments;
    }
}
