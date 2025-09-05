#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonExtensions
// Guid:e9i2h7g6-dg1f-8h0e-ch5h-6f3g1e0f9h2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/5 9:35:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json.Nodes;

namespace XiHan.Framework.Serialization.Dynamic;

/// <summary>
/// 动态 JSON 扩展方法
/// </summary>
public static class DynamicJsonExtensions
{
    #region 字符串扩展

    /// <summary>
    /// 将 JSON 字符串转换为动态 JSON 对象
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject ToDynamicJsonObject(this string json)
    {
        return DynamicJsonObject.Parse(json);
    }

    /// <summary>
    /// 尝试将 JSON 字符串转换为动态 JSON 对象
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryToDynamicJsonObject(this string json, out DynamicJsonObject? result)
    {
        return DynamicJsonObject.TryParse(json, out result);
    }

    /// <summary>
    /// 将 JSON 字符串转换为动态 JSON 数组
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>动态 JSON 数组</returns>
    public static DynamicJsonArray ToDynamicJsonArray(this string json)
    {
        return DynamicJsonArray.Parse(json);
    }

    /// <summary>
    /// 尝试将 JSON 字符串转换为动态 JSON 数组
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryToDynamicJsonArray(this string json, out DynamicJsonArray? result)
    {
        return DynamicJsonArray.TryParse(json, out result);
    }

