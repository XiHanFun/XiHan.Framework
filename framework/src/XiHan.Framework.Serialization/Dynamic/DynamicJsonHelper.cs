#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicJsonHelper
// Guid:g1j4k9i8-fi3h-aj2g-ej7j-8h5i3g2h1j4f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/7 8:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Utils.Serialization.Json;

namespace XiHan.Framework.Serialization.Dynamic;

/// <summary>
/// 动态 JSON 操作帮助类
/// 提供动态 JSON 序列化、反序列化、节点操作、验证等功能
/// </summary>
public static class DynamicJsonHelper
{
    #region 序列化与反序列化

    /// <summary>
    /// 将动态 JSON 对象序列化为字符串
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <param name="indented">是否格式化输出</param>
    /// <returns>JSON 字符串</returns>
    /// <exception cref="ArgumentNullException">当对象为空时抛出</exception>
    public static string Serialize(object? dynamicJson, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(dynamicJson);

        return dynamicJson switch
        {
            DynamicJsonObject obj => obj.ToString(indented),
            DynamicJsonArray array => array.ToString(indented),
            DynamicJsonValue value => value.ToString(),
            DynamicJsonProperty property => property.ToString(),
            _ => JsonHelper.Serialize(dynamicJson, new JsonSerializeOptions { WriteIndented = indented })
        };
    }

