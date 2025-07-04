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
using System.Reflection;
using System.Text.Json;

namespace XiHan.Framework.Utils.Text.Json.Dynamic;

/// <summary>
/// 动态 JSON 对象，支持点语法访问属性
/// 类似 Newtonsoft.Json 的 JObject 体验
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public class DynamicJsonObject : DynamicJsonBase, IEnumerable<KeyValuePair<string, object?>>
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
    /// 获取原始值（字典形式）
    /// </summary>
    public override object? Value => _data;

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
    public override bool IsEmpty => _data.Count == 0;

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
    /// 隐式转换为 dynamic，支持直接属性访问
    /// 使用方式：dynamic obj = (DynamicJsonObject)response.Data;
    /// </summary>
    /// <param name="obj">动态 JSON 对象</param>
    public static implicit operator ExpandoObject(DynamicJsonObject obj)
    {
        var expando = new ExpandoObject();
        var dictionary = (IDictionary<string, object?>)expando;
        
        foreach (var kvp in obj._data)
        {
            dictionary[kvp.Key] = kvp.Value;
        }
        
        return expando;
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
    public bool TryGetValue<T>(string key, out T value)
    {
        value = default!;
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
            var result = JsonSerializer.Deserialize<T>(json);
            value = result ?? default!;
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
    public T GetValue<T>(string key, T defaultValue = default!)
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
    public override string ToJson(JsonSerializerOptions? options = null)
    {
        return JsonHelper.Serialize(_data, options);
    }

    /// <summary>
    /// 强类型属性访问器，支持链式调用
    /// 使用方式：var status = obj.Get("status").AsString();
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <returns>属性值的动态包装</returns>
    public DynamicJsonValue Get(string propertyName)
    {
        return GetProperty(propertyName);
    }

    /// <summary>
    /// 强类型属性设置器
    /// 使用方式：obj.Set("status", "success");
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">属性值</param>
    /// <returns>当前对象（支持链式调用）</returns>
    public DynamicJsonObject Set(string propertyName, object? value)
    {
        SetProperty(propertyName, value);
        return this;
    }

    /// <summary>
    /// 获取所有属性，类似 JObject.Properties()
    /// </summary>
    /// <returns>所有属性的集合</returns>
    public IEnumerable<DynamicJsonProperty> Properties()
    {
        return _data.Select(kvp => new DynamicJsonProperty(kvp.Key, kvp.Value));
    }

    /// <summary>
    /// 根据名称获取属性，类似 JObject.Property(name)
    /// </summary>
    /// <param name="name">属性名</param>
    /// <returns>属性对象，不存在则返回 null</returns>
    public DynamicJsonProperty? Property(string name)
    {
        return _data.TryGetValue(name, out var value)
            ? new DynamicJsonProperty(name, value)
            : null;
    }

    /// <summary>
    /// 获取具有指定名称的属性值，类似 JObject[propertyName]
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <returns>属性值，不存在则返回 null</returns>
    public object? GetPropertyValue(string propertyName)
    {
        return Property(propertyName)?.Value;
    }

    /// <summary>
    /// 获取具有指定名称的属性值并转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换后的属性值</returns>
    public T GetPropertyValue<T>(string propertyName, T defaultValue = default!)
    {
        var property = Property(propertyName);
        return property != null ? property.GetValue<T>(defaultValue) : defaultValue;
    }

    /// <summary>
    /// 设置属性值，类似 JObject[propertyName] = value
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">属性值</param>
    public void SetPropertyValue(string propertyName, object? value)
    {
        _data[propertyName] = value;
    }

    /// <summary>
    /// 添加属性，如果已存在则更新
    /// </summary>
    /// <param name="property">属性对象</param>
    public void AddProperty(DynamicJsonProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        _data[property.Name] = property.Value;
    }

    /// <summary>
    /// 移除指定名称的属性
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <returns>是否成功移除</returns>
    public bool RemoveProperty(string propertyName)
    {
        return _data.Remove(propertyName);
    }

    /// <summary>
    /// 检查是否包含指定名称的属性
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <returns>是否包含</returns>
    public bool HasProperty(string propertyName)
    {
        return _data.ContainsKey(propertyName);
    }

    /// <summary>
    /// 枚举所有属性名和值，类似 JObject 的键值对遍历
    /// </summary>
    /// <returns>属性名值对集合</returns>
    public IEnumerable<(string Name, object? Value)> EnumerateProperties()
    {
        return _data.Select(kvp => (kvp.Key, kvp.Value));
    }

    /// <summary>
    /// 查找符合条件的属性
    /// </summary>
    /// <param name="predicate">查找条件</param>
    /// <returns>符合条件的属性集合</returns>
    public IEnumerable<DynamicJsonProperty> FindProperties(Func<DynamicJsonProperty, bool> predicate)
    {
        return Properties().Where(predicate);
    }

    /// <summary>
    /// 查找符合条件的第一个属性
    /// </summary>
    /// <param name="predicate">查找条件</param>
    /// <returns>符合条件的第一个属性，不存在则返回 null</returns>
    public DynamicJsonProperty? FindProperty(Func<DynamicJsonProperty, bool> predicate)
    {
        return Properties().FirstOrDefault(predicate);
    }

    /// <summary>
    /// 深度获取值，支持点号路径，类似 JObject.SelectToken()
    /// </summary>
    /// <param name="path">属性路径，如 "user.name" 或 "items[0].id"</param>
    /// <returns>目标值，不存在则返回 null</returns>
    public object? SelectToken(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var current = (object?)this;
        var pathParts = ParsePath(path);

        foreach (var part in pathParts)
        {
            if (current == null)
            {
                return null;
            }

            if (part.IsIndex)
            {
                // 处理数组索引
                if (current is IList list && part.Index >= 0 && part.Index < list.Count)
                {
                    current = list[part.Index];
                }
                else if (current is Array array && part.Index >= 0 && part.Index < array.Length)
                {
                    current = array.GetValue(part.Index);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                // 处理属性名
                if (current is DynamicJsonObject dynamicObj)
                {
                    current = dynamicObj.GetPropertyValue(part.Name);
                }
                else if (current is IDictionary<string, object?> dict)
                {
                    dict.TryGetValue(part.Name, out current);
                }
                else
                {
                    // 尝试反射获取属性
                    var type = current.GetType();
                    var property = type.GetProperty(part.Name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    if (property != null)
                    {
                        current = property.GetValue(current);
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        return current;
    }

    /// <summary>
    /// 深度获取值并转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="path">属性路径</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换后的值</returns>
    public T SelectToken<T>(string path, T defaultValue = default!)
    {
        var value = SelectToken(path);
        if (value == null)
        {
            return defaultValue;
        }

        try
        {
            if (value is T directValue)
            {
                return directValue;
            }

            if (typeof(T) == typeof(string))
            {
                return (T)(object)value.ToString()!;
            }

            // 尝试 JSON 序列化/反序列化转换
            var json = JsonSerializer.Serialize(value);
            var result = JsonSerializer.Deserialize<T>(json);
            return result != null ? result : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 获取所有叶子节点的属性路径和值
    /// </summary>
    /// <param name="prefix">路径前缀</param>
    /// <returns>叶子节点的路径值对</returns>
    public IEnumerable<(string Path, object? Value)> GetLeafProperties(string prefix = "")
    {
        foreach (var property in Properties())
        {
            var currentPath = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";

            if (property.Value is DynamicJsonObject childObj)
            {
                // 递归处理子对象
                foreach (var leafProperty in childObj.GetLeafProperties(currentPath))
                {
                    yield return leafProperty;
                }
            }
            else if (property.Value is IList list)
            {
                // 处理数组
                for (var i = 0; i < list.Count; i++)
                {
                    var itemPath = $"{currentPath}[{i}]";
                    if (list[i] is DynamicJsonObject itemObj)
                    {
                        foreach (var leafProperty in itemObj.GetLeafProperties(itemPath))
                        {
                            yield return leafProperty;
                        }
                    }
                    else
                    {
                        yield return (itemPath, list[i]);
                    }
                }
            }
            else
            {
                // 叶子节点
                yield return (currentPath, property.Value);
            }
        }
    }

    /// <summary>
    /// 合并另一个 DynamicJsonObject
    /// </summary>
    /// <param name="other">要合并的对象</param>
    /// <param name="overwrite">是否覆盖已存在的属性</param>
    public void Merge(DynamicJsonObject other, bool overwrite = true)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var property in other.Properties())
        {
            if (overwrite || !HasProperty(property.Name))
            {
                SetPropertyValue(property.Name, property.Value);
            }
        }
    }

    /// <summary>
    /// 克隆当前对象
    /// </summary>
    /// <returns>克隆的对象</returns>
    public DynamicJsonObject Clone()
    {
        var clonedData = new Dictionary<string, object?>();

        foreach (var kvp in _data)
        {
            if (kvp.Value is DynamicJsonObject nestedObj)
            {
                clonedData[kvp.Key] = nestedObj.Clone();
            }
            else if (kvp.Value is ICloneable cloneable)
            {
                clonedData[kvp.Key] = cloneable.Clone();
            }
            else
            {
                // 对于其他类型，进行深度复制（通过 JSON 序列化）
                try
                {
                    var json = JsonSerializer.Serialize(kvp.Value);
                    clonedData[kvp.Key] = JsonSerializer.Deserialize<object>(json);
                }
                catch
                {
                    // 如果序列化失败，使用浅复制
                    clonedData[kvp.Key] = kvp.Value;
                }
            }
        }

        return new DynamicJsonObject(clonedData);
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

    /// <summary>
    /// 解析路径字符串
    /// </summary>
    /// <param name="path">路径字符串</param>
    /// <returns>路径部分集合</returns>
    private static List<PathPart> ParsePath(string path)
    {
        var parts = new List<PathPart>();
        var current = "";
        var i = 0;

        while (i < path.Length)
        {
            var c = path[i];

            if (c == '.')
            {
                if (!string.IsNullOrEmpty(current))
                {
                    parts.Add(new PathPart(current));
                    current = "";
                }
            }
            else if (c == '[')
            {
                if (!string.IsNullOrEmpty(current))
                {
                    parts.Add(new PathPart(current));
                    current = "";
                }

                // 查找匹配的 ]
                var indexStart = i + 1;
                var indexEnd = path.IndexOf(']', indexStart);
                if (indexEnd > indexStart)
                {
                    var indexStr = path[indexStart..indexEnd];
                    if (int.TryParse(indexStr, out var index))
                    {
                        parts.Add(new PathPart(index));
                    }
                    i = indexEnd;
                }
            }
            else
            {
                current += c;
            }

            i++;
        }

        if (!string.IsNullOrEmpty(current))
        {
            parts.Add(new PathPart(current));
        }

        return parts;
    }

    /// <summary>
    /// 路径部分
    /// </summary>
    private class PathPart
    {
        public PathPart(string name)
        {
            Name = name;
            IsIndex = false;
        }

        public PathPart(int index)
        {
            Index = index;
            IsIndex = true;
            Name = "";
        }

        public string Name { get; }
        public int Index { get; }
        public bool IsIndex { get; }
    }
}
