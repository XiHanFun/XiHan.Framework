#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonHelper
// Guid:227522db-7512-4a80-972c-bbedb715da02
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-11-21 上午 01:06:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Dynamic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Utils.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Text.Json;

/// <summary>
/// JSON 操作帮助类
/// </summary>
public static class JsonHelper
{
    private static readonly JsonSerializerOptions _cachedDefaultOptions = JsonSerializerOptionsHelper.DefaultJsonSerializerOptions;

    #region 序列化与反序列化

    /// <summary>
    /// 将对象序列化为 JSON 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public static string Serialize<T>(T obj, JsonSerializerOptions? options = null)
    {
        if (obj == null)
        {
            return "null";
        }

        options ??= _cachedDefaultOptions;
        return JsonSerializer.Serialize(obj, options);
    }

    /// <summary>
    /// 将对象序列化为 JSON 字符串（非泛型版本）
    /// </summary>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="type">对象类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public static string Serialize(object obj, Type type, JsonSerializerOptions? options = null)
    {
        if (obj == null)
        {
            return "null";
        }

        options ??= _cachedDefaultOptions;
        return JsonSerializer.Serialize(obj, type, options);
    }

    /// <summary>
    /// 将 JSON 字符串反序列化为指定类型的对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化后的对象</returns>
    public static T? Deserialize<T>(string json, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        options ??= _cachedDefaultOptions;
        return JsonSerializer.Deserialize<T>(json, options);
    }

    /// <summary>
    /// 将 JSON 字符串反序列化为指定类型的对象（非泛型版本）
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="type">目标类型</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化后的对象</returns>
    public static object? Deserialize(string json, Type type, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        options ??= _cachedDefaultOptions;
        return JsonSerializer.Deserialize(json, type, options);
    }

