#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonArray
// Guid:c9f45c21-d35f-4a29-84b7-28fd84218112
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
/// 动态 JSON 数组，支持索引访问和 dynamic 操作
/// 类似 Newtonsoft.Json 的 JArray 体验
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public class DynamicJsonArray : DynamicJsonBase, IList<object?>
{
    /// <summary>
    /// 内部数据存储
    /// </summary>
    private readonly List<object?> _data;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicJsonArray()
    {
        _data = [];
    }

    /// <summary>
    /// 获取原始值（列表形式）
    /// </summary>
    public override object? Value => _data;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="data">初始数据</param>
    public DynamicJsonArray(IEnumerable<object?> data)
    {
        _data = [.. data];
    }

    /// <summary>
    /// 元素数量
    /// </summary>
    public int Count => _data.Count;

    /// <summary>
    /// 是否为空
    /// </summary>
    public override bool IsEmpty => _data.Count == 0;

    /// <summary>
    /// 是否只读
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// 索引器访问
    /// </summary>
    /// <param name="index">索引</param>
    /// <returns>值</returns>
    public object? this[int index]
    {
        get => index >= 0 && index < _data.Count ? _data[index] : null;
        set
        {
            if (index >= 0 && index < _data.Count)
            {
                _data[index] = value;
            }
            else if (index == _data.Count)
            {
                _data.Add(value);
            }
        }
    }

    /// <summary>
    /// 从数组创建动态 JSON 数组
    /// </summary>
    /// <param name="array">数组</param>
    /// <returns>动态 JSON 数组</returns>
    public static DynamicJsonArray FromArray(object?[] array)
    {
        return [.. array];
    }

    /// <summary>
    /// 从列表创建动态 JSON 数组
    /// </summary>
    /// <param name="list">列表</param>
    /// <returns>动态 JSON 数组</returns>
    public static DynamicJsonArray FromList(IEnumerable<object?> list)
    {
        return [.. list];
    }

    /// <summary>
    /// 从 JSON 字符串创建动态 JSON 数组
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>动态 JSON 数组</returns>
    public static DynamicJsonArray? FromJson(string json)
    {
        return JsonHelper.TryParseJsonDynamic(json, out var result) && result is DynamicJsonArray dynamicArray
            ? dynamicArray
            : null;
    }

    /// <summary>
    /// 隐式转换为数组
    /// </summary>
    /// <param name="array">动态 JSON 数组</param>
    public static implicit operator object?[](DynamicJsonArray array)
    {
        return [.. array];
    }

    /// <summary>
    /// 隐式转换从数组
    /// </summary>
    /// <param name="array">数组</param>
    public static implicit operator DynamicJsonArray(object?[] array)
    {
        return [.. array];
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
        if (indexes.Length == 1 && indexes[0] is int index)
        {
            if (index >= 0 && index < _data.Count)
            {
                result = _data[index];
                return true;
            }
        }
        return false;
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
        if (indexes.Length == 1 && indexes[0] is int index)
        {
            this[index] = value;
            return true;
        }
        return false;
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

        if (binder.Type == typeof(object[]))
        {
            result = _data.ToArray();
            return true;
        }

        if (binder.Type == typeof(List<object?>))
        {
            result = new List<object?>(_data);
            return true;
        }

        if (binder.Type.IsAssignableFrom(typeof(DynamicJsonArray)))
        {
            result = this;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// 获取指定索引的值并转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="index">索引</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换后的值</returns>
    public T? GetValue<T>(int index, T? defaultValue = default)
    {
        if (index < 0 || index >= _data.Count)
        {
            return defaultValue;
        }

        var obj = _data[index];
        if (obj is T directValue)
        {
            return directValue;
        }

        try
        {
            if (obj == null)
            {
                return defaultValue;
            }

            // 尝试类型转换
            if (typeof(T) == typeof(string))
            {
                return (T)(object)obj.ToString()!;
            }

            // 尝试 JSON 序列化/反序列化转换
            var json = JsonSerializer.Serialize(obj);
            return JsonSerializer.Deserialize<T>(json) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 添加元素
    /// </summary>
    /// <param name="item">元素</param>
    public void Add(object? item)
    {
        _data.Add(item);
    }

    /// <summary>
    /// 插入元素
    /// </summary>
    /// <param name="index">索引</param>
    /// <param name="item">元素</param>
    public void Insert(int index, object? item)
    {
        if (index >= 0 && index <= _data.Count)
        {
            _data.Insert(index, item);
        }
    }

    /// <summary>
    /// 移除元素
    /// </summary>
    /// <param name="item">元素</param>
    /// <returns>是否成功移除</returns>
    public bool Remove(object? item)
    {
        return _data.Remove(item);
    }

    /// <summary>
    /// 移除指定索引的元素
    /// </summary>
    /// <param name="index">索引</param>
    public void RemoveAt(int index)
    {
        if (index >= 0 && index < _data.Count)
        {
            _data.RemoveAt(index);
        }
    }

    /// <summary>
    /// 清空所有元素
    /// </summary>
    public void Clear()
    {
        _data.Clear();
    }

    /// <summary>
    /// 检查是否包含指定元素
    /// </summary>
    /// <param name="item">元素</param>
    /// <returns>是否包含</returns>
    public bool Contains(object? item)
    {
        return _data.Contains(item);
    }

    /// <summary>
    /// 获取指定元素的索引
    /// </summary>
    /// <param name="item">元素</param>
    /// <returns>索引，不存在返回 -1</returns>
    public int IndexOf(object? item)
    {
        return _data.IndexOf(item);
    }

    /// <summary>
    /// 复制到数组
    /// </summary>
    /// <param name="array">目标数组</param>
    /// <param name="arrayIndex">起始索引</param>
    public void CopyTo(object?[] array, int arrayIndex)
    {
        _data.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// 转换为数组
    /// </summary>
    /// <returns>数组</returns>
    public object?[] ToArray()
    {
        return [.. _data];
    }

    /// <summary>
    /// 转换为列表
    /// </summary>
    /// <returns>列表</returns>
    public List<object?> ToList()
    {
        return [.. _data];
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
    public IEnumerator<object?> GetEnumerator()
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
