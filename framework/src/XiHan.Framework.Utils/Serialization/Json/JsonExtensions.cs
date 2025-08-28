#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonExtensions
// Guid:d1e5f623-be9a-4c4e-af8d-3f6e4d5a6b7c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/6 8:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json.Nodes;

namespace XiHan.Framework.Utils.Serialization.Json;

/// <summary>
/// JSON 扩展方法
/// </summary>
public static class JsonExtensions
{
    #region 对象扩展

    /// <summary>
    /// 将对象转换为 JSON 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要转换的对象</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public static string ToJson<T>(this T obj, JsonSerializeOptions? options = null)
    {
        return JsonHelper.Serialize(obj, options);
    }

    /// <summary>
    /// 异步将对象转换为 JSON 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要转换的对象</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>JSON 字符串</returns>
    public static async Task<string> ToJsonAsync<T>(this T obj, JsonSerializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => JsonHelper.Serialize(obj, options), cancellationToken);
    }

    #endregion 对象扩展

    #region 字符串扩展

    /// <summary>
    /// 从 JSON 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    public static T FromJson<T>(this string json, JsonDeserializeOptions? options = null)
    {
        return JsonHelper.Deserialize<T>(json, options);
    }

    /// <summary>
    /// 异步从 JSON 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的对象</returns>
    public static async Task<T> FromJsonAsync<T>(this string json, JsonDeserializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => JsonHelper.Deserialize<T>(json, options), cancellationToken);
    }

    /// <summary>
    /// 检查字符串是否为有效的 JSON
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>是否有效</returns>
    public static bool IsValidJson(this string json)
    {
        return JsonHelper.IsValidJson(json);
    }

    /// <summary>
    /// 检查字符串是否为有效的 JSON
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="errorMessage">错误信息</param>
    /// <returns>是否有效</returns>
    public static bool IsValidJson(this string json, out string? errorMessage)
    {
        return JsonHelper.IsValidJson(json, out errorMessage);
    }

    /// <summary>
    /// 格式化 JSON 字符串
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="indent">是否缩进</param>
    /// <returns>格式化后的 JSON 字符串</returns>
    public static string FormatJson(this string json, bool indent = true)
    {
        return JsonHelper.FormatJson(json, indent);
    }

    /// <summary>
    /// 压缩 JSON 字符串（移除空白字符）
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>压缩后的 JSON 字符串</returns>
    public static string CompressJson(this string json)
    {
        return JsonHelper.FormatJson(json, false);
    }

    /// <summary>
    /// 查询 JSON 节点值
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="jsonPath">JSON 路径表达式（如 "$.user.name"）</param>
    /// <returns>节点值，如果未找到则返回 null</returns>
    public static string? QueryNode(this string json, string jsonPath)
    {
        return JsonHelper.QueryNode(json, jsonPath);
    }

    /// <summary>
    /// 查询 JSON 节点集合
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="jsonPath">JSON 路径表达式</param>
    /// <returns>节点值列表</returns>
    public static List<string> QueryNodes(this string json, string jsonPath)
    {
        return JsonHelper.QueryNodes(json, jsonPath);
    }

    /// <summary>
    /// 转换 JSON 为字典
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="separator">层级分隔符</param>
    /// <returns>扁平化的键值对字典</returns>
    public static Dictionary<string, string> ToDictionary(this string json, string separator = ".")
    {
        return JsonHelper.JsonToDictionary(json, separator);
    }

    /// <summary>
    /// 尝试从 JSON 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="result">反序列化结果</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>是否成功反序列化</returns>
    public static bool TryFromJson<T>(this string json, out T? result, JsonDeserializeOptions? options = null)
    {
        result = default;

        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            result = JsonHelper.Deserialize<T>(json, options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion 字符串扩展

    #region 字典扩展

    /// <summary>
    /// 将字典转换为 JSON 字符串
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public static string ToJson(this Dictionary<string, object> dictionary, JsonSerializeOptions? options = null)
    {
        return JsonHelper.DictionaryToJson(dictionary, options);
    }

    /// <summary>
    /// 将字符串字典转换为 JSON 字符串
    /// </summary>
    /// <param name="dictionary">字符串字典</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public static string ToJson(this Dictionary<string, string> dictionary, JsonSerializeOptions? options = null)
    {
        return JsonHelper.Serialize(dictionary, options);
    }

    /// <summary>
    /// 从字典中获取值，如果不存在则返回默认值
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="key">键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>值或默认值</returns>
    public static string GetValueOrDefault(this Dictionary<string, string> dictionary, string key, string defaultValue = "")
    {
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// 从字典中获取嵌套键的值
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="nestedKey">嵌套键（如 "parent.child.grandchild"）</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="separator">分隔符</param>
    /// <returns>值或默认值</returns>
    public static string GetNestedValue(this Dictionary<string, string> dictionary, string nestedKey, string defaultValue = "", string separator = ".")
    {
        return dictionary.GetValueOrDefault(nestedKey, defaultValue);
    }

    /// <summary>
    /// 设置嵌套键的值
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="nestedKey">嵌套键</param>
    /// <param name="value">值</param>
    public static void SetNestedValue(this Dictionary<string, string> dictionary, string nestedKey, string value)
    {
        dictionary[nestedKey] = value;
    }

    /// <summary>
    /// 合并两个字典
    /// </summary>
    /// <param name="dictionary">目标字典</param>
    /// <param name="other">要合并的字典</param>
    /// <param name="overwrite">是否覆盖已存在的键</param>
    /// <returns>合并后的字典</returns>
    public static Dictionary<string, string> Merge(this Dictionary<string, string> dictionary, Dictionary<string, string> other, bool overwrite = true)
    {
        var result = new Dictionary<string, string>(dictionary);

        foreach (var kvp in other)
        {
            if (overwrite || !result.ContainsKey(kvp.Key))
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    #endregion 字典扩展

    #region JsonNode 扩展

    /// <summary>
    /// 安全获取 JsonNode 的字符串值
    /// </summary>
    /// <param name="node">JsonNode</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>字符串值或默认值</returns>
    public static string GetValueSafe(this JsonNode? node, string defaultValue = "")
    {
        return node?.ToString() ?? defaultValue;
    }

    /// <summary>
    /// 安全获取 JsonObject 的属性值
    /// </summary>
    /// <param name="jsonObject">JsonObject</param>
    /// <param name="propertyName">属性名</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>属性值或默认值</returns>
    public static string GetPropertySafe(this JsonObject? jsonObject, string propertyName, string defaultValue = "")
    {
        if (jsonObject?.ContainsKey(propertyName) == true)
        {
            return jsonObject[propertyName]?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// 安全获取 JsonArray 的元素值
    /// </summary>
    /// <param name="jsonArray">JsonArray</param>
    /// <param name="index">索引</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>元素值或默认值</returns>
    public static string GetElementSafe(this JsonArray? jsonArray, int index, string defaultValue = "")
    {
        if (jsonArray != null && index >= 0 && index < jsonArray.Count)
        {
            return jsonArray[index]?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    #endregion JsonNode 扩展

    #region 类型转换扩展

    /// <summary>
    /// 尝试将字符串值转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">字符串值</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryConvertTo<T>(this string value, out T? result)
    {
        result = default;

        try
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var targetType = typeof(T);
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(string))
            {
                result = (T)(object)value;
                return true;
            }

            if (underlyingType == typeof(bool))
            {
                if (bool.TryParse(value, out var boolValue))
                {
                    result = (T)(object)boolValue;
                    return true;
                }
                return false;
            }

            if (underlyingType == typeof(int))
            {
                if (int.TryParse(value, out var intValue))
                {
                    result = (T)(object)intValue;
                    return true;
                }
                return false;
            }

            if (underlyingType == typeof(double))
            {
                if (double.TryParse(value, out var doubleValue))
                {
                    result = (T)(object)doubleValue;
                    return true;
                }
                return false;
            }

            if (underlyingType == typeof(decimal))
            {
                if (decimal.TryParse(value, out var decimalValue))
                {
                    result = (T)(object)decimalValue;
                    return true;
                }
                return false;
            }

            if (underlyingType == typeof(DateTime))
            {
                if (DateTime.TryParse(value, out var dateTimeValue))
                {
                    result = (T)(object)dateTimeValue;
                    return true;
                }
                return false;
            }

            // 尝试使用 Convert.ChangeType
            result = (T)Convert.ChangeType(value, underlyingType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 将字符串值转换为指定类型，失败时返回默认值
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">字符串值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果或默认值</returns>
    public static T ConvertToOrDefault<T>(this string value, T defaultValue = default!)
    {
        return value.TryConvertTo<T>(out var result) ? result! : defaultValue;
    }

    #endregion 类型转换扩展
}
