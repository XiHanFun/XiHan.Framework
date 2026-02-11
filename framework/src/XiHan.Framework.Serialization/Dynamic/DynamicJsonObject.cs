#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonObject
// Guid:7b0aa866-0916-453c-8196-16be88fafb1c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/05 09:35:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.Dynamic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Serialization.Dynamic;

/// <summary>
/// 动态 JSON 对象，类似 Newtonsoft.Json 的 JObject
/// </summary>
public class DynamicJsonObject : DynamicObject, IEnumerable<DynamicJsonProperty>, IEquatable<DynamicJsonObject>
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions NonIndentedOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly JsonObject _jsonObject;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicJsonObject()
    {
        _jsonObject = [];
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="jsonObject">JSON 对象</param>
    public DynamicJsonObject(JsonObject? jsonObject)
    {
        _jsonObject = jsonObject ?? [];
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="properties">属性集合</param>
    public DynamicJsonObject(IEnumerable<DynamicJsonProperty> properties) : this()
    {
        foreach (var property in properties)
        {
            Add(property);
        }
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="properties">属性参数数组</param>
    public DynamicJsonObject(params DynamicJsonProperty[] properties) : this((IEnumerable<DynamicJsonProperty>)properties)
    {
    }

    #region 属性

    /// <summary>
    /// 属性数量
    /// </summary>
    public int Count => _jsonObject.Count;

    /// <summary>
    /// 是否为空
    /// </summary>
    public bool IsEmpty => _jsonObject.Count == 0;

    /// <summary>
    /// 所有属性名
    /// </summary>
    public IEnumerable<string> PropertyNames => _jsonObject.Select(kvp => kvp.Key);

    /// <summary>
    /// 所有属性值
    /// </summary>
    public IEnumerable<object?> PropertyValues => _jsonObject.Select(kvp => kvp.Value);

    /// <summary>
    /// 所有属性
    /// </summary>
    public IEnumerable<DynamicJsonProperty> Properties => _jsonObject.Select(kvp => new DynamicJsonProperty(kvp.Key, kvp.Value));

    /// <summary>
    /// 获取内部 JsonObject（用于内部操作）
    /// </summary>
    internal JsonObject InternalJsonObject => _jsonObject;

    #endregion 属性

    #region 索引器

    /// <summary>
    /// 通过属性名访问值
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <returns>属性值</returns>
    public dynamic? this[string propertyName]
    {
        get => GetValue(propertyName);
        set => SetValue(propertyName, value);
    }

    #endregion 索引器

    #region 属性操作

    /// <summary>
    /// 获取属性值
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <returns>属性值</returns>
    public dynamic? GetValue(string propertyName)
    {
        if (!_jsonObject.TryGetPropertyValue(propertyName, out var value))
        {
            return null;
        }

        return ConvertToDynamic(value);
    }

    /// <summary>
    /// 获取属性值作为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <returns>属性值</returns>
    public T? GetValue<T>(string propertyName)
    {
        if (!_jsonObject.TryGetPropertyValue(propertyName, out var value))
        {
            return default;
        }

        try
        {
            return value.Deserialize<T>();
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// 尝试获取属性值
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">属性值</param>
    /// <returns>是否成功获取</returns>
    public bool TryGetValue(string propertyName, out dynamic? value)
    {
        if (_jsonObject.TryGetPropertyValue(propertyName, out var jsonValue))
        {
            value = ConvertToDynamic(jsonValue);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// 尝试获取属性值作为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">属性值</param>
    /// <returns>是否成功获取</returns>
    public bool TryGetValue<T>(string propertyName, out T? value)
    {
        value = default;

        if (!_jsonObject.TryGetPropertyValue(propertyName, out var jsonValue))
        {
            return false;
        }

        try
        {
            value = jsonValue.Deserialize<T>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 设置属性值
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">属性值</param>
    public void SetValue(string propertyName, object? value)
    {
        _jsonObject[propertyName] = ConvertToJsonNode(value);
    }

    /// <summary>
    /// 检查是否包含指定属性
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <returns>是否包含</returns>
    public bool ContainsKey(string propertyName)
    {
        return _jsonObject.ContainsKey(propertyName);
    }

    /// <summary>
    /// 添加属性
    /// </summary>
    /// <param name="property">属性</param>
    public void Add(DynamicJsonProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        _jsonObject[property.Name] = ConvertToJsonNode(property.Value);
    }

    /// <summary>
    /// 添加属性
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">属性值</param>
    public void Add(string propertyName, object? value)
    {
        _jsonObject.Add(propertyName, ConvertToJsonNode(value));
    }

    /// <summary>
    /// 移除属性
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <returns>是否成功移除</returns>
    public bool Remove(string propertyName)
    {
        return _jsonObject.Remove(propertyName);
    }

    /// <summary>
    /// 清空所有属性
    /// </summary>
    public void Clear()
    {
        _jsonObject.Clear();
    }

    #endregion 属性操作

    #region 转换方法

    /// <summary>
    /// 转换为字符串
    /// </summary>
    /// <returns>JSON 字符串</returns>
    public override string ToString()
    {
        return _jsonObject.ToJsonString();
    }

    /// <summary>
    /// 转换为格式化的 JSON 字符串
    /// </summary>
    /// <param name="indented">是否缩进</param>
    /// <returns>JSON 字符串</returns>
    public string ToString(bool indented)
    {
        var options = indented ? IndentedOptions : NonIndentedOptions;
        return JsonSerializer.Serialize(_jsonObject, options);
    }

    /// <summary>
    /// 转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <returns>转换后的对象</returns>
    public T? ToObject<T>()
    {
        try
        {
            return _jsonObject.Deserialize<T>();
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
    public bool TryToObject<T>(out T? result)
    {
        result = default;

        try
        {
            result = _jsonObject.Deserialize<T>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion 转换方法

    #region 辅助方法

    /// <summary>
    /// 将 JsonNode 转换为动态对象
    /// </summary>
    /// <param name="value">JSON 节点</param>
    /// <returns>动态对象</returns>
    private static dynamic? ConvertToDynamic(JsonNode? value)
    {
        return value switch
        {
            null => null,
            JsonObject jsonObj => new DynamicJsonObject(jsonObj),
            JsonArray jsonArray => new DynamicJsonArray(jsonArray),
            JsonValue jsonValue => new DynamicJsonValue(jsonValue),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// 将对象转换为 JsonNode
    /// </summary>
    /// <param name="value">对象值</param>
    /// <returns>JSON 节点</returns>
    private static JsonNode? ConvertToJsonNode(object? value)
    {
        return value switch
        {
            null => null,
            JsonNode node => node,
            DynamicJsonObject dynObj => dynObj._jsonObject,
            DynamicJsonArray dynArray => dynArray.InternalJsonArray,
            DynamicJsonValue dynValue => dynValue.Value,
            string str => JsonValue.Create(str),
            bool b => JsonValue.Create(b),
            byte bt => JsonValue.Create(bt),
            sbyte sbt => JsonValue.Create(sbt),
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

    #endregion 辅助方法

    #region 静态工厂方法

    /// <summary>
    /// 创建空对象
    /// </summary>
    /// <returns>空的动态 JSON 对象</returns>
    public static DynamicJsonObject Create()
    {
        return [];
    }

    /// <summary>
    /// 从 JSON 字符串创建
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject Parse(string json)
    {
        var jsonObject = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("输入的 JSON 不是有效的对象格式");
        return new DynamicJsonObject(jsonObject);
    }

    /// <summary>
    /// 尝试从 JSON 字符串创建
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="result">创建结果</param>
    /// <returns>是否成功创建</returns>
    public static bool TryParse(string json, out DynamicJsonObject? result)
    {
        result = null;

        try
        {
            result = Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从对象创建
    /// </summary>
    /// <param name="obj">源对象</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject FromObject(object obj)
    {
        var json = JsonHelper.Serialize(obj);
        return Parse(json);
    }

    /// <summary>
    /// 尝试从对象创建
    /// </summary>
    /// <param name="obj">源对象</param>
    /// <param name="result">创建结果</param>
    /// <returns>是否成功创建</returns>
    public static bool TryFromObject(object obj, out DynamicJsonObject? result)
    {
        result = null;

        try
        {
            result = FromObject(obj);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion 静态工厂方法

    #region 集合接口实现

    /// <summary>
    /// 获取类型化的枚举器
    /// </summary>
    /// <returns>枚举器</returns>
    public IEnumerator<DynamicJsonProperty> GetEnumerator()
    {
        return _jsonObject.Select(kvp => new DynamicJsonProperty(kvp.Key, kvp.Value)).GetEnumerator();
    }

    /// <summary>
    /// 获取非泛型枚举器
    /// </summary>
    /// <returns>枚举器</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion 集合接口实现

    #region 比较操作

    /// <summary>
    /// 相等性比较操作符
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>是否相等</returns>
    public static bool operator ==(DynamicJsonObject? left, DynamicJsonObject? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return JsonNode.DeepEquals(left._jsonObject, right._jsonObject);
    }

    /// <summary>
    /// 不等性比较操作符
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>是否不相等</returns>
    public static bool operator !=(DynamicJsonObject? left, DynamicJsonObject? right)
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
        return obj is DynamicJsonObject other && Equals(other);
    }

    /// <summary>
    /// 类型化 Equals 方法
    /// </summary>
    /// <param name="other">要比较的对象</param>
    /// <returns>是否相等</returns>
    public bool Equals(DynamicJsonObject? other)
    {
        if (other is null)
        {
            return false;
        }

        return JsonNode.DeepEquals(_jsonObject, other._jsonObject);
    }

    /// <summary>
    /// 重写 GetHashCode 方法
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode()
    {
        return _jsonObject.GetHashCode();
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
        result = GetValue(binder.Name);
        return true;
    }

    /// <summary>
    /// 动态成员设置
    /// </summary>
    /// <param name="binder">成员绑定器</param>
    /// <param name="value">要设置的值</param>
    /// <returns>是否设置成功</returns>
    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        SetValue(binder.Name, value);
        return true;
    }

    /// <summary>
    /// 动态索引获取
    /// </summary>
    /// <param name="binder">索引绑定器</param>
    /// <param name="indexes">索引参数</param>
    /// <param name="result">获取结果</param>
    /// <returns>是否获取成功</returns>
    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
    {
        result = null;

        if (indexes.Length == 1 && indexes[0] is string propertyName)
        {
            result = GetValue(propertyName);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 动态索引设置
    /// </summary>
    /// <param name="binder">索引绑定器</param>
    /// <param name="indexes">索引参数</param>
    /// <param name="value">要设置的值</param>
    /// <returns>是否设置成功</returns>
    public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object? value)
    {
        if (indexes.Length == 1 && indexes[0] is string propertyName)
        {
            SetValue(propertyName, value);
            return true;
        }

        return false;
    }

    #endregion 动态方法重写
}
