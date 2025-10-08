#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonFactory
// Guid:f0j3i8h7-eh2g-9i1f-di6i-7g4h2f1g0i3e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/5 9:35:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Nodes;

namespace XiHan.Framework.Serialization.Dynamic;

/// <summary>
/// 动态 JSON 工厂类
/// </summary>
public static class DynamicJsonFactory
{
    #region 对象创建

    /// <summary>
    /// 创建空的动态 JSON 对象
    /// </summary>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject CreateObject()
    {
        return [];
    }

    /// <summary>
    /// 创建动态 JSON 对象并设置初始属性
    /// </summary>
    /// <param name="properties">初始属性</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject CreateObject(params (string Name, object? Value)[] properties)
    {
        var obj = new DynamicJsonObject();
        foreach (var (name, value) in properties)
        {
            obj[name] = value;
        }
        return obj;
    }

    /// <summary>
    /// 创建动态 JSON 对象并设置初始属性
    /// </summary>
    /// <param name="properties">初始属性字典</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject CreateObject(IDictionary<string, object?> properties)
    {
        var obj = new DynamicJsonObject();
        foreach (var kvp in properties)
        {
            obj[kvp.Key] = kvp.Value;
        }
        return obj;
    }

    /// <summary>
    /// 创建动态 JSON 对象并设置初始属性
    /// </summary>
    /// <param name="properties">初始属性</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject CreateObject(params DynamicJsonProperty[] properties)
    {
        return [.. properties];
    }

    #endregion 对象创建

    #region 数组创建

    /// <summary>
    /// 创建空的动态 JSON 数组
    /// </summary>
    /// <returns>动态 JSON 数组</returns>
    public static DynamicJsonArray CreateArray()
    {
        return [];
    }

    /// <summary>
    /// 创建动态 JSON 数组并设置初始项目
    /// </summary>
    /// <param name="items">初始项目</param>
    /// <returns>动态 JSON 数组</returns>
    public static DynamicJsonArray CreateArray(params object?[] items)
    {
        return [.. items];
    }

    /// <summary>
    /// 创建动态 JSON 数组并设置初始项目
    /// </summary>
    /// <param name="items">初始项目集合</param>
    /// <returns>动态 JSON 数组</returns>
    public static DynamicJsonArray CreateArray(IEnumerable<object?> items)
    {
        return [.. items];
    }

    #endregion 数组创建

    #region 值创建

    /// <summary>
    /// 创建字符串值
    /// </summary>
    /// <param name="value">字符串值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue CreateString(string? value)
    {
        return DynamicJsonValue.CreateString(value);
    }

    /// <summary>
    /// 创建数字值
    /// </summary>
    /// <param name="value">数字值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue CreateNumber(double value)
    {
        return DynamicJsonValue.CreateNumber(value);
    }

    /// <summary>
    /// 创建整数值
    /// </summary>
    /// <param name="value">整数值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue CreateNumber(int value)
    {
        return new DynamicJsonValue(value);
    }

    /// <summary>
    /// 创建长整数值
    /// </summary>
    /// <param name="value">长整数值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue CreateNumber(long value)
    {
        return new DynamicJsonValue(value);
    }

    /// <summary>
    /// 创建十进制数值
    /// </summary>
    /// <param name="value">十进制数值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue CreateNumber(decimal value)
    {
        return new DynamicJsonValue(value);
    }

    /// <summary>
    /// 创建布尔值
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue CreateBoolean(bool value)
    {
        return DynamicJsonValue.CreateBoolean(value);
    }

    /// <summary>
    /// 创建 null 值
    /// </summary>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue CreateNull()
    {
        return DynamicJsonValue.CreateNull();
    }

    /// <summary>
    /// 创建日期时间值
    /// </summary>
    /// <param name="value">日期时间值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue CreateDateTime(DateTime value)
    {
        return new DynamicJsonValue(value);
    }

    /// <summary>
    /// 创建 GUId 值
    /// </summary>
    /// <param name="value">GUId 值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue CreateGuid(Guid value)
    {
        return new DynamicJsonValue(value);
    }

    #endregion 值创建

    #region 属性创建

    /// <summary>
    /// 创建字符串属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">字符串值</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateProperty(string name, string? value)
    {
        return DynamicJsonProperty.CreateString(name, value);
    }

