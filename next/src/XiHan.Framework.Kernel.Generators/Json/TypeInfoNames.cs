// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Kernel.Generators.Json;

/// <summary>
/// System.Text.Json 源生成器为 <c>[JsonSerializable(typeof(T))]</c> 生成的 <c>Default.&lt;属性&gt;</c> 默认名：
/// 简单类型用 CLR 名（<c>Int32</c>、<c>String</c>），泛型为类型名加各实参名（<c>ListOrderResponse</c>），数组加 <c>Array</c>，可空值类型加 <c>Nullable</c> 前缀。
/// </summary>
internal static class TypeInfoNames
{
    /// <summary>
    /// 推导默认属性名。
    /// </summary>
    public static string DefaultPropertyName(ITypeSymbol type)
    {
        var builder = new StringBuilder();
        Append(builder, type);
        return builder.ToString();
    }

    /// <summary>
    /// 递归拼接类型名。
    /// </summary>
    private static void Append(StringBuilder builder, ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                Append(builder, array.ElementType);
                builder.Append("Array");
                return;
            case INamedTypeSymbol { IsGenericType: true } named when named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T:
                builder.Append("Nullable");
                Append(builder, named.TypeArguments[0]);
                return;
            case INamedTypeSymbol { IsGenericType: true } named:
                builder.Append(named.Name);
                foreach (var argument in named.TypeArguments)
                {
                    Append(builder, argument);
                }

                return;
            default:
                builder.Append(SimpleName(type));
                return;
        }
    }

    /// <summary>
    /// 非泛型、非数组类型的名字：基元类型用 CLR 名（<c>Int32</c>、<c>String</c>），其余取类型名。
    /// </summary>
    public static string SimpleName(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Boolean => "Boolean",
        SpecialType.System_Byte => "Byte",
        SpecialType.System_SByte => "SByte",
        SpecialType.System_Int16 => "Int16",
        SpecialType.System_UInt16 => "UInt16",
        SpecialType.System_Int32 => "Int32",
        SpecialType.System_UInt32 => "UInt32",
        SpecialType.System_Int64 => "Int64",
        SpecialType.System_UInt64 => "UInt64",
        SpecialType.System_Single => "Single",
        SpecialType.System_Double => "Double",
        SpecialType.System_Decimal => "Decimal",
        SpecialType.System_Char => "Char",
        SpecialType.System_String => "String",
        SpecialType.System_Object => "Object",
        _ => type.Name,
    };
}
