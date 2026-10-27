// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Kernel.Generators.Json;

/// <summary>
/// JSON Schema 的根 <c>title</c> 与 <c>$defs</c> 定义名：非泛型取类型名；封闭泛型为 <c>&lt;外层名&gt;Of&lt;实参名&gt;</c>，多个实参以 <c>And</c> 连接、嵌套递归
/// （<c>PageResultOfUserResponse</c>、<c>PageResultOfListOfUserResponse</c>、<c>PairOfStringAndInt32</c>）；数组加 <c>Array</c>，可空值类型加 <c>Nullable</c> 前缀，基元类型用 CLR 名。
/// 不同实参的同一泛型因此有不同的名字，OpenAPI 组件、protobuf 消息与前端生成的类型不再串型。
/// </summary>
internal static class SchemaNames
{
    /// <summary>
    /// 类型的 Schema 名。
    /// </summary>
    public static string Of(ITypeSymbol type)
    {
        var builder = new StringBuilder();
        Append(builder, type);
        return builder.ToString();
    }

    /// <summary>
    /// 递归拼接。
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
                builder.Append(named.Name).Append("Of");
                for (var index = 0; index < named.TypeArguments.Length; index++)
                {
                    if (index > 0)
                    {
                        builder.Append("And");
                    }

                    Append(builder, named.TypeArguments[index]);
                }

                return;
            default:
                builder.Append(TypeInfoNames.SimpleName(type));
                return;
        }
    }
}
