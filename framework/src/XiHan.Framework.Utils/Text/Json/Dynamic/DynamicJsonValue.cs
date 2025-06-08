#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonValue
// Guid:e8f45c21-d35f-4a29-84b7-28fd84218114
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/6 12:40:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XiHan.Framework.Utils.Text.Json.Dynamic;

/// <summary>
/// 动态 JSON 值，支持各种类型的转换和动态操作
/// 类似 Newtonsoft.Json 的 JValue 体验
/// </summary>
[DebuggerDisplay("{Value} ({ValueType})")]
public class DynamicJsonValue : DynamicObject
{
    /// <summary>
    /// 内部值
    /// </summary>
    private readonly object? _value;

    /// <summary>
    /// 值类型
    /// </summary>
    private readonly Type _valueType;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">值</param>
    public DynamicJsonValue(object? value)
    {
        _value = value;
        _valueType = value?.GetType() ?? typeof(object);
    }

    /// <summary>
    /// 原始值
    /// </summary>
    public object? Value => _value;

    /// <summary>
    /// 值类型
    /// </summary>
    public Type ValueType => _valueType;

    /// <summary>
    /// 是否为 null
    /// </summary>
    public bool IsNull => _value == null;

    /// <summary>
    /// 是否为数字类型
    /// </summary>
    public bool IsNumeric => _value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    /// <summary>
    /// 是否为字符串类型
    /// </summary>
    public bool IsString => _value is string;

    /// <summary>
    /// 是否为布尔类型
    /// </summary>
    public bool IsBoolean => _value is bool;

    /// <summary>
    /// 是否为日期时间类型
    /// </summary>
    public bool IsDateTime => _value is DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan;

    /// <summary>
    /// 从 JsonValue 创建动态值
    /// </summary>
    /// <param name="jsonValue">JsonValue</param>
    /// <returns>动态值</returns>
    public static DynamicJsonValue FromJsonValue(JsonValue jsonValue)
    {
        var value = jsonValue switch
        {
            JsonValue jsonBool when jsonBool.TryGetValue<bool>(out var boolValue) => boolValue,
            JsonValue jsonInt when jsonInt.TryGetValue<int>(out var intValue) => intValue,
            JsonValue jsonLong when jsonLong.TryGetValue<long>(out var longValue) => longValue,
            JsonValue jsonFloat when jsonFloat.TryGetValue<float>(out var floatValue) => floatValue,
            JsonValue jsonDouble when jsonDouble.TryGetValue<double>(out var doubleValue) => doubleValue,
            JsonValue jsonDecimal when jsonDecimal.TryGetValue<decimal>(out var decimalValue) => decimalValue,
            JsonValue jsonString when jsonString.TryGetValue<string>(out var stringValue) => stringValue,
            JsonValue jsonDateTime when jsonDateTime.TryGetValue<DateTime>(out var dateTimeValue) => dateTimeValue,
            JsonValue jsonDateTimeOffset when jsonDateTimeOffset.TryGetValue<DateTimeOffset>(out var dateTimeOffsetValue) => dateTimeOffsetValue,
            JsonValue jsonTimeSpan when jsonTimeSpan.TryGetValue<TimeSpan>(out var timeSpanValue) => timeSpanValue,
            JsonValue jsonGuid when jsonGuid.TryGetValue<Guid>(out var guidValue) => guidValue,
            JsonValue jsonNull when jsonNull.TryGetValue<object>(out var nullValue) => nullValue,
            _ => jsonValue.ToString()
        };

        return new DynamicJsonValue(value);
    }

    /// <summary>
    /// 从任意值创建动态值
    /// </summary>
    /// <param name="value">值</param>
    /// <returns>动态值</returns>
    public static DynamicJsonValue FromValue(object? value)
    {
        return new DynamicJsonValue(value);
    }

    /// <summary>
    /// 隐式转换为字符串
    /// </summary>
    /// <param name="value">动态值</param>
    public static implicit operator string?(DynamicJsonValue? value)
    {
        return value?._value?.ToString();
    }

