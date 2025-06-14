#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonObject
// Guid:b8f45c21-d35f-4a29-84b7-28fd84218111
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/6 12:40:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.Diagnostics;
using System.Dynamic;
using System.Text.Json;

namespace XiHan.Framework.Utils.Text.Json.Dynamic;

/// <summary>
/// 动态 JSON 对象，支持点语法访问属性
/// 类似 Newtonsoft.Json 的 JObject 体验
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public class DynamicJsonObject : DynamicObject, IEnumerable<KeyValuePair<string, object?>>
{
    /// <summary>
    /// 内部数据存储
    /// </summary>
    private readonly Dictionary<string, object?> _data;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicJsonObject()
    {
        _data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="data">初始数据</param>
    public DynamicJsonObject(Dictionary<string, object?> data)
    {
        _data = new Dictionary<string, object?>(data, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 属性数量
    /// </summary>
    public int Count => _data.Count;

    /// <summary>
    /// 是否为空
    /// </summary>
    public bool IsEmpty => _data.Count == 0;

    /// <summary>
    /// 所有键
    /// </summary>
    public IEnumerable<string> Keys => _data.Keys;

    /// <summary>
    /// 所有值
    /// </summary>
    public IEnumerable<object?> Values => _data.Values;

    /// <summary>
    /// 索引器访问
    /// </summary>
    /// <param name="key">键</param>
    /// <returns>值</returns>
    public object? this[string key]
    {
        get => _data.TryGetValue(key, out var value) ? value : null;
        set => _data[key] = value;
    }

    /// <summary>
    /// 从字典创建动态 JSON 对象
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject FromDictionary(Dictionary<string, object?> dictionary)
    {
        return new DynamicJsonObject(dictionary);
    }

    /// <summary>
    /// 从 JSON 字符串创建动态 JSON 对象
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject? FromJson(string json)
    {
        return JsonHelper.TryParseJsonDynamic(json, out var result) && result is DynamicJsonObject dynamicObj
            ? dynamicObj
            : null;
    }

    /// <summary>
    /// 隐式转换为字典
    /// </summary>
    /// <param name="obj">动态 JSON 对象</param>
    public static implicit operator Dictionary<string, object?>(DynamicJsonObject obj)
    {
        return obj.ToDictionary();
    }

    /// <summary>
    /// 隐式转换从字典
    /// </summary>
    /// <param name="dictionary">字典</param>
    public static implicit operator DynamicJsonObject(Dictionary<string, object?> dictionary)
    {
        return new DynamicJsonObject(dictionary);
    }

    /// <summary>
    /// 尝试获取成员（属性访问）
    /// </summary>
    /// <param name="binder">绑定器</param>
    /// <param name="result">结果</param>
    /// <returns>是否成功</returns>
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        var name = binder.Name;
        if (_data.TryGetValue(name, out result))
        {
            // 如果值是 DynamicJsonObject，直接返回
            if (result is DynamicJsonObject)
            {
                return true;
            }

            // 如果值是 JsonElement，转换为 DynamicJsonObject
            if (result is JsonElement jsonElement)
            {
                result = JsonHelper.ConvertToDynamic(jsonElement);
                return true;
            }

            return true;
        }

        // 属性不存在时返回 null
        result = null;
        return true;
    }

    /// <summary>
    /// 尝试设置成员（属性赋值）
    /// </summary>
    /// <param name="binder">绑定器</param>
    /// <param name="value">值</param>
    /// <returns>是否成功</returns>
    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        _data[binder.Name] = value;
        return true;
    }

    /// <summary>
    /// 尝试获取索引（数组访问）
    /// </summary>
    /// <param name="binder">绑定器</param>
    /// <param name="indexes">索引</param>
    /// <param name="result">结果</param>
    /// <returns>是否成功</returns>
    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
    {
        result = null;
        return indexes.Length == 1 && indexes[0] is string key && _data.TryGetValue(key, out result);
    }

    /// <summary>
    /// 尝试设置索引（数组赋值）
    /// </summary>
    /// <param name="binder">绑定器</param>
    /// <param name="indexes">索引</param>
    /// <param name="value">值</param>
    /// <returns>是否成功</returns>
    public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object? value)
    {
        if (indexes.Length == 1 && indexes[0] is string key)
        {
            _data[key] = value;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 获取动态成员名称
    /// </summary>
    /// <returns>成员名称集合</returns>
    public override IEnumerable<string> GetDynamicMemberNames()
    {
        return _data.Keys;
    }

    /// <summary>
    /// 尝试转换为指定类型
    /// </summary>
    /// <param name="binder">绑定器</param>
    /// <param name="result">结果</param>
    /// <returns>是否成功</returns>
    public override bool TryConvert(ConvertBinder binder, out object? result)
    {
        if (binder.Type == typeof(string))
        {
            result = ToString();
            return true;
        }

        if (binder.Type == typeof(Dictionary<string, object?>))
        {
            result = new Dictionary<string, object?>(_data);
            return true;
        }

        if (binder.Type.IsAssignableFrom(typeof(DynamicJsonObject)))
        {
            result = this;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// 检查是否包含指定键
    /// </summary>
    /// <param name="key">键</param>
    /// <returns>是否包含</returns>
    public bool ContainsKey(string key)
    {
        return _data.ContainsKey(key);
    }

    /// <summary>
    /// 尝试获取值
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <returns>是否成功</returns>
    public bool TryGetValue(string key, out object? value)
    {
        return _data.TryGetValue(key, out value);
    }

    /// <summary>
    /// 尝试获取值并转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <returns>是否成功</returns>
    public bool TryGetValue<T>(string key, out T? value)
    {
        value = default;
        if (!_data.TryGetValue(key, out var obj))
        {
            return false;
        }

        try
        {
            if (obj is T directValue)
            {
                value = directValue;
                return true;
            }

            if (obj == null)
            {
                return true;
            }

            // 尝试类型转换
            if (typeof(T) == typeof(string))
            {
                value = (T)(object)obj.ToString()!;
                return true;
            }

            // 尝试 JSON 序列化/反序列化转换
            var json = JsonSerializer.Serialize(obj);
            value = JsonSerializer.Deserialize<T>(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取值并转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换后的值</returns>
    public T? GetValue<T>(string key, T? defaultValue = default)
    {
        return TryGetValue<T>(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// 添加或更新值
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    public void Add(string key, object? value)
    {
        _data[key] = value;
    }

    /// <summary>
    /// 移除指定键
    /// </summary>
    /// <param name="key">键</param>
    /// <returns>是否成功移除</returns>
    public bool Remove(string key)
    {
        return _data.Remove(key);
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void Clear()
    {
        _data.Clear();
    }

    /// <summary>
    /// 转换为字典
    /// </summary>
    /// <returns>字典对象</returns>
    public Dictionary<string, object?> ToDictionary()
    {
        return new Dictionary<string, object?>(_data);
    }

    /// <summary>
    /// 转换为 JSON 字符串
    /// </summary>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public string ToJson(JsonSerializerOptions? options = null)
    {
        return JsonHelper.Serialize(_data, options);
    }

    /// <summary>
    /// 转换为字符串
    /// </summary>
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        return ToJson();
    }

    /// <summary>
    /// 获取枚举器
    /// </summary>
    /// <returns>枚举器</returns>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        return _data.GetEnumerator();
    }

    /// <summary>
    /// 获取枚举器（非泛型）
    /// </summary>
    /// <returns>枚举器</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
