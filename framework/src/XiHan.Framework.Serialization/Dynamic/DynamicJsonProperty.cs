#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonProperty
// Guid:b6f9e4d3-ad8c-5e7b-9f2e-3c0d8b7e6f9a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/5 9:35:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Dynamic;
using System.Text.Json.Nodes;

namespace XiHan.Framework.Serialization.Dynamic;

/// <summary>
/// 动态 JSON 属性，类似 Newtonsoft.Json 的 JProperty
/// </summary>
public class DynamicJsonProperty : DynamicObject, IEquatable<DynamicJsonProperty>
{
    private string _name;
    private object? _value;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">属性值</param>
    public DynamicJsonProperty(string name, object? value = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _value = value;
    }

    #region 属性

    /// <summary>
    /// 属性名
    /// </summary>
    public string Name
    {
        get => _name;
        set => _name = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// 属性值
    /// </summary>
    public object? Value
    {
        get => _value;
        set => _value = value;
    }

    /// <summary>
    /// 获取属性值作为 DynamicJsonValue
    /// </summary>
    public DynamicJsonValue DynamicValue => ConvertToDynamicValue(_value);

    /// <summary>
    /// 获取属性值作为 DynamicJsonObject（如果值是对象）
    /// </summary>
    public DynamicJsonObject? AsObject => _value is JsonObject jsonObj ? new DynamicJsonObject(jsonObj) : null;

    /// <summary>
    /// 获取属性值作为 DynamicJsonArray（如果值是数组）
    /// </summary>
    public DynamicJsonArray? AsArray => _value is JsonArray jsonArray ? new DynamicJsonArray(jsonArray) : null;

    #endregion 属性

    #region 类型转换

    /// <summary>
    /// 转换为字符串
    /// </summary>
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        return $"\"{_name}\": {FormatValue(_value)}";
    }

    /// <summary>
    /// 转换值为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <returns>转换后的值</returns>
    public T? ToObject<T>()
    {
        return DynamicValue.ToObject<T>();
    }

    /// <summary>
    /// 尝试获取值
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public bool TryGetValue<T>(out T? result)
    {
        return DynamicValue.TryGetValue(out result);
    }

    #endregion 类型转换

    #region 辅助方法

    /// <summary>
    /// 转换值为动态 JSON 值
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>动态 JSON 值</returns>
    private static DynamicJsonValue ConvertToDynamicValue(object? value)
    {
        return value switch
        {
            null => DynamicJsonValue.CreateNull(),
            JsonValue jsonValue => new DynamicJsonValue(jsonValue),
            JsonNode jsonNode => new DynamicJsonValue(jsonNode.ToString()),
            _ => new DynamicJsonValue(value)
        };
    }

    /// <summary>
    /// 格式化值用于字符串表示
    /// </summary>
    /// <param name="value">要格式化的值</param>
    /// <returns>格式化后的字符串</returns>
    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            string str => $"\"{str}\"",
            bool b => b.ToString().ToLowerInvariant(),
            JsonNode node => node.ToJsonString(),
            _ => value.ToString() ?? "null"
        };
    }

    #endregion 辅助方法

    #region 工厂方法

    /// <summary>
    /// 创建字符串属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">字符串值</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateString(string name, string? value)
    {
        return new DynamicJsonProperty(name, value);
    }

    /// <summary>
    /// 创建数字属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">数字值</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateNumber(string name, double value)
    {
        return new DynamicJsonProperty(name, value);
    }

    /// <summary>
    /// 创建布尔属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">布尔值</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateBoolean(string name, bool value)
    {
        return new DynamicJsonProperty(name, value);
    }

    /// <summary>
    /// 创建 null 属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateNull(string name)
    {
        return new DynamicJsonProperty(name, null);
    }

    /// <summary>
    /// 创建对象属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">对象值</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateObject(string name, DynamicJsonObject? value)
    {
        return new DynamicJsonProperty(name, value?.InternalJsonObject);
    }

    /// <summary>
    /// 创建数组属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">数组值</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateArray(string name, DynamicJsonArray? value)
    {
        return new DynamicJsonProperty(name, value?.InternalJsonArray);
    }

    #endregion 工厂方法

    #region 比较操作

    /// <summary>
    /// 相等性比较操作符
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>是否相等</returns>
    public static bool operator ==(DynamicJsonProperty? left, DynamicJsonProperty? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left._name == right._name && Equals(left._value, right._value);
    }

    /// <summary>
    /// 不等性比较操作符
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>是否不相等</returns>
    public static bool operator !=(DynamicJsonProperty? left, DynamicJsonProperty? right)
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
        return obj is DynamicJsonProperty other && Equals(other);
    }

    /// <summary>
    /// 类型化 Equals 方法
    /// </summary>
    /// <param name="other">要比较的对象</param>
    /// <returns>是否相等</returns>
    public bool Equals(DynamicJsonProperty? other)
    {
        if (other is null)
        {
            return false;
        }

        return _name == other._name && Equals(_value, other._value);
    }

    /// <summary>
    /// 重写 GetHashCode 方法
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(_name, _value);
    }

    #endregion 比较操作

    #region 动态方法重写

    /// <summary>
    /// 动态成员获取
    /// </summary>
    /// <param name="binder">成员绑定器</param>
    /// <param name="result">获取结果</param>
    /// <returns>是否获取成功</returns>
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        result = null;

        switch (binder.Name.ToLowerInvariant())
        {
            case "name":
                result = _name;
                return true;

            case "value":
                result = _value;
                return true;

            case "dynamicvalue":
                result = DynamicValue;
                return true;

            case "asobject":
                result = AsObject;
                return true;

            case "asarray":
                result = AsArray;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// 动态成员设置
    /// </summary>
    /// <param name="binder">成员绑定器</param>
    /// <param name="value">要设置的值</param>
    /// <returns>是否设置成功</returns>
    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        switch (binder.Name.ToLowerInvariant())
        {
            case "name":
                if (value is string name)
                {
                    _name = name;
                    return true;
                }
                return false;

            case "value":
                _value = value;
                return true;

            default:
                return false;
        }
    }

    #endregion 动态方法重写
}
