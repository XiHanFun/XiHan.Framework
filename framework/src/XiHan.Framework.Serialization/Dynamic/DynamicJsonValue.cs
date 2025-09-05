#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonValue
// Guid:a5f8d3e2-9c7b-4d6a-8e1f-2b9c7a6d4e8f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/5 9:35:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XiHan.Framework.Serialization.Dynamic;

/// <summary>
/// 动态 JSON 值，类似 Newtonsoft.Json 的 JValue
/// </summary>
public class DynamicJsonValue : DynamicObject, IEquatable<DynamicJsonValue>
{
    private readonly JsonValue? _value;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">JSON 值</param>
    public DynamicJsonValue(JsonValue? value)
    {
        _value = value;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">原始值</param>
    public DynamicJsonValue(object? value)
    {
        _value = value switch
        {
            null => null,
            JsonValue jsonValue => jsonValue,
            string str => JsonValue.Create(str),
            bool boolean => JsonValue.Create(boolean),
            byte b => JsonValue.Create(b),
            sbyte sb => JsonValue.Create(sb),
            short s => JsonValue.Create(s),
            ushort us => JsonValue.Create(us),
            int i => JsonValue.Create(i),
            uint ui => JsonValue.Create(ui),
            long l => JsonValue.Create(l),
            ulong ul => JsonValue.Create(ul),
            float f => JsonValue.Create(f),
            double d => JsonValue.Create(d),
            decimal dec => JsonValue.Create(dec),
            DateTime dt => JsonValue.Create(dt),
            DateTimeOffset dto => JsonValue.Create(dto),
            Guid guid => JsonValue.Create(guid),
            _ => JsonValue.Create(value.ToString())
        };
    }

    #region 属性

    /// <summary>
    /// 获取原始 JsonValue
    /// </summary>
    public JsonValue? Value => _value;

    /// <summary>
    /// 获取值类型
    /// </summary>
    public JsonValueKind ValueKind => _value?.GetValueKind() ?? JsonValueKind.Null;

    /// <summary>
    /// 是否为 null
    /// </summary>
    public bool IsNull => _value == null;

    /// <summary>
    /// 是否有值
    /// </summary>
    public bool HasValue => _value != null;

    #endregion 属性

    #region 类型转换

    /// <summary>
    /// 转换为字符串
    /// </summary>
    /// <returns>字符串值</returns>
    public override string ToString()
    {
        return _value?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <returns>转换后的值</returns>
    public T? ToObject<T>()
    {
        if (_value == null)
        {
            return default;
        }

        try
        {
            return _value.Deserialize<T>();
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// 尝试转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public bool TryGetValue<T>(out T? result)
    {
        result = default;

        if (_value == null)
        {
            return false;
        }

        try
        {
            result = _value.Deserialize<T>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion 类型转换

    #region 隐式转换操作符

    /// <summary>隐式转换：字符串 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(string? value) => new(value);

    /// <summary>隐式转换：布尔值 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(bool value) => new(value);

    /// <summary>隐式转换：可空布尔值 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(bool? value) => new(value);

    /// <summary>隐式转换：字节 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(byte value) => new(value);

    /// <summary>隐式转换：可空字节 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(byte? value) => new(value);

    /// <summary>隐式转换：有符号字节 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(sbyte value) => new(value);

    /// <summary>隐式转换：可空有符号字节 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(sbyte? value) => new(value);

    /// <summary>隐式转换：短整数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(short value) => new(value);

    /// <summary>隐式转换：可空短整数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(short? value) => new(value);

    /// <summary>隐式转换：无符号短整数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(ushort value) => new(value);

    /// <summary>隐式转换：可空无符号短整数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(ushort? value) => new(value);

    /// <summary>隐式转换：整数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(int value) => new(value);

    /// <summary>隐式转换：可空整数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(int? value) => new(value);

    /// <summary>隐式转换：无符号整数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(uint value) => new(value);

    /// <summary>隐式转换：可空无符号整数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(uint? value) => new(value);

    /// <summary>隐式转换：长整数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(long value) => new(value);

    /// <summary>隐式转换：可空长整数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(long? value) => new(value);

    /// <summary>隐式转换：无符号长整数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(ulong value) => new(value);

    /// <summary>隐式转换：可空无符号长整数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(ulong? value) => new(value);

    /// <summary>隐式转换：单精度浮点数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(float value) => new(value);

    /// <summary>隐式转换：可空单精度浮点数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(float? value) => new(value);

    /// <summary>隐式转换：双精度浮点数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(double value) => new(value);

    /// <summary>隐式转换：可空双精度浮点数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(double? value) => new(value);

    /// <summary>隐式转换：十进制数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(decimal value) => new(value);

    /// <summary>隐式转换：可空十进制数 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(decimal? value) => new(value);

    /// <summary>隐式转换：日期时间 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(DateTime value) => new(value);

    /// <summary>隐式转换：可空日期时间 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(DateTime? value) => new(value);

    /// <summary>隐式转换：带时区的日期时间 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(DateTimeOffset value) => new(value);

    /// <summary>隐式转换：可空带时区的日期时间 → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(DateTimeOffset? value) => new(value);

    /// <summary>隐式转换：GUID → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(Guid value) => new(value);

    /// <summary>隐式转换：可空GUID → DynamicJsonValue</summary>
    public static implicit operator DynamicJsonValue(Guid? value) => new(value);

    // 反向转换
    /// <summary>显式转换：DynamicJsonValue → 字符串</summary>
    public static explicit operator string?(DynamicJsonValue value) => value._value?.GetValue<string>();

    /// <summary>显式转换：DynamicJsonValue → 布尔值</summary>
    public static explicit operator bool(DynamicJsonValue value) => value._value?.GetValue<bool>() ?? false;

    /// <summary>显式转换：DynamicJsonValue → 可空布尔值</summary>
    public static explicit operator bool?(DynamicJsonValue value) => value._value?.GetValue<bool?>();

    /// <summary>显式转换：DynamicJsonValue → 字节</summary>
    public static explicit operator byte(DynamicJsonValue value) => value._value?.GetValue<byte>() ?? 0;

    /// <summary>显式转换：DynamicJsonValue → 可空字节</summary>
    public static explicit operator byte?(DynamicJsonValue value) => value._value?.GetValue<byte?>();

    /// <summary>显式转换：DynamicJsonValue → 有符号字节</summary>
    public static explicit operator sbyte(DynamicJsonValue value) => value._value?.GetValue<sbyte>() ?? 0;

    /// <summary>显式转换：DynamicJsonValue → 可空有符号字节</summary>
    public static explicit operator sbyte?(DynamicJsonValue value) => value._value?.GetValue<sbyte?>();

    /// <summary>显式转换：DynamicJsonValue → 短整数</summary>
    public static explicit operator short(DynamicJsonValue value) => value._value?.GetValue<short>() ?? 0;

    /// <summary>显式转换：DynamicJsonValue → 可空短整数</summary>
    public static explicit operator short?(DynamicJsonValue value) => value._value?.GetValue<short?>();

    /// <summary>显式转换：DynamicJsonValue → 无符号短整数</summary>
    public static explicit operator ushort(DynamicJsonValue value) => value._value?.GetValue<ushort>() ?? 0;

    /// <summary>显式转换：DynamicJsonValue → 可空无符号短整数</summary>
    public static explicit operator ushort?(DynamicJsonValue value) => value._value?.GetValue<ushort?>();

    /// <summary>显式转换：DynamicJsonValue → 整数</summary>
    public static explicit operator int(DynamicJsonValue value) => value._value?.GetValue<int>() ?? 0;

    /// <summary>显式转换：DynamicJsonValue → 可空整数</summary>
    public static explicit operator int?(DynamicJsonValue value) => value._value?.GetValue<int?>();

    /// <summary>显式转换：DynamicJsonValue → 无符号整数</summary>
    public static explicit operator uint(DynamicJsonValue value) => value._value?.GetValue<uint>() ?? 0;

    /// <summary>显式转换：DynamicJsonValue → 可空无符号整数</summary>
    public static explicit operator uint?(DynamicJsonValue value) => value._value?.GetValue<uint?>();

    /// <summary>显式转换：DynamicJsonValue → 长整数</summary>
    public static explicit operator long(DynamicJsonValue value) => value._value?.GetValue<long>() ?? 0;

    /// <summary>显式转换：DynamicJsonValue → 可空长整数</summary>
    public static explicit operator long?(DynamicJsonValue value) => value._value?.GetValue<long?>();

    /// <summary>显式转换：DynamicJsonValue → 无符号长整数</summary>
    public static explicit operator ulong(DynamicJsonValue value) => value._value?.GetValue<ulong>() ?? 0;

    /// <summary>显式转换：DynamicJsonValue → 可空无符号长整数</summary>
    public static explicit operator ulong?(DynamicJsonValue value) => value._value?.GetValue<ulong?>();

    /// <summary>显式转换：DynamicJsonValue → 单精度浮点数</summary>
    public static explicit operator float(DynamicJsonValue value) => value._value?.GetValue<float>() ?? 0f;

    /// <summary>显式转换：DynamicJsonValue → 可空单精度浮点数</summary>
    public static explicit operator float?(DynamicJsonValue value) => value._value?.GetValue<float?>();

    /// <summary>显式转换：DynamicJsonValue → 双精度浮点数</summary>
    public static explicit operator double(DynamicJsonValue value) => value._value?.GetValue<double>() ?? 0d;

    /// <summary>显式转换：DynamicJsonValue → 可空双精度浮点数</summary>
    public static explicit operator double?(DynamicJsonValue value) => value._value?.GetValue<double?>();

    /// <summary>显式转换：DynamicJsonValue → 十进制数</summary>
    public static explicit operator decimal(DynamicJsonValue value) => value._value?.GetValue<decimal>() ?? 0m;

    /// <summary>显式转换：DynamicJsonValue → 可空十进制数</summary>
    public static explicit operator decimal?(DynamicJsonValue value) => value._value?.GetValue<decimal?>();

    /// <summary>显式转换：DynamicJsonValue → 日期时间</summary>
    public static explicit operator DateTime(DynamicJsonValue value) => value._value?.GetValue<DateTime>() ?? default;

    /// <summary>显式转换：DynamicJsonValue → 可空日期时间</summary>
    public static explicit operator DateTime?(DynamicJsonValue value) => value._value?.GetValue<DateTime?>();

    /// <summary>显式转换：DynamicJsonValue → 带时区的日期时间</summary>
    public static explicit operator DateTimeOffset(DynamicJsonValue value) => value._value?.GetValue<DateTimeOffset>() ?? default;

    /// <summary>显式转换：DynamicJsonValue → 可空带时区的日期时间</summary>
    public static explicit operator DateTimeOffset?(DynamicJsonValue value) => value._value?.GetValue<DateTimeOffset?>();

    /// <summary>显式转换：DynamicJsonValue → GUID</summary>
    public static explicit operator Guid(DynamicJsonValue value) => value._value?.GetValue<Guid>() ?? default;

    /// <summary>显式转换：DynamicJsonValue → 可空GUID</summary>
    public static explicit operator Guid?(DynamicJsonValue value) => value._value?.GetValue<Guid?>();

    #endregion 隐式转换操作符

    #region 比较操作符

    /// <summary>
    /// 相等性比较操作符
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>是否相等</returns>
    public static bool operator ==(DynamicJsonValue? left, DynamicJsonValue? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return Equals(left._value, right._value);
    }

    /// <summary>
    /// 不等性比较操作符
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>是否不相等</returns>
    public static bool operator !=(DynamicJsonValue? left, DynamicJsonValue? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// 重写 Equals 方法
    /// </summary>
    /// <param name="obj">要比较的对象</param>
    /// <returns>是否相等</returns>
    public override bool Equals(object? obj)
    {
        return obj is DynamicJsonValue other && Equals(other);
    }

    /// <summary>
    /// 类型化 Equals 方法
    /// </summary>
    /// <param name="other">要比较的对象</param>
    /// <returns>是否相等</returns>
    public bool Equals(DynamicJsonValue? other)
    {
        if (other is null)
        {
            return false;
        }

        return Equals(_value, other._value);
    }

    /// <summary>
    /// 重写 GetHashCode 方法
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode()
    {
        return _value?.GetHashCode() ?? 0;
    }

    #endregion 比较操作符

    #region 静态工厂方法

    /// <summary>
    /// 创建 null 值
    /// </summary>
    /// <returns>null 值</returns>
    public static DynamicJsonValue CreateNull() => new(null);

    /// <summary>
    /// 从字符串创建
    /// </summary>
    /// <param name="value">字符串值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue CreateString(string? value) => new(value);

    /// <summary>
    /// 从布尔值创建
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue CreateBoolean(bool value) => new(value);

    /// <summary>
    /// 从数字创建
    /// </summary>
    /// <param name="value">数字值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue CreateNumber(double value) => new(value);

    /// <summary>
    /// 从任意对象创建
    /// </summary>
    /// <param name="value">对象值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue Create(object? value) => new(value);

    #endregion 静态工厂方法

    #region 动态方法重写

    /// <summary>
    /// 动态类型转换
    /// </summary>
    /// <param name="binder">转换绑定器</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public override bool TryConvert(ConvertBinder binder, out object? result)
    {
        result = null;

        if (_value == null)
        {
            return !binder.Type.IsValueType && (result = null) == null;
        }

        try
        {
            if (binder.Type == typeof(string))
            {
                result = ToString();
                return true;
            }

            if (binder.Type.IsGenericType && binder.Type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = Nullable.GetUnderlyingType(binder.Type);
                if (underlyingType != null)
                {
                    result = Convert.ChangeType(_value.ToString(), underlyingType, CultureInfo.InvariantCulture);
                    return true;
                }
            }

            result = Convert.ChangeType(_value.ToString(), binder.Type, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion 动态方法重写
}
