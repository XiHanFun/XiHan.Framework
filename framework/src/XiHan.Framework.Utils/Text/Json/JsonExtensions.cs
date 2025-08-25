#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonExtensions
// Guid:a1b2c3d4-e5f6-7890-abcd-ef1234567890
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2024-12-19 上午 11:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Utils.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Text.Json;

/// <summary>
/// JSON 扩展方法
/// </summary>
public static class JsonExtensions
{
    private static readonly JsonSerializerOptions DefaultOptions = JsonSerializerOptionsHelper.DefaultJsonSerializerOptions;

    #region 对象扩展

    /// <summary>
    /// 将对象转换为 JSON 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要转换的对象</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public static string ToJson<T>(this T obj, JsonSerializerOptions? options = null)
    {
        return JsonHelper.Serialize(obj, options);
    }

    /// <summary>
    /// 将对象转换为格式化的 JSON 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要转换的对象</param>
    /// <param name="indented">是否缩进</param>
    /// <returns>格式化的 JSON 字符串</returns>
    public static string ToFormattedJson<T>(this T obj, bool indented = true)
    {
        var json = JsonHelper.Serialize(obj);
        return JsonHelper.FormatJson(json, indented);
    }

    /// <summary>
    /// 深度克隆对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要克隆的对象</param>
    /// <returns>克隆后的对象</returns>
    public static T? Clone<T>(this T obj)
    {
        return JsonHelper.DeepClone(obj);
    }

    /// <summary>
    /// 将对象转换为指定类型
    /// </summary>
    /// <typeparam name="TSource">源类型</typeparam>
    /// <typeparam name="TTarget">目标类型</typeparam>
    /// <param name="obj">源对象</param>
    /// <returns>转换后的对象</returns>
    public static TTarget? ConvertTo<TSource, TTarget>(this TSource obj)
    {
        return obj == null ? default : JsonHelper.ConvertTo<TTarget>(obj);
    }

    #endregion 对象扩展

    #region 字符串扩展

    /// <summary>
    /// 将 JSON 字符串反序列化为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化后的对象</returns>
    public static T? FromJson<T>(this string json, JsonSerializerOptions? options = null)
    {
        return JsonHelper.Deserialize<T>(json, options);
    }

    /// <summary>
    /// 尝试将 JSON 字符串反序列化为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="result">反序列化结果</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>是否成功反序列化</returns>
    public static bool TryFromJson<T>(this string json, out T? result, JsonSerializerOptions? options = null)
    {
        return JsonHelper.TryDeserialize(json, out result, options);
    }

    /// <summary>
    /// 尝试将 JSON 字符串解析为动态对象
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="jsonObject">解析结果</param>
    /// <returns>是否解析成功</returns>
    public static bool TryParseJsonDynamic(this string json, out dynamic? jsonObject)
    {
        return JsonHelper.TryParseJsonDynamic(json, out jsonObject);
    }

    /// <summary>
    /// 验证字符串是否为有效的 JSON
    /// </summary>
    /// <param name="json">要验证的字符串</param>
    /// <returns>是否为有效 JSON</returns>
    public static bool IsValidJson(this string json)
    {
        return JsonHelper.IsValidJson(json);
    }

    /// <summary>
    /// 格式化 JSON 字符串
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="indented">是否缩进</param>
    /// <returns>格式化后的 JSON 字符串</returns>
    public static string FormatJson(this string json, bool indented = true)
    {
        return JsonHelper.FormatJson(json, indented);
    }

    /// <summary>
    /// 压缩 JSON 字符串
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>压缩后的 JSON 字符串</returns>
    public static string CompressJson(this string json)
    {
        return JsonHelper.CompressJson(json);
    }

    /// <summary>
    /// 将 JSON 字符串解析为 JsonNode
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>JsonNode 对象</returns>
    public static JsonNode? ToJsonNode(this string json)
    {
        return JsonHelper.ParseToJsonNode(json);
    }

    /// <summary>
    /// 将 JSON 字符串解析为 JsonDocument
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>JsonDocument 对象</returns>
    public static JsonDocument? ToJsonDocument(this string json)
    {
        return JsonHelper.ParseToJsonDocument(json);
    }

    /// <summary>
    /// 根据路径获取 JSON 值
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="path">JSON 路径</param>
    /// <returns>路径对应的值</returns>
    public static JsonNode? GetValueByPath(this string json, string path)
    {
        return JsonHelper.GetValueByPath(json, path);
    }

    /// <summary>
    /// 根据路径获取 JSON 值并转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="path">JSON 路径</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>转换后的值</returns>
    public static T? GetValueByPath<T>(this string json, string path, JsonSerializerOptions? options = null)
    {
        return JsonHelper.GetValueByPath<T>(json, path, options);
    }

    #endregion 字符串扩展

    #region JsonNode 扩展

    /// <summary>
    /// 根据路径获取值
    /// </summary>
    /// <param name="jsonNode">JsonNode 对象</param>
    /// <param name="path">JSON 路径</param>
    /// <returns>路径对应的值</returns>
    public static JsonNode? GetValueByPath(this JsonNode jsonNode, string path)
    {
        return JsonHelper.GetValueByPath(jsonNode, path);
    }