    /// <summary>
    /// 将 JSON 字符串转换为动态 JSON（自动判断类型）
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>动态 JSON 对象</returns>
    public static dynamic? ToDynamicJson(this string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return node switch
            {
                JsonObject jsonObj => new DynamicJsonObject(jsonObj),
                JsonArray jsonArray => new DynamicJsonArray(jsonArray),
                JsonValue jsonValue => new DynamicJsonValue(jsonValue),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    #endregion 字符串扩展

    #region 对象扩展

    /// <summary>
    /// 将对象转换为动态 JSON 对象
    /// </summary>
    /// <param name="obj">源对象</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject ToDynamicJsonObject(this object obj)
    {
        return DynamicJsonObject.FromObject(obj);
    }

    /// <summary>
    /// 尝试将对象转换为动态 JSON 对象
    /// </summary>
    /// <param name="obj">源对象</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryToDynamicJsonObject(this object obj, out DynamicJsonObject? result)
    {
        return DynamicJsonObject.TryFromObject(obj, out result);
    }

    /// <summary>
    /// 将对象转换为动态 JSON 数组
    /// </summary>
    /// <param name="obj">源对象</param>
    /// <returns>动态 JSON 数组</returns>
    public static DynamicJsonArray ToDynamicJsonArray(this object obj)
    {
        return DynamicJsonArray.FromObject(obj);
    }

    /// <summary>
    /// 尝试将对象转换为动态 JSON 数组
    /// </summary>
    /// <param name="obj">源对象</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryToDynamicJsonArray(this object obj, out DynamicJsonArray? result)
    {
        return DynamicJsonArray.TryFromObject(obj, out result);
    }

    #endregion 对象扩展

    #region JsonNode 扩展

    /// <summary>
    /// 将 JsonNode 转换为动态 JSON
    /// </summary>
    /// <param name="node">JSON 节点</param>
    /// <returns>动态 JSON 对象</returns>
    public static dynamic? ToDynamic(this JsonNode? node)
    {
        return node switch
        {
            null => null,
            JsonObject jsonObj => new DynamicJsonObject(jsonObj),
            JsonArray jsonArray => new DynamicJsonArray(jsonArray),
            JsonValue jsonValue => new DynamicJsonValue(jsonValue),
            _ => null
        };
    }

    /// <summary>
    /// 将 JsonObject 转换为动态 JSON 对象
    /// </summary>
    /// <param name="jsonObject">JSON 对象</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject ToDynamic(this JsonObject jsonObject)
    {
        return new DynamicJsonObject(jsonObject);
    }

    /// <summary>
    /// 将 JsonArray 转换为动态 JSON 数组
    /// </summary>
    /// <param name="jsonArray">JSON 数组</param>
    /// <returns>动态 JSON 数组</returns>
    public static DynamicJsonArray ToDynamic(this JsonArray jsonArray)
    {
        return [.. jsonArray];
    }

    /// <summary>
    /// 将 JsonValue 转换为动态 JSON 值
    /// </summary>
    /// <param name="jsonValue">JSON 值</param>
    /// <returns>动态 JSON 值</returns>
    public static DynamicJsonValue ToDynamic(this JsonValue jsonValue)
    {
        return new DynamicJsonValue(jsonValue);
    }

    #endregion JsonNode 扩展

    #region 集合扩展

    /// <summary>
    /// 将字典转换为动态 JSON 对象
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject ToDynamicJsonObject(this IDictionary<string, object?> dictionary)
    {
        var obj = new DynamicJsonObject();
        foreach (var kvp in dictionary)
        {
            obj[kvp.Key] = kvp.Value;
        }
        return obj;
    }

    /// <summary>
    /// 将可枚举对象转换为动态 JSON 数组
    /// </summary>
    /// <param name="enumerable">可枚举对象</param>
    /// <returns>动态 JSON 数组</returns>
    public static DynamicJsonArray ToDynamicJsonArray(this IEnumerable<object?> enumerable)
    {
        return [.. enumerable];
    }

    /// <summary>
    /// 将动态 JSON 对象转换为字典
    /// </summary>
    /// <param name="dynamicObject">动态 JSON 对象</param>
    /// <returns>字典</returns>
    public static Dictionary<string, object?> ToDictionary(this DynamicJsonObject dynamicObject)
    {
        var result = new Dictionary<string, object?>();
        foreach (var property in dynamicObject.Properties)
        {
            result[property.Name] = property.Value;
        }
        return result;
    }

    /// <summary>
    /// 将动态 JSON 数组转换为列表
    /// </summary>
    /// <param name="dynamicArray">动态 JSON 数组</param>
    /// <returns>列表</returns>
    public static List<object?> ToList(this DynamicJsonArray dynamicArray)
    {
        var result = new List<object?>();
        foreach (var item in dynamicArray)
        {
            result.Add(item);
        }
        return result;
    }

    #endregion 集合扩展

    #region 查询扩展

    /// <summary>
    /// 使用路径查询动态 JSON 对象的值
    /// </summary>
    /// <param name="dynamicObject">动态 JSON 对象</param>
    /// <param name="path">路径（如 "user.profile.name"）</param>
    /// <param name="separator">路径分隔符</param>
    /// <returns>查询结果</returns>
    public static dynamic? SelectToken(this DynamicJsonObject dynamicObject, string path, char separator = '.')
    {
        if (string.IsNullOrEmpty(path))
        {
            return dynamicObject;
        }

        var parts = path.Split(separator);
        dynamic? current = dynamicObject;

        foreach (var part in parts)
        {
            if (current is DynamicJsonObject obj)
            {
                current = obj[part];
            }
            else if (current is DynamicJsonArray array && int.TryParse(part, out var index))
            {
                current = index < array.Count ? array[index] : null;
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// 使用路径设置动态 JSON 对象的值
    /// </summary>
    /// <param name="dynamicObject">动态 JSON 对象</param>
    /// <param name="path">路径（如 "user.profile.name"）</param>
    /// <param name="value">要设置的值</param>
    /// <param name="separator">路径分隔符</param>
    /// <param name="createPath">是否创建不存在的路径</param>
    public static void SetToken(this DynamicJsonObject dynamicObject, string path, object? value, char separator = '.', bool createPath = true)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var parts = path.Split(separator);
        dynamic current = dynamicObject;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];

            if (current is DynamicJsonObject obj)
            {
                if (!obj.ContainsKey(part))
                {
                    if (createPath)
                    {
                        obj[part] = new DynamicJsonObject();
                    }
                    else
                    {
                        return;
                    }
                }
                current = obj[part]!;
            }
            else
            {
                return;
            }
        }

        if (current is DynamicJsonObject finalObj)
        {
            finalObj[parts[^1]] = value;
        }
    }

    /// <summary>
    /// 深度合并两个动态 JSON 对象
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="source">源对象</param>
    /// <param name="overwrite">是否覆盖已存在的属性</param>
    /// <returns>合并后的对象</returns>
    public static DynamicJsonObject DeepMerge(this DynamicJsonObject target, DynamicJsonObject source, bool overwrite = true)
    {
        foreach (var property in source.Properties)
        {
            if (target.ContainsKey(property.Name))
            {
                if (overwrite)
                {
                    if (target[property.Name] is DynamicJsonObject targetObj && property.Value is DynamicJsonObject sourceObj)
                    {
                        targetObj.DeepMerge(sourceObj, overwrite);
                    }
                    else
                    {
                        target[property.Name] = property.Value;
                    }
                }
            }
            else
            {
                target[property.Name] = property.Value;
            }
        }

        return target;
    }

    /// <summary>
    /// 扁平化动态 JSON 对象
    /// </summary>
    /// <param name="dynamicObject">动态 JSON 对象</param>
    /// <param name="separator">分隔符</param>
    /// <returns>扁平化的字典</returns>
    public static Dictionary<string, object?> Flatten(this DynamicJsonObject dynamicObject, string separator = ".")
    {
        var result = new Dictionary<string, object?>();
        FlattenInternal(dynamicObject, "", result, separator);
        return result;
    }

    /// <summary>
    /// 内部扁平化方法
    /// </summary>
    /// <param name="obj">对象</param>
    /// <param name="prefix">前缀</param>
    /// <param name="result">结果字典</param>
    /// <param name="separator">分隔符</param>
    private static void FlattenInternal(DynamicJsonObject obj, string prefix, Dictionary<string, object?> result, string separator)
    {
        foreach (var property in obj.Properties)
        {
            var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}{separator}{property.Name}";

            if (property.Value is DynamicJsonObject nestedObj)
            {
                FlattenInternal(nestedObj, key, result, separator);
            }
            else
            {
                result[key] = property.Value;
            }
        }
    }

    #endregion 查询扩展

    #region 验证扩展

    /// <summary>
    /// 检查动态 JSON 对象是否包含指定路径
    /// </summary>
    /// <param name="dynamicObject">动态 JSON 对象</param>
    /// <param name="path">路径</param>
    /// <param name="separator">路径分隔符</param>
    /// <returns>是否包含</returns>
    public static bool HasPath(this DynamicJsonObject dynamicObject, string path, char separator = '.')
    {
        return dynamicObject.SelectToken(path, separator) != null;
    }

    /// <summary>
    /// 检查动态 JSON 对象是否为空（递归检查）
    /// </summary>
    /// <param name="dynamicObject">动态 JSON 对象</param>
    /// <returns>是否为空</returns>
    public static bool IsDeepEmpty(this DynamicJsonObject dynamicObject)
    {
        if (dynamicObject.IsEmpty)
        {
            return true;
        }

        foreach (var property in dynamicObject.Properties)
        {
            if (property.Value is DynamicJsonObject nestedObj)
            {
                if (!nestedObj.IsDeepEmpty())
                {
                    return false;
                }
            }
            else if (property.Value is DynamicJsonArray array)
            {
                if (!array.IsEmpty)
                {
                    return false;
                }
            }
            else if (property.Value != null)
            {
                return false;
            }
        }

        return true;
    }

    #endregion 验证扩展
}
