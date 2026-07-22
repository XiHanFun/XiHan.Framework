// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Text.Json;

namespace XiHan.Framework.Workflow.Abstractions.Runtime;

/// <summary>
/// 工作流值转换器
/// </summary>
/// <remarks>
/// 实例变量经 JSON 持久化往返后会变成 <see cref="JsonElement"/>，
/// 本转换器负责把任意来源的变量值归一化为原生类型，并提供目标类型强转，
/// 使活动与表达式求值不必感知持久化细节。
/// </remarks>
public static class WorkflowValueConverter
{
    /// <summary>
    /// 归一化值（递归把 JsonElement 转为 null/bool/string/decimal/List/Dictionary 原生结构，其余类型原样返回）
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>归一化后的值</returns>
    public static object? Normalize(object? value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            // 超出 decimal 范围/精度的数值退化为 double，避免外部接口返回的极值直接故障节点
            JsonValueKind.Number => element.TryGetDecimal(out var decimalValue) ? decimalValue : element.GetDouble(),
            JsonValueKind.Array => element.EnumerateArray().Select(item => Normalize(item)).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => Normalize(property.Value)),
            _ => null
        };
    }

    /// <summary>
    /// 把值转换为目标类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">原始值</param>
    /// <returns>转换结果（原始值为空时返回默认值）</returns>
    /// <exception cref="InvalidCastException">无法转换时抛出</exception>
    public static T? ConvertTo<T>(object? value)
    {
        return (T?)ConvertTo(value, typeof(T));
    }

    /// <summary>
    /// 把值转换为目标类型
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="targetType">目标类型</param>
    /// <returns>转换结果（原始值为空时返回 null）</returns>
    /// <exception cref="InvalidCastException">无法转换时抛出</exception>
    public static object? ConvertTo(object? value, Type targetType)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is null)
        {
            return null;
        }

        // JsonElement 直接按目标类型反序列化，保留复杂对象绑定能力
        if (value is JsonElement element)
        {
            if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            return element.Deserialize(targetType, JsonSerializerOptions.Web);
        }

        if (underlyingType.IsInstanceOfType(value))
        {
            return value;
        }

        if (underlyingType.IsEnum)
        {
            return value is string enumText
                ? Enum.Parse(underlyingType, enumText, ignoreCase: true)
                : Enum.ToObject(underlyingType, System.Convert.ChangeType(value, typeof(long), CultureInfo.InvariantCulture)!);
        }

        if (underlyingType == typeof(TimeSpan))
        {
            return value switch
            {
                string text => TimeSpan.Parse(text, CultureInfo.InvariantCulture),
                _ => TimeSpan.FromSeconds(System.Convert.ToDouble(value, CultureInfo.InvariantCulture))
            };
        }

        if (underlyingType == typeof(DateTime) && value is string dateText)
        {
            return DateTime.Parse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        if (underlyingType == typeof(Guid) && value is string guidText)
        {
            return Guid.Parse(guidText);
        }

        if (value is IConvertible)
        {
            return System.Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
        }

        // 兜底走 JSON 往返，覆盖归一化字典/列表到 POCO 的绑定
        var json = JsonSerializer.Serialize(value, JsonSerializerOptions.Web);
        return JsonSerializer.Deserialize(json, targetType, JsonSerializerOptions.Web);
    }
}