    /// <summary>
    /// 根据路径获取值并转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="jsonNode">JsonNode 对象</param>
    /// <param name="path">JSON 路径</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>转换后的值</returns>
    public static T? GetValueByPath<T>(this JsonNode jsonNode, string path, JsonSerializerOptions? options = null)
    {
        var node = JsonHelper.GetValueByPath(jsonNode, path);
        if (node == null)
        {
            return default;
        }

        try
        {
            options ??= DefaultOptions;
            return JsonSerializer.Deserialize<T>(node, options);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// 将 JsonNode 转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="jsonNode">JsonNode 对象</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>转换后的对象</returns>
    public static T? ToObject<T>(this JsonNode jsonNode, JsonSerializerOptions? options = null)
    {
        try
        {
            options ??= DefaultOptions;
            return JsonSerializer.Deserialize<T>(jsonNode, options);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// 尝试将 JsonNode 转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="jsonNode">JsonNode 对象</param>
    /// <param name="result">转换结果</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>是否成功转换</returns>
    public static bool TryToObject<T>(this JsonNode jsonNode, out T? result, JsonSerializerOptions? options = null)
    {
        result = default;
        try
        {
            options ??= DefaultOptions;
            result = JsonSerializer.Deserialize<T>(jsonNode, options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion JsonNode 扩展

    #region 集合扩展

    /// <summary>
    /// 将对象集合转换为 JSON 数组字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="collection">对象集合</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 数组字符串</returns>
    public static string ToJsonArray<T>(this IEnumerable<T> collection, JsonSerializerOptions? options = null)
    {
        return JsonHelper.Serialize(collection, options);
    }

    /// <summary>
    /// 将 JSON 数组字符串转换为对象集合
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="jsonArray">JSON 数组字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>对象集合</returns>
    public static IEnumerable<T>? FromJsonArray<T>(this string jsonArray, JsonSerializerOptions? options = null)
    {
        return JsonHelper.Deserialize<IEnumerable<T>>(jsonArray, options);
    }

    /// <summary>
    /// 将 JSON 数组字符串转换为对象列表
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="jsonArray">JSON 数组字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>对象列表</returns>
    public static List<T>? FromJsonArrayToList<T>(this string jsonArray, JsonSerializerOptions? options = null)
    {
        return JsonHelper.Deserialize<List<T>>(jsonArray, options);
    }

    #endregion 集合扩展

    #region 字典扩展

    /// <summary>
    /// 将字典转换为 JSON 对象字符串
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="dictionary">字典</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 对象字符串</returns>
    public static string ToJsonObject<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, JsonSerializerOptions? options = null) where TKey : notnull
    {
        return JsonHelper.Serialize(dictionary, options);
    }

    /// <summary>
    /// 将 JSON 对象字符串转换为字典
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="jsonObject">JSON 对象字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>字典</returns>
    public static Dictionary<TKey, TValue>? FromJsonObject<TKey, TValue>(this string jsonObject, JsonSerializerOptions? options = null) where TKey : notnull
    {
        return JsonHelper.Deserialize<Dictionary<TKey, TValue>>(jsonObject, options);
    }

    #endregion 字典扩展

    #region 文件扩展

    /// <summary>
    /// 将对象保存为 JSON 文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要保存的对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    /// <param name="createDirectory">是否自动创建目录</param>
    public static void SaveToJsonFile<T>(this T obj, string filePath, JsonSerializerOptions? options = null, bool createDirectory = true)
    {
        JsonFileHelper.WriteToFile(filePath, obj, options, createDirectory: createDirectory);
    }

    /// <summary>
    /// 异步将对象保存为 JSON 文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要保存的对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    /// <param name="createDirectory">是否自动创建目录</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task SaveToJsonFileAsync<T>(this T obj, string filePath, JsonSerializerOptions? options = null, bool createDirectory = true, CancellationToken cancellationToken = default)
    {
        await JsonFileHelper.WriteToFileAsync(filePath, obj, options, createDirectory: createDirectory, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 尝试将对象保存为 JSON 文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要保存的对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    /// <param name="createDirectory">是否自动创建目录</param>
    /// <returns>是否成功保存</returns>
    public static bool TrySaveToJsonFile<T>(this T obj, string filePath, JsonSerializerOptions? options = null, bool createDirectory = true)
    {
        return JsonFileHelper.TryWriteToFile(filePath, obj, options, createDirectory: createDirectory);
    }

    #endregion 文件扩展

    #region 流扩展

    /// <summary>
    /// 将对象序列化并写入流
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="stream">目标流</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task WriteToStreamAsync<T>(this T obj, Stream stream, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        await JsonHelper.SerializeAsync(stream, obj, options, cancellationToken);
    }

    /// <summary>
    /// 从流中读取并反序列化为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="stream">源流</param>
    /// <param name="options">反序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化后的对象</returns>
    public static async Task<T?> ReadFromStreamAsync<T>(this Stream stream, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await JsonHelper.DeserializeAsync<T>(stream, options, cancellationToken);
    }

    #endregion 流扩展
}