    /// <summary>
    /// 创建数字属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">数字值</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateProperty(string name, double value)
    {
        return DynamicJsonProperty.CreateNumber(name, value);
    }

    /// <summary>
    /// 创建整数属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">整数值</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateProperty(string name, int value)
    {
        return new DynamicJsonProperty(name, value);
    }

    /// <summary>
    /// 创建布尔属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">布尔值</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateProperty(string name, bool value)
    {
        return DynamicJsonProperty.CreateBoolean(name, value);
    }

    /// <summary>
    /// 创建 null 属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateNullProperty(string name)
    {
        return DynamicJsonProperty.CreateNull(name);
    }

    /// <summary>
    /// 创建对象属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">对象值</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateProperty(string name, DynamicJsonObject value)
    {
        return DynamicJsonProperty.CreateObject(name, value);
    }

    /// <summary>
    /// 创建数组属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">数组值</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateProperty(string name, DynamicJsonArray value)
    {
        return DynamicJsonProperty.CreateArray(name, value);
    }

    /// <summary>
    /// 创建通用属性
    /// </summary>
    /// <param name="name">属性名</param>
    /// <param name="value">属性值</param>
    /// <returns>动态 JSON 属性</returns>
    public static DynamicJsonProperty CreateProperty(string name, object? value)
    {
        return new DynamicJsonProperty(name, value);
    }

    #endregion 属性创建

    #region 解析和转换

    /// <summary>
    /// 解析 JSON 字符串为动态对象（自动判断类型）
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>动态对象</returns>
    public static dynamic? Parse(string json)
    {
        return json.ToDynamic();
    }

