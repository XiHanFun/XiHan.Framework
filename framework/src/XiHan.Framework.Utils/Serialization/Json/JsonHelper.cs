#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonHelper
// Guid:92776f67-afff-44e9-a428-2b15a53a412c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/8/29 5:43:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XiHan.Framework.Utils.Serialization.Json;

/// <summary>
/// JSON 操作帮助类
/// 提供 JSON 序列化、反序列化、节点操作、验证等功能
/// </summary>
public static class JsonHelper
{
    #region 序列化与反序列化

    /// <summary>
    /// 将对象序列化为 JSON 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    /// <exception cref="ArgumentNullException">当对象为空时抛出</exception>
    /// <exception cref="InvalidOperationException">当序列化失败时抛出</exception>
    public static string Serialize<T>(T obj, JsonSerializeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(obj);
        options ??= new JsonSerializeOptions();

        try
        {
            var systemOptions = options.ToSystemOptions();
            return JsonSerializer.Serialize(obj, systemOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"序列化失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 异步将对象序列化为 JSON 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>JSON 字符串</returns>
    public static async Task<string> SerializeAsync<T>(T obj, JsonSerializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Serialize(obj, options), cancellationToken);
    }

    /// <summary>
    /// 从 JSON 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    /// <exception cref="ArgumentException">当 JSON 字符串为空时抛出</exception>
    /// <exception cref="InvalidOperationException">当反序列化失败时抛出</exception>
    public static T Deserialize<T>(string json, JsonDeserializeOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON 字符串不能为空", nameof(json));
        }

        options ??= new JsonDeserializeOptions();

        try
        {
            if (options.ValidateJson && !IsValidJson(json))
            {
                throw new JsonException("无效的 JSON 格式");
            }

            var systemOptions = options.ToSystemOptions();
            var result = JsonSerializer.Deserialize<T>(json, systemOptions);

            return result ?? (options.ErrorHandling == JsonErrorHandling.UseDefault
                ? default!
                : throw new InvalidOperationException("反序列化失败：结果为空"));
        }
        catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
        {
            return options.ErrorHandling switch
            {
                JsonErrorHandling.UseDefault => default!,
                JsonErrorHandling.Ignore => default!,
                JsonErrorHandling.Log => default!, // 这里可以添加日志记录
                _ => throw new InvalidOperationException($"反序列化失败：{ex.Message}", ex)
            };
        }
    }

    /// <summary>
    /// 异步从 JSON 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的对象</returns>
    public static async Task<T> DeserializeAsync<T>(string json, JsonDeserializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Deserialize<T>(json, options), cancellationToken);
    }

