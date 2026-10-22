// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Persistence.Generators;

/// <summary>
/// 持久化生成器的诊断。
/// </summary>
internal static class GeneratorRules
{
    /// <summary>
    /// 诊断类别。
    /// </summary>
    public const string Category = "XiHan";

    /// <summary>
    /// 帮助链接。
    /// </summary>
    private const string HelpLink = "https://github.com/XiHanFun/XiHan.Framework/blob/next/next/src/XiHan.Framework.Persistence.Generators/README.md";

    /// <summary>
    /// XHP001：实体类通过多个 <c>IEntity&lt;TKey&gt;</c> 声明了不同的键类型。
    /// </summary>
    public static readonly DiagnosticDescriptor AmbiguousKey = new(
        "XHP001",
        "Entity class must implement IEntity<TKey> with a single key type",
        "Class '{0}' implements IEntity<TKey> with more than one key type; a repository needs exactly one",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>
    /// XHP002：实体类是嵌套类型或不可访问。
    /// </summary>
    public static readonly DiagnosticDescriptor InaccessibleEntity = new(
        "XHP002",
        "Entity class must be a top-level public or internal class",
        "Class '{0}' implements IEntity<TKey> but is nested or not accessible to the generated registration code",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>
    /// XHP003：要生成的过滤规约类与已有类型同名。
    /// </summary>
    public static readonly DiagnosticDescriptor FilterNameConflict = new(
        "XHP003",
        "Generated filter class name conflicts with an existing type",
        "Filters for entity '{0}' were not generated because type '{1}' already exists",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);
}