    /// <summary>
    /// 隐式转换为布尔值
    /// </summary>
    /// <param name="value">动态值</param>
    public static implicit operator bool(DynamicJsonValue? value)
    {
        if (value?._value == null)
        {
            return false;
        }

        if (value._value is bool boolValue)
        {
            return boolValue;
        }

        if (value.IsNumeric)
        {
            return !value._value.Equals(Convert.ChangeType(0, value._valueType));
        }

        return value._value is string stringValue ? !string.IsNullOrEmpty(stringValue) && stringValue != "false" && stringValue != "0" : true;
    }

    /// <summary>
    /// 隐式转换为整数
    /// </summary>
    /// <param name="value">动态值</param>
    public static implicit operator int(DynamicJsonValue? value)
    {
        return value?.ToValue<int>() ?? 0;
    }

    /// <summary>
    /// 隐式转换为长整数
    /// </summary>
    /// <param name="value">动态值</param>
    public static implicit operator long(DynamicJsonValue? value)
    {
        return value?.ToValue<long>() ?? 0;
    }

    /// <summary>
    /// 隐式转换为双精度浮点数
    /// </summary>
    /// <param name="value">动态值</param>
    public static implicit operator double(DynamicJsonValue? value)
    {
        return value?.ToValue<double>() ?? 0.0;
    }

    /// <summary>
    /// 隐式转换为十进制数
    /// </summary>
    /// <param name="value">动态值</param>
    public static implicit operator decimal(DynamicJsonValue? value)
    {
        return value?.ToValue<decimal>() ?? 0m;
    }

    /// <summary>
    /// 隐式转换为日期时间
    /// </summary>
    /// <param name="value">动态值</param>
    public static implicit operator DateTime(DynamicJsonValue? value)
    {
        return value?.ToValue(DateTime.MinValue) ?? DateTime.MinValue;
    }

    /// <summary>
    /// 隐式转换为 GUID
    /// </summary>
    /// <param name="value">动态值</param>
    public static implicit operator Guid(DynamicJsonValue? value)
    {
        return value?.ToValue(Guid.Empty) ?? Guid.Empty;
    }

    /// <summary>
    /// 尝试转换为指定类型
    /// </summary>
    /// <param name="binder">绑定器</param>
    /// <param name="result">结果</param>
    /// <returns>是否成功</returns>
    public override bool TryConvert(ConvertBinder binder, out object? result)
    {
        try
        {
            if (_value == null)
            {
                result = GetDefaultValue(binder.Type);
                return true;
            }

            // 直接类型匹配
            if (binder.Type.IsAssignableFrom(_valueType))
            {
                result = _value;
                return true;
            }

            // 处理可空类型
            var targetType = Nullable.GetUnderlyingType(binder.Type) ?? binder.Type;

            // 尝试直接转换
            if (TryDirectConvert(targetType, out result))
            {
                return true;
            }

            // 尝试字符串转换
            if (TryStringConvert(targetType, out result))
            {
                return true;
            }

            // 尝试数值转换
            if (TryNumericConvert(targetType, out result))
            {
                return true;
            }

            // 最后尝试 JSON 序列化/反序列化
            if (TryJsonConvert(binder.Type, out result))
            {
                return true;
            }

            result = GetDefaultValue(binder.Type);
            return false;
        }
        catch
        {
            result = GetDefaultValue(binder.Type);
            return false;
        }
    }

    /// <summary>
    /// 转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换后的值</returns>
    public T? ToValue<T>(T? defaultValue = default)
    {
        try
        {
            if (_value is T directValue)
            {
                return directValue;
            }

            if (_value == null)
            {
                return defaultValue;
            }

            // 尝试类型转换
            if (typeof(T) == typeof(string))
            {
                return (T)(object)_value.ToString()!;
            }

            if (typeof(T) == typeof(bool) && IsNumeric)
            {
                // 数值转布尔：0 为 false，非 0 为 true
                var isTrue = !_value.Equals(Convert.ChangeType(0, _valueType));
                return (T)(object)isTrue;
            }

            // 使用 Convert.ChangeType
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (Convert.GetTypeCode(_value) != TypeCode.Object)
            {
                var converted = Convert.ChangeType(_value, targetType, CultureInfo.InvariantCulture);
                return (T)converted;
            }

            // 尝试 JSON 序列化/反序列化
            var json = JsonSerializer.Serialize(_value);
            return JsonSerializer.Deserialize<T>(json) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 转换为字符串
    /// </summary>
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        return _value?.ToString() ?? "";
    }

    /// <summary>
    /// 转换为 JSON 字符串
    /// </summary>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public string ToJson(JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(_value, options);
    }

