// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Kernel.Generators.Json;

/// <summary>
/// 按命名空间与元数据名识别 BCL 类型
/// </summary>
/// <remarks>
/// 比对原始定义的元数据名（泛型带元数，如 <c>IDictionary`2</c>）与所在命名空间，不比显示字符串：显示字符串带可空标注（<c>JsonNode?</c>）、泛型实参与关键字，写法一变就漏判。Schema 构建拿不到编译，BCL 类型在多个引用里各有一份（polyfill 包）时 <c>GetTypeByMetadataName</c> 也会返回 null，所以这里按名字比对；拿得到编译的地方（序列化上下文、操作特性）按符号比对
/// </remarks>
internal static class KnownTypes
{
    /// <summary>
    /// 类型是给定命名空间下的顶层类型时返回其元数据名
    /// </summary>
    /// <remarks>
    /// 可空标注与封闭泛型都按原始定义取名；嵌套类型与其他命名空间的类型返回 <see langword="null"/>
    /// </remarks>
    /// <param name="type">类型</param>
    /// <param name="ns">完整命名空间，如 <c>System.Text.Json.Nodes</c></param>
    public static string? NameIn(ITypeSymbol? type, string ns)
        => type?.OriginalDefinition is { ContainingType: null } definition && IsNamespace(definition.ContainingNamespace, ns) ? definition.MetadataName : null;

    /// <summary>
    /// 是否给定命名空间下的某个顶层类型
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="ns">完整命名空间</param>
    /// <param name="metadataName">元数据名，泛型带反引号与元数</param>
    public static bool Is(ITypeSymbol? type, string ns, string metadataName)
        => string.Equals(NameIn(type, ns), metadataName, StringComparison.Ordinal);

    /// <summary>
    /// 是否该类型本身或其派生类型
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="ns">基类的完整命名空间</param>
    /// <param name="metadataName">基类的元数据名</param>
    public static bool IsOrDerivesFrom(ITypeSymbol type, string ns, string metadataName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (Is(current, ns, metadataName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 命名空间是否就是给定的完整名
    /// </summary>
    /// <remarks>
    /// 从最内层逐段向外比对，不拼接字符串；空串表示全局命名空间
    /// </remarks>
    private static bool IsNamespace(INamespaceSymbol? symbol, string ns)
    {
        var end = ns.Length;
        for (var current = symbol; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
        {
            // 这一段必须正好落在 ns 的 [start, end) 上，且前面是开头或分隔点；外层还有命名空间而 ns 已经比完时 start 为负
            var start = end - current.Name.Length;
            if (start < 0 || string.CompareOrdinal(ns, start, current.Name, 0, current.Name.Length) != 0 || (start > 0 && ns[start - 1] != '.'))
            {
                return false;
            }

            end = start == 0 ? -1 : start - 1;
        }

        // 走到全局命名空间时 ns 也必须恰好用完（end 为 -1 表示首段已比完；空串 ns 对全局命名空间为 0）
        return end == -1 || (end == 0 && ns.Length == 0);
    }
}
