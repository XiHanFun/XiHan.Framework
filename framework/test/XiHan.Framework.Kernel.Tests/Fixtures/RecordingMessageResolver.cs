// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 按给定模板解析并记录请求区域的解析器
/// </summary>
/// <remarks>
/// 内核测试不引用本地化实现；用它验证调用方按哪个区域取文案
/// </remarks>
/// <param name="templates">键 → 模板</param>
public sealed class RecordingMessageResolver(IReadOnlyDictionary<string, string> templates) : IMessageResolver
{
    /// <summary>
    /// 每次解析请求的区域，按调用顺序
    /// </summary>
    public List<string?> RequestedLocales { get; } = [];

    /// <inheritdoc />
    public bool TryResolve(string key, string? locale, IReadOnlyDictionary<string, object?>? arguments, [NotNullWhen(true)] out string? text)
    {
        RequestedLocales.Add(locale);
        if (templates.TryGetValue(key, out var template))
        {
            return MessageTemplate.TryFormat(template, arguments, CultureInfo.InvariantCulture, out text);
        }

        text = null;
        return false;
    }
}