    /// <summary>
    /// 从文件反序列化对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    public static T DeserializeFromFile<T>(string filePath, JsonDeserializeOptions? options = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在：{filePath}");
        }

        var json = File.ReadAllText(filePath, Encoding.UTF8);
        return Deserialize<T>(json, options);
    }

    /// <summary>
    /// 异步从文件反序列化对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">反序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的对象</returns>
    public static async Task<T> DeserializeFromFileAsync<T>(string filePath, JsonDeserializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在：{filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return await DeserializeAsync<T>(json, options, cancellationToken);
    }

    /// <summary>
    /// 将对象序列化并保存到文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    public static void SerializeToFile<T>(T obj, string filePath, JsonSerializeOptions? options = null)
    {
        var json = Serialize(obj, options);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(filePath, json, options?.Encoding ?? Encoding.UTF8);
    }

    /// <summary>
    /// 异步将对象序列化并保存到文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task SerializeToFileAsync<T>(T obj, string filePath, JsonSerializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        var json = await SerializeAsync(obj, options, cancellationToken);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(filePath, json, options?.Encoding ?? Encoding.UTF8, cancellationToken);
    }

    #endregion 序列化与反序列化

    #region JSON 节点操作

    /// <summary>
    /// 查询 JSON 节点值
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="jsonPath">JSON 路径表达式（如 "$.user.name"）</param>
    /// <returns>节点值，如果未找到则返回 null</returns>
    public static string? QueryNode(string json, string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(jsonPath))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(json);
            return QueryNodeRecursive(node, jsonPath.TrimStart('$', '.'));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 查询 JSON 节点集合
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="jsonPath">JSON 路径表达式</param>
    /// <returns>节点值列表</returns>
    public static List<string> QueryNodes(string json, string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(jsonPath))
        {
            return [];
        }

        try
        {
            var result = new List<string>();
            var node = JsonNode.Parse(json);
            QueryNodesRecursive(node, jsonPath.TrimStart('$', '.'), result);
            return result;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 设置 JSON 节点值
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="jsonPath">JSON 路径表达式</param>
    /// <param name="value">新值</param>
    /// <returns>修改后的 JSON 字符串</returns>
    /// <exception cref="InvalidOperationException">当找不到节点时抛出</exception>
    public static string SetNode(string json, string jsonPath, object value)
    {
        try
        {
            var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("无效的 JSON");
            SetNodeRecursive(node, jsonPath.TrimStart('$', '.'), value);
            return node.ToJsonString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"设置节点失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 添加 JSON 节点
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="parentPath">父节点路径</param>
    /// <param name="key">新节点键</param>
    /// <param name="value">新节点值</param>
    /// <returns>修改后的 JSON 字符串</returns>
    public static string AddNode(string json, string parentPath, string key, object value)
    {
        try
        {
            var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("无效的 JSON");
            var parentNode = GetNodeByPath(node, parentPath.TrimStart('$', '.'));

            if (parentNode is JsonObject jsonObject)
            {
                jsonObject[key] = JsonValue.Create(value);
            }
            else if (parentNode is JsonArray jsonArray)
            {
                jsonArray.Add(JsonValue.Create(value));
            }
            else
            {
                throw new InvalidOperationException($"父节点不是对象或数组：{parentPath}");
            }

            return node.ToJsonString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"添加节点失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 删除 JSON 节点
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="jsonPath">节点路径</param>
    /// <returns>修改后的 JSON 字符串</returns>
    public static string RemoveNode(string json, string jsonPath)
    {
        try
        {
            var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("无效的 JSON");
            RemoveNodeRecursive(node, jsonPath.TrimStart('$', '.'));
            return node.ToJsonString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"删除节点失败：{ex.Message}", ex);
        }
    }

    #endregion JSON 节点操作

    #region 验证功能

    /// <summary>
    /// 检查 JSON 是否有效
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="errorMessage">错误信息</param>
    /// <returns>是否有效</returns>
    public static bool IsValidJson(string json, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            errorMessage = "JSON 字符串为空";
            return false;
        }

        try
        {
            JsonDocument.Parse(json);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 检查 JSON 是否有效（简化版本）
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>是否有效</returns>
    public static bool IsValidJson(string json)
    {
        return IsValidJson(json, out _);
    }

    /// <summary>
    /// 验证 JSON 结构是否符合指定格式
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="expectedType">期望的根类型</param>
    /// <returns>是否符合</returns>
    public static bool ValidateStructure(string json, JsonValueKind expectedType)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == expectedType;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 验证 JSON 是否包含必需的属性
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="requiredProperties">必需的属性列表</param>
    /// <returns>验证结果和缺失的属性</returns>
    public static (bool IsValid, List<string> MissingProperties) ValidateRequiredProperties(string json, IEnumerable<string> requiredProperties)
    {
        var missingProperties = new List<string>();

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (false, requiredProperties.ToList());
            }

            foreach (var property in requiredProperties)
            {
                if (!document.RootElement.TryGetProperty(property, out _))
                {
                    missingProperties.Add(property);
                }
            }

            return (missingProperties.Count == 0, missingProperties);
        }
        catch
        {
            return (false, requiredProperties.ToList());
        }
    }

    #endregion 验证功能

    #region 辅助功能

    /// <summary>
    /// 格式化 JSON 字符串
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="indent">是否缩进</param>
    /// <returns>格式化后的 JSON 字符串</returns>
    public static string FormatJson(string json, bool indent = true)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var options = new JsonSerializerOptions { WriteIndented = indent };
            return JsonSerializer.Serialize(document.RootElement, options);
        }
        catch
        {
            return json; // 如果格式化失败，返回原始字符串
        }
    }

    /// <summary>
    /// 压缩 JSON 字符串（移除空白字符）
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>压缩后的 JSON 字符串</returns>
    public static string CompressJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var options = new JsonSerializerOptions { WriteIndented = false };
            return JsonSerializer.Serialize(document.RootElement, options);
        }
        catch
        {
            return json;
        }
    }

    /// <summary>
    /// 转换 JSON 为字典
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="separator">层级分隔符</param>
    /// <returns>扁平化的键值对字典</returns>
    public static Dictionary<string, string> JsonToDictionary(string json, string separator = ".")
    {
        var result = new Dictionary<string, string>();

        try
        {
            using var document = JsonDocument.Parse(json);
            FlattenJsonElement(document.RootElement, string.Empty, result, separator);
        }
        catch
        {
            // 解析失败时返回空字典
        }

        return result;
    }

    /// <summary>
    /// 从字典创建 JSON 字符串
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="options">序列化选项</param>
    /// <returns>JSON 字符串</returns>
    public static string DictionaryToJson(Dictionary<string, object> dictionary, JsonSerializeOptions? options = null)
    {
        return Serialize(dictionary, options);
    }

    /// <summary>
    /// 合并两个 JSON 字符串
    /// </summary>
    /// <param name="json1">第一个 JSON</param>
    /// <param name="json2">第二个 JSON</param>
    /// <param name="overwrite">是否覆盖重复键</param>
    /// <returns>合并后的 JSON 字符串</returns>
    public static string MergeJson(string json1, string json2, bool overwrite = true)
    {
        try
        {
            var dict1 = JsonToDictionary(json1);
            var dict2 = JsonToDictionary(json2);

            foreach (var kvp in dict2)
            {
                if (overwrite || !dict1.ContainsKey(kvp.Key))
                {
                    dict1[kvp.Key] = kvp.Value;
                }
            }

            // 重建嵌套结构
            var merged = BuildNestedStructure(dict1);
            return Serialize(merged);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"合并 JSON 失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 比较两个 JSON 字符串是否相等（结构化比较）
    /// </summary>
    /// <param name="json1">第一个 JSON</param>
    /// <param name="json2">第二个 JSON</param>
    /// <returns>是否相等</returns>
    public static bool CompareJson(string json1, string json2)
    {
        try
        {
            using var doc1 = JsonDocument.Parse(json1);
            using var doc2 = JsonDocument.Parse(json2);
            return JsonElementEquals(doc1.RootElement, doc2.RootElement);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 计算 JSON 的哈希值
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>哈希值</returns>
    public static string ComputeHash(string json)
    {
        try
        {
            // 先格式化为标准格式，再计算哈希
            var normalized = CompressJson(json);
            var bytes = Encoding.UTF8.GetBytes(normalized);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 克隆 JSON 对象（深拷贝）
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>克隆的 JSON 字符串</returns>
    public static string CloneJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement);
        }
        catch
        {
            return json;
        }
    }

    #endregion 辅助功能

    #region 私有辅助方法

    /// <summary>
    /// 递归查询节点
    /// </summary>
    private static string? QueryNodeRecursive(JsonNode? node, string path)
    {
        if (node == null || string.IsNullOrEmpty(path))
        {
            return node?.ToString();
        }

        var parts = path.Split('.', 2);
        var currentKey = parts[0];
        var remainingPath = parts.Length > 1 ? parts[1] : string.Empty;

        return node switch
        {
            JsonObject jsonObject when jsonObject.ContainsKey(currentKey) =>
                QueryNodeRecursive(jsonObject[currentKey], remainingPath),
            JsonArray jsonArray when int.TryParse(currentKey, out var index) && index < jsonArray.Count =>
                QueryNodeRecursive(jsonArray[index], remainingPath),
            _ => null
        };
    }

    /// <summary>
    /// 递归查询多个节点
    /// </summary>
    private static void QueryNodesRecursive(JsonNode? node, string path, List<string> result)
    {
        if (node == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(path))
        {
            result.Add(node.ToString());
            return;
        }

        var parts = path.Split('.', 2);
        var currentKey = parts[0];
        var remainingPath = parts.Length > 1 ? parts[1] : string.Empty;

        switch (node)
        {
            case JsonObject jsonObject:
                if (currentKey == "*")
                {
                    foreach (var prop in jsonObject)
                    {
                        QueryNodesRecursive(prop.Value, remainingPath, result);
                    }
                }
                else if (jsonObject.ContainsKey(currentKey))
                {
                    QueryNodesRecursive(jsonObject[currentKey], remainingPath, result);
                }
                break;

            case JsonArray jsonArray:
                if (currentKey == "*")
                {
                    foreach (var item in jsonArray)
                    {
                        QueryNodesRecursive(item, remainingPath, result);
                    }
                }
                else if (int.TryParse(currentKey, out var index) && index < jsonArray.Count)
                {
                    QueryNodesRecursive(jsonArray[index], remainingPath, result);
                }
                break;
        }
    }

    /// <summary>
    /// 递归设置节点值
    /// </summary>
    private static void SetNodeRecursive(JsonNode node, string path, object value)
    {
        var parts = path.Split('.', 2);
        var currentKey = parts[0];
        var remainingPath = parts.Length > 1 ? parts[1] : string.Empty;

        if (string.IsNullOrEmpty(remainingPath))
        {
            // 到达目标节点
            if (node is JsonObject jsonObject)
            {
                jsonObject[currentKey] = JsonValue.Create(value);
            }
            else if (node is JsonArray jsonArray && int.TryParse(currentKey, out var index))
            {
                if (index < jsonArray.Count)
                {
                    jsonArray[index] = JsonValue.Create(value);
                }
            }
        }
        else
        {
            // 继续递归
            JsonNode? childNode = null;
            if (node is JsonObject jsonObject && jsonObject.ContainsKey(currentKey))
            {
                childNode = jsonObject[currentKey];
            }
            else if (node is JsonArray jsonArray && int.TryParse(currentKey, out var index) && index < jsonArray.Count)
            {
                childNode = jsonArray[index];
            }

            if (childNode != null)
            {
                SetNodeRecursive(childNode, remainingPath, value);
            }
        }
    }

    /// <summary>
    /// 根据路径获取节点
    /// </summary>
    private static JsonNode? GetNodeByPath(JsonNode node, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return node;
        }

        var parts = path.Split('.', 2);
        var currentKey = parts[0];
        var remainingPath = parts.Length > 1 ? parts[1] : string.Empty;

        JsonNode? childNode = null;
        if (node is JsonObject jsonObject && jsonObject.ContainsKey(currentKey))
        {
            childNode = jsonObject[currentKey];
        }
        else if (node is JsonArray jsonArray && int.TryParse(currentKey, out var index) && index < jsonArray.Count)
        {
            childNode = jsonArray[index];
        }

        return childNode != null ? GetNodeByPath(childNode, remainingPath) : null;
    }

    /// <summary>
    /// 递归删除节点
    /// </summary>
    private static void RemoveNodeRecursive(JsonNode node, string path)
    {
        var parts = path.Split('.', 2);
        var currentKey = parts[0];
        var remainingPath = parts.Length > 1 ? parts[1] : string.Empty;

        if (string.IsNullOrEmpty(remainingPath))
        {
            // 删除目标节点
            if (node is JsonObject jsonObject)
            {
                jsonObject.Remove(currentKey);
            }
            else if (node is JsonArray jsonArray && int.TryParse(currentKey, out var index) && index < jsonArray.Count)
            {
                jsonArray.RemoveAt(index);
            }
        }
        else
        {
            // 继续递归
            JsonNode? childNode = null;
            if (node is JsonObject jsonObject && jsonObject.ContainsKey(currentKey))
            {
                childNode = jsonObject[currentKey];
            }
            else if (node is JsonArray jsonArray && int.TryParse(currentKey, out var index) && index < jsonArray.Count)
            {
                childNode = jsonArray[index];
            }

            if (childNode != null)
            {
                RemoveNodeRecursive(childNode, remainingPath);
            }
        }
    }

    /// <summary>
    /// 递归扁平化 JSON 元素
    /// </summary>
    private static void FlattenJsonElement(JsonElement element, string prefix, Dictionary<string, string> result, string separator)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}{separator}{property.Name}";
                    FlattenJsonElement(property.Value, key, result, separator);
                }
                break;

            case JsonValueKind.Array:
                for (var i = 0; i < element.GetArrayLength(); i++)
                {
                    var key = string.IsNullOrEmpty(prefix) ? i.ToString() : $"{prefix}{separator}{i}";
                    FlattenJsonElement(element[i], key, result, separator);
                }
                break;

            default:
                result[prefix] = element.ToString();
                break;
        }
    }

    /// <summary>
    /// 从扁平化字典重建嵌套结构
    /// </summary>
    private static object BuildNestedStructure(Dictionary<string, string> flatDict, string separator = ".")
    {
        var result = new Dictionary<string, object>();

        foreach (var kvp in flatDict)
        {
            var keys = kvp.Key.Split(separator);
            var current = result;

            for (var i = 0; i < keys.Length - 1; i++)
            {
                if (!current.ContainsKey(keys[i]))
                {
                    current[keys[i]] = new Dictionary<string, object>();
                }
                current = (Dictionary<string, object>)current[keys[i]];
            }

            current[keys[^1]] = kvp.Value;
        }

        return result;
    }

    /// <summary>
    /// 比较两个 JsonElement 是否相等
    /// </summary>
    private static bool JsonElementEquals(JsonElement element1, JsonElement element2)
    {
        if (element1.ValueKind != element2.ValueKind)
        {
            return false;
        }

        return element1.ValueKind switch
        {
            JsonValueKind.Object => CompareJsonObjects(element1, element2),
            JsonValueKind.Array => CompareJsonArrays(element1, element2),
            _ => element1.GetRawText() == element2.GetRawText()
        };
    }

    /// <summary>
    /// 比较两个 JSON 对象
    /// </summary>
    private static bool CompareJsonObjects(JsonElement obj1, JsonElement obj2)
    {
        var props1 = obj1.EnumerateObject().ToList();
        var props2 = obj2.EnumerateObject().ToList();

        if (props1.Count != props2.Count)
        {
            return false;
        }

        foreach (var prop1 in props1)
        {
            if (!obj2.TryGetProperty(prop1.Name, out var prop2Value) ||
                !JsonElementEquals(prop1.Value, prop2Value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 比较两个 JSON 数组
    /// </summary>
    private static bool CompareJsonArrays(JsonElement arr1, JsonElement arr2)
    {
        if (arr1.GetArrayLength() != arr2.GetArrayLength())
        {
            return false;
        }

        var items1 = arr1.EnumerateArray().ToList();
        var items2 = arr2.EnumerateArray().ToList();

        for (var i = 0; i < items1.Count; i++)
        {
            if (!JsonElementEquals(items1[i], items2[i]))
            {
                return false;
            }
        }

        return true;
    }

    #endregion 私有辅助方法
}
