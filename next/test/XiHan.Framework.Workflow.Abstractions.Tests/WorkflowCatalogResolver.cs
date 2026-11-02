// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 只查工作流消息目录的解析器，按给定区域精确查找。
/// </summary>
internal sealed class WorkflowCatalogResolver : IMessageResolver
{
    /// <inheritdoc />
    public bool TryResolve(string key, string? locale, IReadOnlyDictionary<string, object?>? arguments, [NotNullWhen(true)] out string? text)
    {
        text = null;
        return locale is not null
            && WorkflowMessages.Catalog.Find(key, locale).TryGetValue(out var template)
            && MessageTemplate.TryFormat(template, arguments, CultureInfo.GetCultureInfo(locale), out text);
    }
}
