#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonExtensions
// Guid:c8f45c21-d35f-4a29-84b7-28fd84218120
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/15 11:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Dynamic;
using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Utils.Serialization.Dynamic;

/// <summary>
/// 动态 JSON 扩展方法
/// </summary>
public static class DynamicJsonExtensions
{
    /// <summary>
    /// 转换为 dynamic 对象，支持直接属性访问
    /// 使用方式：var obj = jsonObj.AsDynamic(); var status = obj.status;
    /// </summary>
    /// <param name="jsonObj">动态 JSON 对象</param>
    /// <returns>动态对象</returns>
    public static dynamic AsDynamic(this DynamicJsonObject jsonObj)
    {
        return jsonObj;
    }

    /// <summary>
    /// 转换为 ExpandoObject，支持直接属性访问
    /// 使用方式：dynamic obj = jsonObj.AsExpando(); var status = obj.status;
    /// </summary>
    /// <param name="jsonObj">动态 JSON 对象</param>
    /// <returns>ExpandoObject</returns>
    public static ExpandoObject AsExpando(this DynamicJsonObject jsonObj)
    {
        return jsonObj;
    }

    /// <summary>
    /// 批量获取属性值
    /// 使用方式：var values = jsonObj.GetValues("prop1", "prop2", "prop3");
    /// </summary>
    /// <param name="jsonObj">动态 JSON 对象</param>
    /// <param name="propertyNames">属性名数组</param>
    /// <returns>属性值数组</returns>
    public static object?[] GetValues(this DynamicJsonObject jsonObj, params string[] propertyNames)
    {
        return [.. propertyNames.Select(name => jsonObj[name])];
    }

    /// <summary>
    /// 批量设置属性值
    /// 使用方式：jsonObj.SetValues(new { prop1 = "value1", prop2 = "value2" });
    /// </summary>
    /// <param name="jsonObj">动态 JSON 对象</param>
    /// <param name="values">属性值对象</param>
    /// <returns>当前对象（支持链式调用）</returns>
    public static DynamicJsonObject SetValues(this DynamicJsonObject jsonObj, object values)
    {
        var type = values.GetType();
        var properties = type.GetProperties();

        foreach (var property in properties)
        {
            var value = property.GetValue(values);
            jsonObj[property.Name] = value;
        }

        return jsonObj;
    }

    /// <summary>
    /// 检查是否包含所有指定的属性
    /// 使用方式：var hasAll = jsonObj.HasAll("prop1", "prop2", "prop3");
    /// </summary>
    /// <param name="jsonObj">动态 JSON 对象</param>
    /// <param name="propertyNames">属性名数组</param>
    /// <returns>是否包含所有属性</returns>
    public static bool HasAll(this DynamicJsonObject jsonObj, params string[] propertyNames)
    {
        return propertyNames.All(name => jsonObj.ContainsKey(name));
    }

    /// <summary>
    /// 检查是否包含任一指定的属性
    /// 使用方式：var hasAny = jsonObj.HasAny("prop1", "prop2", "prop3");
    /// </summary>
    /// <param name="jsonObj">动态 JSON 对象</param>
    /// <param name="propertyNames">属性名数组</param>
    /// <returns>是否包含任一属性</returns>
    public static bool HasAny(this DynamicJsonObject jsonObj, params string[] propertyNames)
    {
        return propertyNames.Any(name => jsonObj.ContainsKey(name));
    }

    /// <summary>
    /// 获取第一个非空属性值
    /// 使用方式：var value = jsonObj.GetFirstNonNull("prop1", "prop2", "prop3");
    /// </summary>
    /// <param name="jsonObj">动态 JSON 对象</param>
    /// <param name="propertyNames">属性名数组</param>
    /// <returns>第一个非空属性值</returns>
    public static object? GetFirstNonNull(this DynamicJsonObject jsonObj, params string[] propertyNames)
    {
        return propertyNames
            .Select(name => jsonObj[name])
            .FirstOrDefault(value => value != null);
    }

    /// <summary>
    /// 获取第一个非空属性值并转换为指定类型
    /// 使用方式：var value = jsonObj.GetFirstNonNull&lt;string&gt;("prop1", "prop2", "prop3");
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="jsonObj">动态 JSON 对象</param>
    /// <param name="propertyNames">属性名数组</param>
    /// <returns>第一个非空属性值</returns>
    public static T? GetFirstNonNull<T>(this DynamicJsonObject jsonObj, params string[] propertyNames)
    {
        var value = jsonObj.GetFirstNonNull(propertyNames);
        return value != null ? jsonObj.Get("temp").ToValue<T>() : default;
    }

    /// <summary>
    /// 安全获取嵌套属性值
    /// 使用方式：var value = jsonObj.SafeGet("user", "profile", "name");
    /// </summary>
    /// <param name="jsonObj">动态 JSON 对象</param>
    /// <param name="path">属性路径</param>
    /// <returns>属性值</returns>
    public static object? SafeGet(this DynamicJsonObject jsonObj, params string[] path)
    {
        var pathStr = string.Join(".", path);
        return jsonObj.SelectToken(pathStr);
    }

    /// <summary>
    /// 安全获取嵌套属性值并转换为指定类型
    /// 使用方式：var name = jsonObj.SafeGet&lt;string&gt;("user", "profile", "name");
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="jsonObj">动态 JSON 对象</param>
    /// <param name="path">属性路径</param>
    /// <returns>属性值</returns>
    public static T? SafeGet<T>(this DynamicJsonObject jsonObj, params string[] path)
    {
        var pathStr = string.Join(".", path);
        return jsonObj.SelectToken<T>(pathStr);
    }

    /// <summary>
    /// 将 DynamicJsonObject 转换为强类型对象
    /// 使用方式：var user = jsonObj.ToObject&lt;User&gt;();
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="jsonObj">动态 JSON 对象</param>
    /// <returns>强类型对象</returns>
    public static T? ToObject<T>(this DynamicJsonObject jsonObj)
    {
        return jsonObj.ToValue<T>();
    }

    /// <summary>
    /// 从强类型对象创建 DynamicJsonObject
    /// 使用方式：var jsonObj = user.ToDynamicJson();
    /// </summary>
    /// <param name="obj">强类型对象</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject ToDynamicJson(this object obj)
    {
        var json = JsonHelper.Serialize(obj);
        return JsonHelper.TryParseJsonDynamic(json, out var result) && result is DynamicJsonObject dynamicObj
            ? dynamicObj
            : [];
    }
}