    /// <summary>
    /// 尝试将 JSON 字符串反序列化为指定类型的对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="result">反序列化结果</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>是否成功反序列化</returns>
    public static bool TryDeserialize<T>(string json, out T? result, JsonSerializerOptions? options = null)
    {
        result = default;

        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            options ??= _cachedDefaultOptions;
            result = JsonSerializer.Deserialize<T>(json, options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion 序列化与反序列化

    #region 动态 JSON 解析

    /// <summary>
    /// 尝试将 JSON 字符串解析为动态对象
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="jsonObject">解析结果</param>
    /// <returns>是否解析成功</returns>
    public static bool TryParseJsonDynamic(string json, out dynamic? jsonObject)
    {
        jsonObject = null;

        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var jsonNode = JsonNode.Parse(json);
            jsonObject = ConvertJsonNodeToDynamic(jsonNode);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 将 JSON 字符串解析为 JsonNode
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>JsonNode 对象</returns>
    public static JsonNode? ParseToJsonNode(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 将 JSON 字符串解析为 JsonDocument
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>JsonDocument 对象</returns>
    public static JsonDocument? ParseToJsonDocument(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 将 JsonNode 转换为动态对象
    /// </summary>
    /// <param name="node">JsonNode</param>
    /// <returns>动态对象</returns>
    private static dynamic? ConvertJsonNodeToDynamic(JsonNode? node)
    {
        return node switch
        {
            JsonObject jsonObject => ConvertJsonObjectToDynamic(jsonObject),
            JsonArray jsonArray => ConvertJsonArrayToDynamic(jsonArray),
            JsonValue jsonValue => ConvertJsonValueToDynamic(jsonValue),
            _ => null
        };
    }

    /// <summary>
    /// 将 JsonObject 转换为动态对象
    /// </summary>
    /// <param name="jsonObject">JsonObject</param>
    /// <returns>动态对象</returns>
    private static ExpandoObject ConvertJsonObjectToDynamic(JsonObject jsonObject)
    {
        var expandoObject = new ExpandoObject();
        var dictionary = (IDictionary<string, object?>)expandoObject;

        foreach (var kvp in jsonObject)
        {
            dictionary[kvp.Key] = ConvertJsonNodeToDynamic(kvp.Value);
        }

        return expandoObject;
    }

    /// <summary>
    /// 将 JsonArray 转换为动态数组
    /// </summary>
    /// <param name="jsonArray">JsonArray</param>
    /// <returns>动态数组</returns>
    private static object[] ConvertJsonArrayToDynamic(JsonArray jsonArray)
    {
        var array = new object[jsonArray.Count];
        for (var i = 0; i < jsonArray.Count; i++)
        {
            array[i] = ConvertJsonNodeToDynamic(jsonArray[i])!;
        }
        return array;
    }

    /// <summary>
    /// 将 JsonValue 转换为对应的 .NET 类型
    /// </summary>
    /// <param name="jsonValue">JsonValue</param>
    /// <returns>对应的 .NET 类型值</returns>
    private static object? ConvertJsonValueToDynamic(JsonValue jsonValue)
    {
        if (jsonValue.TryGetValue<bool>(out var boolValue))
        {
            return boolValue;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            return intValue;
        }

        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            return longValue;
        }

        if (jsonValue.TryGetValue<double>(out var doubleValue))
        {
            return doubleValue;
        }

        if (jsonValue.TryGetValue<decimal>(out var decimalValue))
        {
            return decimalValue;
        }

        if (jsonValue.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        if (jsonValue.TryGetValue<DateTime>(out var dateTimeValue))
        {
            return dateTimeValue;
        }

        return jsonValue.ToString();
    }

    #endregion 动态 JSON 解析

    #region JSON 验证与格式化

    /// <summary>
    /// 验证 JSON 字符串是否有效
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>是否有效</returns>
    public static bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 格式化 JSON 字符串（美化输出）
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="indented">是否缩进</param>
    /// <returns>格式化后的 JSON 字符串</returns>
    public static string FormatJson(string json, bool indented = true)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var options = _cachedDefaultOptions;
            options.WriteIndented = indented;
            return JsonSerializer.Serialize(document.RootElement, options);
        }
        catch
        {
            return json;
        }
    }

    /// <summary>
    /// 压缩 JSON 字符串（移除空白字符）
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>压缩后的 JSON 字符串</returns>
    public static string CompressJson(string json)
    {
        return FormatJson(json, false);
    }

    #endregion JSON 验证与格式化

    #region JSON 路径操作

    /// <summary>
    /// 根据 JSON 路径获取值
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="path">JSON 路径（如：$.user.name）</param>
    /// <returns>路径对应的值</returns>
    public static JsonNode? GetValueByPath(string json, string path)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var jsonNode = JsonNode.Parse(json);
            return GetValueByPath(jsonNode, path);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 根据 JSON 路径获取值
    /// </summary>
    /// <param name="jsonNode">JsonNode 对象</param>
    /// <param name="path">JSON 路径</param>
    /// <returns>路径对应的值</returns>
    public static JsonNode? GetValueByPath(JsonNode? jsonNode, string path)
    {
        if (jsonNode == null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            // 简单的路径解析实现（支持 $.property 和 $.array[index] 格式）
            var segments = path.TrimStart('$', '.').Split('.');
            var current = jsonNode;

            foreach (var segment in segments)
            {
                if (current == null)
                {
                    return null;
                }

                if (segment.Contains('[') && segment.Contains(']'))
                {
                    // 处理数组索引
                    var propertyName = segment[..segment.IndexOf('[')];
                    var indexStr = segment.Substring(segment.IndexOf('[') + 1, segment.IndexOf(']') - segment.IndexOf('[') - 1);

                    if (!string.IsNullOrEmpty(propertyName))
                    {
                        current = current[propertyName];
                    }

                    if (int.TryParse(indexStr, out var index) && current is JsonArray array)
                    {
                        current = index < array.Count ? array[index] : null;
                    }
                }
                else
                {
                    // 处理普通属性
                    current = current[segment];
                }
            }

            return current;
        }
        catch
        {
            return null;
        }
    }

    #endregion JSON 路径操作

    #region JSON 合并与比较

    /// <summary>
    /// 合并两个 JSON 对象
    /// </summary>
    /// <param name="json1">第一个 JSON 字符串</param>
    /// <param name="json2">第二个 JSON 字符串</param>
    /// <param name="overwriteExisting">是否覆盖已存在的属性</param>
    /// <returns>合并后的 JSON 字符串</returns>
    public static string MergeJson(string json1, string json2, bool overwriteExisting = true)
    {
        try
        {
            var node1 = JsonNode.Parse(json1);
            var node2 = JsonNode.Parse(json2);

            if (node1 is JsonObject obj1 && node2 is JsonObject obj2)
            {
                var merged = MergeJsonObjects(obj1, obj2, overwriteExisting);
                return merged.ToJsonString();
            }

            return overwriteExisting ? json2 : json1;
        }
        catch
        {
            return json1;
        }
    }

    /// <summary>
    /// 比较两个 JSON 字符串是否相等
    /// </summary>
    /// <param name="json1">第一个 JSON 字符串</param>
    /// <param name="json2">第二个 JSON 字符串</param>
    /// <param name="ignoreOrder">是否忽略属性顺序</param>
    /// <returns>是否相等</returns>
    public static bool JsonEquals(string json1, string json2, bool ignoreOrder = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json1) && string.IsNullOrWhiteSpace(json2))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(json1) || string.IsNullOrWhiteSpace(json2))
            {
                return false;
            }

            if (ignoreOrder)
            {
                // 通过序列化和反序列化来标准化 JSON 格式
                var normalized1 = FormatJson(json1, false);
                var normalized2 = FormatJson(json2, false);
                return normalized1 == normalized2;
            }
            else
            {
                return json1.Trim() == json2.Trim();
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 合并两个 JsonObject
    /// </summary>
    /// <param name="obj1">第一个 JsonObject</param>
    /// <param name="obj2">第二个 JsonObject</param>
    /// <param name="overwriteExisting">是否覆盖已存在的属性</param>
    /// <returns>合并后的 JsonObject</returns>
    private static JsonObject MergeJsonObjects(JsonObject obj1, JsonObject obj2, bool overwriteExisting)
    {
        var result = new JsonObject();

        // 复制第一个对象的所有属性
        foreach (var kvp in obj1)
        {
            result[kvp.Key] = kvp.Value?.DeepClone();
        }

        // 合并第二个对象的属性
        foreach (var kvp in obj2)
        {
            if (result.ContainsKey(kvp.Key))
            {
                if (overwriteExisting)
                {
                    result[kvp.Key] = result[kvp.Key] is JsonObject existingObj && kvp.Value is JsonObject newObj
                        ? MergeJsonObjects(existingObj, newObj, overwriteExisting)
                        : (kvp.Value?.DeepClone());
                }
            }
            else
            {
                result[kvp.Key] = kvp.Value?.DeepClone();
            }
        }

        return result;
    }

    #endregion JSON 合并与比较

    #region 类型转换辅助

    /// <summary>
    /// 将对象转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="obj">源对象</param>
    /// <returns>转换后的对象</returns>
    public static T? ConvertTo<T>(object obj)
    {
        if (obj == null)
        {
            return default;
        }

        try
        {
            var json = Serialize(obj);
            return Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// 深度克隆对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要克隆的对象</param>
    /// <returns>克隆后的对象</returns>
    public static T? DeepClone<T>(T obj)
    {
        if (obj == null)
        {
            return default;
        }

        try
        {
            var json = Serialize(obj);
            return Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }

    #endregion 类型转换辅助

    #region 异步操作

    /// <summary>
    /// 异步序列化对象到流
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="stream">目标流</param>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public static async Task SerializeAsync<T>(Stream stream, T obj, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= _cachedDefaultOptions;
        await JsonSerializer.SerializeAsync(stream, obj, options, cancellationToken);
    }

    /// <summary>
    /// 异步从流反序列化对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="stream">源流</param>
    /// <param name="options">反序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化后的对象</returns>
    public static async Task<T?> DeserializeAsync<T>(Stream stream, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= _cachedDefaultOptions;
        return await JsonSerializer.DeserializeAsync<T>(stream, options, cancellationToken);
    }

    #endregion 异步操作
}
