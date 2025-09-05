#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonArray
// Guid:f3dc3b5d-8b0e-4e48-a62a-f4527b5e8cb3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/5 9:35:36
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
/// 动态 JSON 数组，类似 Newtonsoft.Json 的 JArray
/// </summary>
public class DynamicJsonArray : DynamicObject, IEnumerable<object?>, IList<object?>, IEquatable<DynamicJsonArray>
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly JsonArray _jsonArray;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DynamicJsonArray()
    {
        _jsonArray = [];
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="jsonArray">JSON 数组</param>
    public DynamicJsonArray(JsonArray? jsonArray)
    {
        _jsonArray = jsonArray ?? [];
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="items">初始项目</param>
    public DynamicJsonArray(IEnumerable<object?> items) : this()
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="items">初始项目参数数组</param>
    public DynamicJsonArray(params object?[] items) : this((IEnumerable<object?>)items)
    {
    }

    #region 属性

    /// <summary>
    /// 数组长度
    /// </summary>
    public int Count => _jsonArray.Count;

    /// <summary>
    /// 是否为空
    /// </summary>
    public bool IsEmpty => _jsonArray.Count == 0;

    /// <summary>
    /// 是否为只读（始终为 false）
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// 第一个元素
    /// </summary>
    public dynamic? First => Count > 0 ? this[0] : null;

    /// <summary>
    /// 最后一个元素
    /// </summary>
    public dynamic? Last => Count > 0 ? this[Count - 1] : null;

    /// <summary>
    /// 获取内部 JsonArray（用于内部操作）
    /// </summary>
    internal JsonArray InternalJsonArray => _jsonArray;

    #endregion 属性

    #region 索引器

    /// <summary>
    /// 通过索引访问数组元素
    /// </summary>
    /// <param name="index">索引</param>
    /// <returns>数组元素</returns>
    public object? this[int index]
    {
        get
        {
            if (index < 0 || index >= _jsonArray.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return ConvertToDynamic(_jsonArray[index]);
        }
        set
        {
            if (index < 0 || index >= _jsonArray.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            _jsonArray[index] = ConvertToJsonNode(value);
        }
    }

    #endregion 索引器

    #region 数组操作

    /// <summary>
    /// 添加元素
    /// </summary>
    /// <param name="item">要添加的元素</param>
    public void Add(object? item)
    {
        _jsonArray.Add(ConvertToJsonNode(item));
    }

    /// <summary>
    /// 在指定索引插入元素
    /// </summary>
    /// <param name="index">索引</param>
    /// <param name="item">要插入的元素</param>
    public void Insert(int index, object? item)
    {
        if (index < 0 || index > _jsonArray.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        _jsonArray.Insert(index, ConvertToJsonNode(item));
    }

    /// <summary>
    /// 移除指定元素
    /// </summary>
    /// <param name="item">要移除的元素</param>
    /// <returns>是否成功移除</returns>
    public bool Remove(object? item)
    {
        var jsonNode = ConvertToJsonNode(item);
        for (var i = 0; i < _jsonArray.Count; i++)
        {
            if (JsonNode.DeepEquals(_jsonArray[i], jsonNode))
            {
                _jsonArray.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 移除指定索引的元素
    /// </summary>
    /// <param name="index">索引</param>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _jsonArray.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        _jsonArray.RemoveAt(index);
    }

    /// <summary>
    /// 清空数组
    /// </summary>
    public void Clear()
    {
        _jsonArray.Clear();
    }

    /// <summary>
    /// 检查是否包含指定元素
    /// </summary>
    /// <param name="item">要检查的元素</param>
    /// <returns>是否包含</returns>
    public bool Contains(object? item)
    {
        var jsonNode = ConvertToJsonNode(item);
        return _jsonArray.Any(node => JsonNode.DeepEquals(node, jsonNode));
    }

    /// <summary>
    /// 查找元素的索引
    /// </summary>
    /// <param name="item">要查找的元素</param>
    /// <returns>元素索引，不存在返回 -1</returns>
    public int IndexOf(object? item)
    {
        var jsonNode = ConvertToJsonNode(item);
        for (var i = 0; i < _jsonArray.Count; i++)
        {
            if (JsonNode.DeepEquals(_jsonArray[i], jsonNode))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// 复制数组到指定数组
    /// </summary>
    /// <param name="array">目标数组</param>
    /// <param name="arrayIndex">起始索引</param>
    public void CopyTo(object?[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (arrayIndex < 0 || arrayIndex + Count > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        for (var i = 0; i < Count; i++)
        {
            array[arrayIndex + i] = this[i];
        }
    }

    #endregion 数组操作

    #region 转换方法

    /// <summary>
    /// 转换为字符串
    /// </summary>
    /// <returns>JSON 字符串</returns>
    public override string ToString()
    {
        return _jsonArray.ToJsonString();
    }

    /// <summary>
    /// 转换为格式化的 JSON 字符串
    /// </summary>
    /// <param name="indented">是否缩进</param>
    /// <returns>JSON 字符串</returns>
    public string ToString(bool indented)
    {
        var options = indented ? IndentedOptions : DefaultOptions;
        return JsonSerializer.Serialize(_jsonArray, options);
    }

    /// <summary>
    /// 转换为指定类型的数组
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <returns>转换后的数组</returns>
    public T[]? ToObject<T>()
    {
        try
        {
            return _jsonArray.Deserialize<T[]>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 转换为指定类型的列表
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <returns>转换后的列表</returns>
    public List<T>? ToList<T>()
    {
        try
        {
            return _jsonArray.Deserialize<List<T>>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 尝试转换为指定类型的数组
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public bool TryToObject<T>(out T[]? result)
    {
        result = null;

        try
        {
            result = _jsonArray.Deserialize<T[]>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion 转换方法

    #region LINQ 扩展

    /// <summary>
    /// 获取指定类型的值
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <returns>值的枚举</returns>
    public IEnumerable<T> Values<T>()
    {
        return _jsonArray.Select(item =>
        {
            try
            {
                return item.Deserialize<T>();
            }
            catch
            {
                return default!;
            }
        }).Where(item => item != null)!;
    }

    /// <summary>
    /// 选择对象属性
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <returns>属性值的枚举</returns>
    public IEnumerable<object?> SelectTokens(string propertyName)
    {
        return _jsonArray.OfType<JsonObject>()
            .Where(obj => obj.ContainsKey(propertyName))
            .Select(obj => ConvertToDynamic(obj[propertyName]));
    }

    /// <summary>
    /// 查找第一个匹配的对象
    /// </summary>
    /// <param name="predicate">匹配条件</param>
    /// <returns>匹配的对象</returns>
    public DynamicJsonObject? FirstObject(Func<DynamicJsonObject, bool>? predicate = null)
    {
        var objects = _jsonArray.OfType<JsonObject>().Select(obj => new DynamicJsonObject(obj));
        return predicate == null ? objects.FirstOrDefault() : objects.FirstOrDefault(predicate);
    }

    /// <summary>
    /// 获取所有对象
    /// </summary>
    /// <returns>对象的枚举</returns>
    public IEnumerable<DynamicJsonObject> Objects()
    {
        return _jsonArray.OfType<JsonObject>().Select(obj => new DynamicJsonObject(obj));
    }

    /// <summary>
    /// 获取所有数组
    /// </summary>
    /// <returns>数组的枚举</returns>
    public IEnumerable<DynamicJsonArray> Arrays()
    {
        return _jsonArray.OfType<JsonArray>().Select(array => new DynamicJsonArray(array));
    }

    #endregion LINQ 扩展

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
            DynamicJsonObject dynObj => dynObj.InternalJsonObject,
            DynamicJsonArray dynArray => dynArray._jsonArray,
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
            IEnumerable<object> enumerable => new JsonArray([.. enumerable.Select(ConvertToJsonNode)]),
            _ => JsonValue.Create(value.ToString())
        };
    }

    #endregion 辅助方法

    #region 静态工厂方法

    /// <summary>
    /// 创建空数组
    /// </summary>
    /// <returns>空的动态 JSON 数组</returns>
    public static DynamicJsonArray Create()
    {
        return [];
    }

    /// <summary>
    /// 从 JSON 字符串创建
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>动态 JSON 数组</returns>
    public static DynamicJsonArray Parse(string json)
    {
        var jsonArray = JsonNode.Parse(json) as JsonArray ?? throw new JsonException("输入的 JSON 不是有效的数组格式");
        return [.. jsonArray];
    }

    /// <summary>
    /// 尝试从 JSON 字符串创建
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="result">创建结果</param>
    /// <returns>是否成功创建</returns>
    public static bool TryParse(string json, out DynamicJsonArray? result)
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
    /// <returns>动态 JSON 数组</returns>
    public static DynamicJsonArray FromObject(object obj)
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
    public static bool TryFromObject(object obj, out DynamicJsonArray? result)
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
    public IEnumerator<object?> GetEnumerator()
    {
        return _jsonArray.Select(ConvertToDynamic).GetEnumerator();
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
    public static bool operator ==(DynamicJsonArray? left, DynamicJsonArray? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return JsonNode.DeepEquals(left._jsonArray, right._jsonArray);
    }

    /// <summary>
    /// 不等性比较操作符
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <returns>是否不相等</returns>
    public static bool operator !=(DynamicJsonArray? left, DynamicJsonArray? right)
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
        return obj is DynamicJsonArray other && Equals(other);
    }

    /// <summary>
    /// 类型化 Equals 方法
    /// </summary>
    /// <param name="other">要比较的对象</param>
    /// <returns>是否相等</returns>
    public bool Equals(DynamicJsonArray? other)
    {
        if (other is null)
        {
            return false;
        }

        return JsonNode.DeepEquals(_jsonArray, other._jsonArray);
    }

    /// <summary>
    /// 重写 GetHashCode 方法
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode()
    {
        return _jsonArray.GetHashCode();
    }

    #endregion 比较操作

    #region 动态方法重写

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

        if (indexes.Length == 1 && indexes[0] is int index)
        {
            if (index >= 0 && index < Count)
            {
                result = this[index];
                return true;
            }
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
        if (indexes.Length == 1 && indexes[0] is int index)
        {
            if (index >= 0 && index < Count)
            {
                this[index] = value;
                return true;
            }
        }

        return false;
    }

    #endregion 动态方法重写
}