    /// <summary>
    /// 尝试解析 JSON 字符串为动态对象
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="result">解析结果</param>
    /// <returns>是否解析成功</returns>
    public static bool TryParse(string json, out dynamic? result)
    {
        result = null;

        try
        {
            result = Parse(json);
            return result != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从对象创建动态 JSON（自动判断类型）
    /// </summary>
    /// <param name="obj">源对象</param>
    /// <returns>动态 JSON 对象</returns>
    public static dynamic? FromObject(object obj)
    {
        if (obj == null)
        {
            return CreateNull();
        }

        try
        {
            var json = JsonSerializer.Serialize(obj);
            var node = JsonNode.Parse(json);

            return node switch
            {
                JsonObject jsonObj => new DynamicJsonObject(jsonObj),
                JsonArray jsonArray => new DynamicJsonArray(jsonArray),
                JsonValue jsonValue => new DynamicJsonValue(jsonValue),
                _ => new DynamicJsonValue(obj)
            };
        }
        catch
        {
            return new DynamicJsonValue(obj);
        }
    }

    /// <summary>
    /// 尝试从对象创建动态 JSON
    /// </summary>
    /// <param name="obj">源对象</param>
    /// <param name="result">创建结果</param>
    /// <returns>是否创建成功</returns>
    public static bool TryFromObject(object obj, out dynamic? result)
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

    #endregion 解析和转换

    #region 构建器模式

    /// <summary>
    /// 创建对象构建器
    /// </summary>
    /// <returns>对象构建器</returns>
    public static ObjectBuilder Object()
    {
        return new ObjectBuilder();
    }

    /// <summary>
    /// 创建数组构建器
    /// </summary>
    /// <returns>数组构建器</returns>
    public static ArrayBuilder Array()
    {
        return new ArrayBuilder();
    }

    /// <summary>
    /// 对象构建器
    /// </summary>
    public class ObjectBuilder
    {
        private readonly DynamicJsonObject _object = [];

        /// <summary>
        /// 添加属性
        /// </summary>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        /// <returns>构建器</returns>
        public ObjectBuilder Add(string name, object? value)
        {
            _object[name] = value;
            return this;
        }

        /// <summary>
        /// 添加字符串属性
        /// </summary>
        /// <param name="name">属性名</param>
        /// <param name="value">字符串值</param>
        /// <returns>构建器</returns>
        public ObjectBuilder AddString(string name, string? value)
        {
            _object[name] = value;
            return this;
        }

        /// <summary>
        /// 添加数字属性
        /// </summary>
        /// <param name="name">属性名</param>
        /// <param name="value">数字值</param>
        /// <returns>构建器</returns>
        public ObjectBuilder AddNumber(string name, double value)
        {
            _object[name] = value;
            return this;
        }

        /// <summary>
        /// 添加布尔属性
        /// </summary>
        /// <param name="name">属性名</param>
        /// <param name="value">布尔值</param>
        /// <returns>构建器</returns>
        public ObjectBuilder AddBoolean(string name, bool value)
        {
            _object[name] = value;
            return this;
        }

        /// <summary>
        /// 添加 null 属性
        /// </summary>
        /// <param name="name">属性名</param>
        /// <returns>构建器</returns>
        public ObjectBuilder AddNull(string name)
        {
            _object[name] = null;
            return this;
        }

        /// <summary>
        /// 添加对象属性
        /// </summary>
        /// <param name="name">属性名</param>
        /// <param name="builderAction">对象构建动作</param>
        /// <returns>构建器</returns>
        public ObjectBuilder AddObject(string name, Action<ObjectBuilder> builderAction)
        {
            var childBuilder = new ObjectBuilder();
            builderAction(childBuilder);
            _object[name] = childBuilder.Build();
            return this;
        }

        /// <summary>
        /// 添加数组属性
        /// </summary>
        /// <param name="name">属性名</param>
        /// <param name="builderAction">数组构建动作</param>
        /// <returns>构建器</returns>
        public ObjectBuilder AddArray(string name, Action<ArrayBuilder> builderAction)
        {
            var arrayBuilder = new ArrayBuilder();
            builderAction(arrayBuilder);
            _object[name] = arrayBuilder.Build();
            return this;
        }

        /// <summary>
        /// 构建对象
        /// </summary>
        /// <returns>动态 JSON 对象</returns>
        public DynamicJsonObject Build()
        {
            return _object;
        }
    }

    /// <summary>
    /// 数组构建器
    /// </summary>
    public class ArrayBuilder
    {
        private readonly DynamicJsonArray _array = [];

        /// <summary>
        /// 添加元素
        /// </summary>
        /// <param name="value">元素值</param>
        /// <returns>构建器</returns>
        public ArrayBuilder Add(object? value)
        {
            _array.Add(value);
            return this;
        }

        /// <summary>
        /// 添加字符串元素
        /// </summary>
        /// <param name="value">字符串值</param>
        /// <returns>构建器</returns>
        public ArrayBuilder AddString(string? value)
        {
            _array.Add(value);
            return this;
        }

        /// <summary>
        /// 添加数字元素
        /// </summary>
        /// <param name="value">数字值</param>
        /// <returns>构建器</returns>
        public ArrayBuilder AddNumber(double value)
        {
            _array.Add(value);
            return this;
        }

        /// <summary>
        /// 添加布尔元素
        /// </summary>
        /// <param name="value">布尔值</param>
        /// <returns>构建器</returns>
        public ArrayBuilder AddBoolean(bool value)
        {
            _array.Add(value);
            return this;
        }

        /// <summary>
        /// 添加 null 元素
        /// </summary>
        /// <returns>构建器</returns>
        public ArrayBuilder AddNull()
        {
            _array.Add(null);
            return this;
        }

        /// <summary>
        /// 添加对象元素
        /// </summary>
        /// <param name="builderAction">对象构建动作</param>
        /// <returns>构建器</returns>
        public ArrayBuilder AddObject(Action<ObjectBuilder> builderAction)
        {
            var objectBuilder = new ObjectBuilder();
            builderAction(objectBuilder);
            _array.Add(objectBuilder.Build());
            return this;
        }

        /// <summary>
        /// 添加数组元素
        /// </summary>
        /// <param name="builderAction">数组构建动作</param>
        /// <returns>构建器</returns>
        public ArrayBuilder AddArray(Action<ArrayBuilder> builderAction)
        {
            var arrayBuilder = new ArrayBuilder();
            builderAction(arrayBuilder);
            _array.Add(arrayBuilder.Build());
            return this;
        }

        /// <summary>
        /// 构建数组
        /// </summary>
        /// <returns>动态 JSON 数组</returns>
        public DynamicJsonArray Build()
        {
            return _array;
        }
    }

    #endregion 构建器模式
}
