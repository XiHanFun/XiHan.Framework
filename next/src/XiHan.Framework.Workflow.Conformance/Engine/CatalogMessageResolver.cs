// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 套件用的消息解析器：直接查宿主登记的消息目录。
/// </summary>
/// <remarks>
/// 只按给定区域精确查找、严格格式化，不回退到别的区域：用例据此断言故障在哪个区域渲染成了哪种语言，不依赖宿主是否装配了本地化特性。
/// </remarks>
internal sealed class CatalogMessageResolver : IMessageResolver
{
    /// <summary>
    /// 宿主登记的消息目录。
    /// </summary>
    private readonly IReadOnlyList<MessageCatalog> _catalogs;

    /// <summary>
    /// 创建。
    /// </summary>
    public CatalogMessageResolver(IEnumerable<MessageCatalog> catalogs)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        _catalogs = [.. catalogs];
    }

    /// <inheritdoc />
    public bool TryResolve(string key, string? locale, IReadOnlyDictionary<string, object?>? arguments, [NotNullWhen(true)] out string? text)
    {
        ArgumentNullException.ThrowIfNull(key);
        text = null;
        if (locale is null)
        {
            return false;
        }

        foreach (var catalog in _catalogs)
        {
            if (catalog.Find(key, locale).TryGetValue(out var template))
            {
                return MessageTemplate.TryFormat(template, arguments, CultureInfo.GetCultureInfo(locale), out text);
            }
        }

        return false;
    }
}