    /// <summary>
    /// 异步将动态 JSON 对象序列化为字符串
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <param name="indented">是否格式化输出</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>JSON 字符串</returns>
    public static async Task<string> SerializeAsync(object? dynamicJson, bool indented = true, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Serialize(dynamicJson, indented), cancellationToken);
    }

    /// <summary>
    /// 从 JSON 字符串反序列化为动态 JSON 对象
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>动态 JSON 对象</returns>
    /// <exception cref="ArgumentException">当 JSON 字符串为空时抛出</exception>
    /// <exception cref="JsonException">当 JSON 格式无效时抛出</exception>
    public static dynamic? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON 字符串不能为空", nameof(json));
        }

        try
        {
            var node = JsonNode.Parse(json);
            return node switch
            {
                JsonObject jsonObject => new DynamicJsonObject(jsonObject),
                JsonArray jsonArray => new DynamicJsonArray(jsonArray),
                JsonValue jsonValue => new DynamicJsonValue(jsonValue),
                _ => null
            };
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new JsonException($"反序列化失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 异步从 JSON 字符串反序列化为动态 JSON 对象
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>动态 JSON 对象</returns>
    public static async Task<dynamic?> DeserializeAsync(string json, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Deserialize(json), cancellationToken);
    }

    /// <summary>
    /// 从 JSON 字符串反序列化为动态 JSON 对象
    /// </summary>
    /// <typeparam name="T">目标动态类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <returns>指定类型的动态 JSON 对象</returns>
    public static T? DeserializeAs<T>(string json) where T : class
    {
        var result = Deserialize(json);
        return result as T;
    }

    /// <summary>
    /// 从文件反序列化动态 JSON 对象
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>动态 JSON 对象</returns>
    public static dynamic? DeserializeFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在：{filePath}");
        }

        var json = File.ReadAllText(filePath, Encoding.UTF8);
        return Deserialize(json);
    }

    /// <summary>
    /// 异步从文件反序列化动态 JSON 对象
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>动态 JSON 对象</returns>
    public static async Task<dynamic?> DeserializeFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在：{filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return await DeserializeAsync(json, cancellationToken);
    }

    /// <summary>
    /// 将动态 JSON 对象序列化并保存到文件
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="indented">是否格式化输出</param>
    public static void SerializeToFile(object? dynamicJson, string filePath, bool indented = true)
    {
        var json = Serialize(dynamicJson, indented);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    /// <summary>
    /// 异步将动态 JSON 对象序列化并保存到文件
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="indented">是否格式化输出</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task SerializeToFileAsync(object? dynamicJson, string filePath, bool indented = true, CancellationToken cancellationToken = default)
    {
        var json = await SerializeAsync(dynamicJson, indented, cancellationToken);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, cancellationToken);
    }

    #region Try 方法

    /// <summary>
    /// 尝试将动态 JSON 对象序列化为字符串（不抛出异常）
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <param name="result">序列化结果，失败时为 null</param>
    /// <param name="indented">是否格式化输出</param>
    /// <returns>是否序列化成功</returns>
    public static bool TrySerialize(object? dynamicJson, out string? result, bool indented = true)
    {
        result = null;

        if (dynamicJson == null)
        {
            return false;
        }

        try
        {
            result = Serialize(dynamicJson, indented);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试从 JSON 字符串反序列化为动态 JSON 对象（不抛出异常）
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="result">反序列化结果，失败时为 null</param>
    /// <returns>是否反序列化成功</returns>
    public static bool TryDeserialize(string json, out dynamic? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            result = Deserialize(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试从 JSON 字符串反序列化为指定类型的动态 JSON 对象（不抛出异常）
    /// </summary>
    /// <typeparam name="T">目标动态类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="result">反序列化结果，失败时为 default(T)</param>
    /// <returns>是否反序列化成功</returns>
    public static bool TryDeserializeAs<T>(string json, out T? result) where T : class
    {
        result = default;

        if (TryDeserialize(json, out var dynamicResult))
        {
            result = dynamicResult as T;
            return result != null;
        }

        return false;
    }

    /// <summary>
    /// 尝试从文件反序列化动态 JSON 对象（不抛出异常）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="result">反序列化结果，失败时为 null</param>
    /// <returns>是否反序列化成功</returns>
    public static bool TryDeserializeFromFile(string filePath, out dynamic? result)
    {
        result = null;

        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            result = DeserializeFromFile(filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试将动态 JSON 对象序列化并保存到文件（不抛出异常）
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="indented">是否格式化输出</param>
    /// <returns>是否保存成功</returns>
    public static bool TrySerializeToFile(object? dynamicJson, string filePath, bool indented = true)
    {
        if (dynamicJson == null)
        {
            return false;
        }

        try
        {
            SerializeToFile(dynamicJson, filePath, indented);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion Try 方法

    #endregion 序列化与反序列化

    #region 类型转换

    /// <summary>
    /// 从普通对象创建动态 JSON 对象
    /// </summary>
    /// <param name="obj">普通对象</param>
    /// <returns>动态 JSON 对象</returns>
    public static dynamic? FromObject(object? obj)
    {
        if (obj == null)
        {
            return null;
        }

        var json = JsonHelper.Serialize(obj);
        return Deserialize(json);
    }

    /// <summary>
    /// 将动态 JSON 对象转换为普通对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <returns>普通对象</returns>
    public static T? ToObject<T>(object? dynamicJson)
    {
        if (dynamicJson == null)
        {
            return default;
        }

        var json = Serialize(dynamicJson, false);
        return JsonHelper.Deserialize<T>(json);
    }

    /// <summary>
    /// 尝试从普通对象创建动态 JSON 对象（不抛出异常）
    /// </summary>
    /// <param name="obj">普通对象</param>
    /// <param name="result">创建结果，失败时为 null</param>
    /// <returns>是否创建成功</returns>
    public static bool TryFromObject(object? obj, out dynamic? result)
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

    /// <summary>
    /// 尝试将动态 JSON 对象转换为普通对象（不抛出异常）
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <param name="result">转换结果，失败时为 default(T)</param>
    /// <returns>是否转换成功</returns>
    public static bool TryToObject<T>(object? dynamicJson, out T? result)
    {
        result = default;

        try
        {
            result = ToObject<T>(dynamicJson);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion 类型转换

    #region 验证功能

    /// <summary>
    /// 验证动态 JSON 对象是否有效
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <returns>是否有效</returns>
    public static bool IsValid(object? dynamicJson)
    {
        if (dynamicJson == null)
        {
            return false;
        }

        return TrySerialize(dynamicJson, out _);
    }

    /// <summary>
    /// 验证动态 JSON 对象是否为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <returns>是否为指定类型</returns>
    public static bool IsType<T>(object? dynamicJson) where T : class
    {
        return dynamicJson is T;
    }

    /// <summary>
    /// 验证动态 JSON 对象是否为空
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <returns>是否为空</returns>
    public static bool IsEmpty(object? dynamicJson)
    {
        return dynamicJson switch
        {
            null => true,
            DynamicJsonObject obj => obj.IsEmpty,
            DynamicJsonArray array => array.IsEmpty,
            DynamicJsonValue value => value.IsNull,
            _ => false
        };
    }

    /// <summary>
    /// 验证动态 JSON 对象是否包含指定属性
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <param name="propertyName">属性名</param>
    /// <returns>是否包含属性</returns>
    public static bool HasProperty(object? dynamicJson, string propertyName)
    {
        return dynamicJson switch
        {
            DynamicJsonObject obj => obj.ContainsKey(propertyName),
            _ => false
        };
    }

    #endregion 验证功能

    #region 查询和操作

    /// <summary>
    /// 通过路径查询动态 JSON 值
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <param name="path">属性路径（如 "user.name"）</param>
    /// <param name="separator">路径分隔符</param>
    /// <returns>查询到的值，未找到返回 null</returns>
    public static dynamic? SelectToken(object? dynamicJson, string path, char separator = '.')
    {
        if (dynamicJson is not DynamicJsonObject obj || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var parts = path.Split(separator);
        dynamic? current = obj;

        foreach (var part in parts)
        {
            if (current is DynamicJsonObject currentObj)
            {
                current = currentObj.GetValue(part);
            }
            else if (current is DynamicJsonArray currentArray && int.TryParse(part, out var index))
            {
                current = index >= 0 && index < currentArray.Count ? currentArray[index] : null;
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// 通过路径设置动态 JSON 值
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <param name="path">属性路径</param>
    /// <param name="value">要设置的值</param>
    /// <param name="separator">路径分隔符</param>
    /// <param name="createPath">是否创建不存在的路径</param>
    public static void SetToken(object? dynamicJson, string path, object? value, char separator = '.', bool createPath = true)
    {
        if (dynamicJson is not DynamicJsonObject obj || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var parts = path.Split(separator);
        var current = obj;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            var next = current.GetValue(part);

            if (next is not DynamicJsonObject nextObj)
            {
                if (createPath)
                {
                    nextObj = [];
                    current.SetValue(part, nextObj);
                }
                else
                {
                    return;
                }
            }

            current = nextObj;
        }

        current.SetValue(parts[^1], value);
    }

    /// <summary>
    /// 深度克隆动态 JSON 对象
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <returns>克隆的对象</returns>
    public static dynamic? DeepClone(object? dynamicJson)
    {
        if (dynamicJson == null)
        {
            return null;
        }

        var json = Serialize(dynamicJson, false);
        return Deserialize(json);
    }

    /// <summary>
    /// 合并两个动态 JSON 对象
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="source">源对象</param>
    /// <param name="overwrite">是否覆盖已存在的属性</param>
    /// <returns>合并后的对象</returns>
    public static DynamicJsonObject? Merge(DynamicJsonObject? target, DynamicJsonObject? source, bool overwrite = true)
    {
        if (target == null && source == null)
        {
            return null;
        }

        if (target == null)
        {
            return DeepClone(source) as DynamicJsonObject;
        }

        if (source == null)
        {
            return target;
        }

        var result = DeepClone(target) as DynamicJsonObject;
        if (result == null)
        {
            return target;
        }

        foreach (var property in source.Properties)
        {
            if (overwrite || !result.ContainsKey(property.Name))
            {
                if (property.Value is DynamicJsonObject sourceObj && result.GetValue(property.Name) is DynamicJsonObject targetObj)
                {
                    result.SetValue(property.Name, Merge(targetObj, sourceObj, overwrite));
                }
                else
                {
                    result.SetValue(property.Name, DeepClone(property.Value));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 比较两个动态 JSON 对象是否相等
    /// </summary>
    /// <param name="left">左侧对象</param>
    /// <param name="right">右侧对象</param>
    /// <returns>是否相等</returns>
    public static bool DeepEquals(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        // 如果类型不同，先序列化再比较
        if (left.GetType() != right.GetType())
        {
            if (TrySerialize(left, out var leftJson) && !string.IsNullOrEmpty(leftJson)
                && TrySerialize(right, out var rightJson) && !string.IsNullOrEmpty(rightJson))
            {
                return JsonHelper.CompareJson(leftJson!, rightJson!);
            }
            return false;
        }

        return left switch
        {
            DynamicJsonObject leftObj when right is DynamicJsonObject rightObj => leftObj.Equals(rightObj),
            DynamicJsonArray leftArray when right is DynamicJsonArray rightArray => leftArray.Equals(rightArray),
            DynamicJsonValue leftValue when right is DynamicJsonValue rightValue => leftValue.Equals(rightValue),
            DynamicJsonProperty leftProp when right is DynamicJsonProperty rightProp => leftProp.Equals(rightProp),
            _ => left.Equals(right)
        };
    }

    #endregion 查询和操作

    #region 辅助功能

    /// <summary>
    /// 格式化动态 JSON 对象
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <param name="indent">是否缩进</param>
    /// <returns>格式化后的字符串</returns>
    public static string Format(object? dynamicJson, bool indent = true)
    {
        if (dynamicJson == null)
        {
            return "null";
        }

        try
        {
            var json = Serialize(dynamicJson, false);
            return JsonHelper.FormatJson(json, indent);
        }
        catch
        {
            return dynamicJson.ToString() ?? "null";
        }
    }

    /// <summary>
    /// 压缩动态 JSON 对象（移除空白字符）
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <returns>压缩后的字符串</returns>
    public static string Compress(object? dynamicJson)
    {
        return Format(dynamicJson, false);
    }

    /// <summary>
    /// 获取动态 JSON 对象的类型信息
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <returns>类型信息</returns>
    public static string GetTypeInfo(object? dynamicJson)
    {
        return dynamicJson switch
        {
            null => "null",
            DynamicJsonObject => "DynamicJsonObject",
            DynamicJsonArray => "DynamicJsonArray",
            DynamicJsonValue value => $"DynamicJsonValue({value.ValueKind})",
            DynamicJsonProperty => "DynamicJsonProperty",
            _ => dynamicJson.GetType().Name
        };
    }

    /// <summary>
    /// 获取动态 JSON 对象的大小信息
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <returns>大小信息</returns>
    public static (int PropertyCount, int TotalSize) GetSizeInfo(object? dynamicJson)
    {
        return dynamicJson switch
        {
            DynamicJsonObject obj => (obj.Count, CalculateSize(obj)),
            DynamicJsonArray array => (array.Count, CalculateSize(array)),
            DynamicJsonValue value => (0, CalculateSize(value)),
            DynamicJsonProperty property => (1, CalculateSize(property)),
            _ => (0, 0)
        };
    }

    /// <summary>
    /// 将动态 JSON 对象转换为字典（扁平化）
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <param name="separator">分隔符</param>
    /// <returns>扁平化的字典</returns>
    public static Dictionary<string, object?> Flatten(object? dynamicJson, string separator = ".")
    {
        var result = new Dictionary<string, object?>();

        if (dynamicJson is DynamicJsonObject obj)
        {
            FlattenInternal(obj, string.Empty, result, separator);
        }

        return result;
    }

    /// <summary>
    /// 从扁平化字典构建动态 JSON 对象
    /// </summary>
    /// <param name="flattenedData">扁平化字典</param>
    /// <param name="separator">分隔符</param>
    /// <returns>动态 JSON 对象</returns>
    public static DynamicJsonObject? Unflatten(Dictionary<string, object?> flattenedData, string separator = ".")
    {
        if (flattenedData.Count == 0)
        {
            return [];
        }

        var result = new DynamicJsonObject();

        foreach (var kvp in flattenedData)
        {
            SetToken(result, kvp.Key, kvp.Value, separator[0]);
        }

        return result;
    }

    #endregion 辅助功能

    #region 私有辅助方法

    /// <summary>
    /// 计算动态 JSON 对象的大小
    /// </summary>
    /// <param name="dynamicJson">动态 JSON 对象</param>
    /// <returns>大小</returns>
    private static int CalculateSize(object? dynamicJson)
    {
        return dynamicJson switch
        {
            DynamicJsonObject obj => obj.Properties.Sum(p => CalculateSize(p)),
            DynamicJsonArray array => array.Count,
            DynamicJsonProperty property => property.Name.Length + CalculateSize(property.Value),
            DynamicJsonValue value => value.ToString().Length,
            string str => str.Length,
            _ => 1
        };
    }

    /// <summary>
    /// 内部扁平化方法
    /// </summary>
    /// <param name="obj">动态 JSON 对象</param>
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
            else if (property.Value is DynamicJsonArray array)
            {
                for (var i = 0; i < array.Count; i++)
                {
                    var arrayKey = $"{key}[{i}]";
                    if (array[i] is DynamicJsonObject arrayObj)
                    {
                        FlattenInternal(arrayObj, arrayKey, result, separator);
                    }
                    else
                    {
                        result[arrayKey] = array[i];
                    }
                }
            }
            else
            {
                result[key] = property.Value;
            }
        }
    }

    #endregion 私有辅助方法
}