    /// <summary>
    /// 比较是否相等
    /// </summary>
    /// <param name="obj">比较对象</param>
    /// <returns>是否相等</returns>
    public override bool Equals(object? obj)
    {
        return obj switch
        {
            DynamicJsonValue other => Equals(_value, other._value),
            _ => Equals(_value, obj)
        };
    }

    /// <summary>
    /// 获取哈希码
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode()
    {
        return _value?.GetHashCode() ?? 0;
    }

    /// <summary>
    /// 获取类型的默认值
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>默认值</returns>
    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    /// <summary>
    /// 尝试直接类型转换
    /// </summary>
    /// <param name="targetType">目标类型</param>
    /// <param name="result">结果</param>
    /// <returns>是否成功</returns>
    private bool TryDirectConvert(Type targetType, out object? result)
    {
        result = null;

        try
        {
            if (targetType == typeof(string))
            {
                result = _value?.ToString() ?? "";
                return true;
            }

            if (targetType == typeof(object))
            {
                result = _value;
                return true;
            }

            if (_value != null && Convert.GetTypeCode(_value) != TypeCode.Object)
            {
                result = Convert.ChangeType(_value, targetType, CultureInfo.InvariantCulture);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试字符串转换
    /// </summary>
    /// <param name="targetType">目标类型</param>
    /// <param name="result">结果</param>
    /// <returns>是否成功</returns>
    private bool TryStringConvert(Type targetType, out object? result)
    {
        result = null;

        if (_value is not string stringValue)
        {
            return false;
        }

        try
        {
            if (targetType == typeof(bool))
            {
                result = bool.Parse(stringValue);
                return true;
            }

            if (targetType == typeof(DateTime))
            {
                result = DateTime.Parse(stringValue, CultureInfo.InvariantCulture);
                return true;
            }

            if (targetType == typeof(DateTimeOffset))
            {
                result = DateTimeOffset.Parse(stringValue, CultureInfo.InvariantCulture);
                return true;
            }

            if (targetType == typeof(TimeSpan))
            {
                result = TimeSpan.Parse(stringValue, CultureInfo.InvariantCulture);
                return true;
            }

            if (targetType == typeof(Guid))
            {
                result = Guid.Parse(stringValue);
                return true;
            }

            if (targetType.IsEnum)
            {
                result = Enum.Parse(targetType, stringValue, true);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试数值转换
    /// </summary>
    /// <param name="targetType">目标类型</param>
    /// <param name="result">结果</param>
    /// <returns>是否成功</returns>
    private bool TryNumericConvert(Type targetType, out object? result)
    {
        result = null;

        if (!IsNumeric)
        {
            return false;
        }

        try
        {
            if (targetType == typeof(bool))
            {
                // 数值转布尔：0 为 false，非 0 为 true
                result = !_value!.Equals(Convert.ChangeType(0, _valueType));
                return true;
            }

            if (targetType == typeof(string))
            {
                result = _value!.ToString();
                return true;
            }

            if (targetType.IsEnum)
            {
                result = Enum.ToObject(targetType, _value!);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试 JSON 转换
    /// </summary>
    /// <param name="targetType">目标类型</param>
    /// <param name="result">结果</param>
    /// <returns>是否成功</returns>
    private bool TryJsonConvert(Type targetType, out object? result)
    {
        result = null;

        try
        {
            var json = JsonSerializer.Serialize(_value);
            result = JsonSerializer.Deserialize(json, targetType);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
