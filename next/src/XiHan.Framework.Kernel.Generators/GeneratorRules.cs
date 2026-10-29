// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Kernel.Generators;

/// <summary>
/// 生成器的诊断描述。
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
    private const string HelpLink = "https://github.com/XiHanFun/XiHan.Framework/blob/next/next/src/XiHan.Framework.Kernel.Generators/README.md";

    /// <summary>
    /// XHG001：标注 <c>[Operation]</c> 的类没有实现 <c>IOperation&lt;TRequest, TResponse&gt;</c>。
    /// </summary>
    public static readonly DiagnosticDescriptor MissingOperationInterface = new(
        "XHG001",
        "Operation class must implement IOperation<TRequest, TResponse>",
        "Class '{0}' is annotated with [Operation] but does not implement IOperation<TRequest, TResponse>",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>
    /// XHG002：请求或响应类型没有被任何 <c>JsonSerializerContext</c> 登记。
    /// </summary>
    public static readonly DiagnosticDescriptor MissingJsonTypeInfo = new(
        "XHG002",
        "Operation request and response types must be registered in a JsonSerializerContext",
        "Type '{0}' used by operation '{1}' is not registered with [JsonSerializable] in any JsonSerializerContext of this project",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>
    /// XHG003：操作 ID 不符合文法。
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidOperationId = new(
        "XHG003",
        "Operation ID must follow the operation-id grammar",
        "Operation ID '{0}' on class '{1}' is not valid; expected '<resource>.<verb>' such as 'orders.create'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>
    /// XHG004。
    /// </summary>
    /// <remarks>
    /// 操作没写 <c>Expose</c>，程序集也没有 <c>[assembly: OperationDefaults]</c>，于是暴露给全部协议。缺省关闭：想强制每个操作写明协议的应用以<c>dotnet_diagnostic.XHG004.severity = error</c> 打开，或声明程序集缺省。
    /// </remarks>
    public static readonly DiagnosticDescriptor ImplicitExposeAll = new(
        "XHG004",
        "Operation is exposed on every protocol by default",
        "Operation '{0}' does not set Expose and the assembly has no [assembly: OperationDefaults(Expose = ...)], so it is exposed on every protocol; set Expose explicitly or declare an assembly default",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        helpLinkUri: HelpLink);

    /// <summary>
    /// XHG005：<c>create</c> / <c>update</c> / <c>delete</c> 动词的操作声明了 <c>ReadOnly</c>。
    /// </summary>
    public static readonly DiagnosticDescriptor ReadOnlyOnWriteVerb = new(
        "XHG005",
        "Write verbs cannot be declared read-only",
        "Operation '{0}' declares ReadOnly = true, but the '{1}' verb changes state",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);
}
