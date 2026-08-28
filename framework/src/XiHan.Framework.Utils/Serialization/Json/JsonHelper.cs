// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
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
            // 预校验原来调用的是无参 IsValidJson，内部走默认 JsonDocumentOptions（不认尾随逗号、注释一律报错），
            // 而 ValidateJson 默认为 true，于是 Lenient / WebApi 预设上的 AllowTrailingCommas、ReadCommentHandling
            // 根本走不到真正的反序列化就被这道门拦掉，容错开关成了死开关。
            // 这里改成与 ToSystemOptions() 同源的文档解析选项，预校验与实际解析用同一把尺子。
            if (options.ValidateJson && !IsValidJson(json, ToDocumentOptions(options)))
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
    /// 从文件反序列化对象
    /// </summary>
    /// <remarks>
    /// 按 UTF-8 读取并保留 BOM 探测，所以 SerializeToFile 用带前导码的编码（UTF-8 / UTF-16 / UTF-32）
    /// 写出的文件都能自动识别读回。写入端若用了不带前导码的非 UTF-8 编码（如 Latin1、代码页编码），
    /// 必须改用显式传 encoding 的重载并传入与 JsonSerializeOptions.Encoding 相同的编码。
    /// </remarks>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    public static T DeserializeFromFile<T>(string filePath, JsonDeserializeOptions? options = null)
    {
        return DeserializeFromFile<T>(filePath, Encoding.UTF8, options);
    }

    /// <summary>
    /// 从文件反序列化对象（显式指定文件编码）
    /// </summary>
    /// <remarks>
    /// 补这个重载是因为读写两侧的编码来源原来不对称：SerializeToFile 用 JsonSerializeOptions.Encoding 落盘，
    /// 读取侧却硬编码 Encoding.UTF8，导致用不带前导码的非 UTF-8 编码写出的文件无法被同一套 API 读回。
    /// 调用方在这里传入与写入时相同的编码即可闭环。
    /// </remarks>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="encoding">读取文件使用的编码，应与写入时的编码一致</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    public static T DeserializeFromFile<T>(string filePath, Encoding encoding, JsonDeserializeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(encoding);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在：{filePath}");
        }

        var json = File.ReadAllText(filePath, encoding);
        return Deserialize<T>(json, options);
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

    #region Try 方法

    /// <summary>
    /// 尝试将对象序列化为 JSON 字符串（不抛出异常）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="result">序列化结果，失败时为 null</param>
    /// <param name="options">序列化选项</param>
    /// <returns>是否序列化成功</returns>
    public static bool TrySerialize<T>(T obj, out string? result, JsonSerializeOptions? options = null)
    {
        result = null;

        if (obj == null)
        {
            return false;
        }

        try
        {
            result = Serialize(obj, options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试从 JSON 字符串反序列化为对象（不抛出异常）
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="json">JSON 字符串</param>
    /// <param name="result">反序列化结果，失败时为 default(T)</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>是否反序列化成功</returns>
    public static bool TryDeserialize<T>(string json, out T? result, JsonDeserializeOptions? options = null)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            // 原来直接把调用方的 options 透传给 Deserialize：当 ErrorHandling 是 UseDefault/Ignore/Log 时，
            // Deserialize 会吞掉异常返回 default，于是这里对着一段非法 JSON 也会返回 true、out 参数为 null，
            // Try 语义被错误处理策略架空。改用"强制抛异常"的选项副本，让失败以异常形式冒出来再转成 false；
            // 复制而不是就地改写，是为了不污染调用方传进来的实例。
            result = Deserialize<T>(json, WithThrowOnError(options ?? new JsonDeserializeOptions()));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试从文件反序列化对象（不抛出异常）
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="result">反序列化结果，失败时为 default(T)</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>是否反序列化成功</returns>
    public static bool TryDeserializeFromFile<T>(string filePath, out T? result, JsonDeserializeOptions? options = null)
    {
        result = default;

        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            result = DeserializeFromFile<T>(filePath, options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试将对象序列化并保存到文件（不抛出异常）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    /// <returns>是否保存成功</returns>
    public static bool TrySerializeToFile<T>(T obj, string filePath, JsonSerializeOptions? options = null)
    {
        if (obj == null)
        {
            return false;
        }

        try
        {
            SerializeToFile(obj, filePath, options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion Try 方法

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
            // 不指定 Encoder 会走默认严格编码器，把中文等非 ASCII 字符转义成 \uXXXX，
            // 而同一个 Helper 的 Serialize 默认用 UnsafeRelaxedJsonEscaping 输出原样中文，两条路径对中文的处理不一致
            // （语义无损但文本差异明显，日志/配置比对场景会踩坑）。这里与 Serialize 的默认行为对齐。
            var options = new JsonSerializerOptions
            {
                WriteIndented = indent,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
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
            // 同 FormatJson：显式使用宽松编码器，避免压缩顺带把中文转义成 \uXXXX
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
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
    /// <remarks>
    /// 原实现先 JsonToDictionary 扁平化再 BuildNestedStructure 重建，结构与类型双双失真：
    /// 所有标量被降级为字符串（数字 1 变 "1"、布尔 true 变 "True"），数组被重建成以下标为键的对象
    /// （[a,b] 变 {"0":"a","1":"b"}），合并结果与两个输入的 JSON 都不同构。
    /// 现在改为在 JsonNode 层面递归合并，原始值类型与数组结构原样保留。
    /// </remarks>
    public static string MergeJson(string json1, string json2, bool overwrite = true)
    {
        try
        {
            var node1 = JsonNode.Parse(json1);
            var node2 = JsonNode.Parse(json2);

            var merged = MergeNode(node1, node2, overwrite);
            if (merged is null)
            {
                return "null";
            }

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
            // 同 FormatJson：显式使用宽松编码器，避免克隆顺带把中文转义成 \uXXXX
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(document.RootElement, options);
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
    /// 在 JsonNode 层面递归合并两个节点
    /// </summary>
    /// <remarks>
    /// 只有两侧同为对象时才逐键下钻；只要有一侧是数组、标量或缺失，就整体取胜方，
    /// 因为对象与数组之间不存在"逐键合并"的语义。
    /// 每次写入都用 DeepClone，避免把仍挂在原文档上的节点直接挂到新对象下（JsonNode 不允许一个节点有两个父级）。
    /// </remarks>
    /// <param name="target">基准节点（来自第一个 JSON）</param>
    /// <param name="source">要合并进来的节点（来自第二个 JSON）</param>
    /// <param name="overwrite">同名键冲突时是否用 source 覆盖 target</param>
    /// <returns>合并后的新节点</returns>
    private static JsonNode? MergeNode(JsonNode? target, JsonNode? source, bool overwrite)
    {
        if (target is not JsonObject targetObject || source is not JsonObject sourceObject)
        {
            return overwrite ? source?.DeepClone() : (target?.DeepClone() ?? source?.DeepClone());
        }

        var result = (JsonObject)targetObject.DeepClone();

        foreach (var property in sourceObject)
        {
            // 目标侧没有的键无条件补齐：overwrite 只决定"冲突时谁赢"，不决定"要不要合并新键"
            if (!result.TryGetPropertyValue(property.Key, out var existing))
            {
                result[property.Key] = property.Value?.DeepClone();
                continue;
            }

            result[property.Key] = MergeNode(existing, property.Value, overwrite);
        }

        return result;
    }

    /// <summary>
    /// 按反序列化选项构造同源的 JSON 文档解析选项
    /// </summary>
    /// <param name="options">反序列化选项</param>
    /// <returns>与 ToSystemOptions() 口径一致的文档解析选项</returns>
    private static JsonDocumentOptions ToDocumentOptions(JsonDeserializeOptions options)
    {
        return new JsonDocumentOptions
        {
            AllowTrailingCommas = options.AllowTrailingCommas,
            CommentHandling = options.ReadCommentHandling ? JsonCommentHandling.Skip : JsonCommentHandling.Disallow,
            MaxDepth = options.MaxDepth
        };
    }

    /// <summary>
    /// 按指定文档解析选项检查 JSON 是否有效
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="documentOptions">文档解析选项</param>
    /// <returns>是否有效</returns>
    private static bool IsValidJson(string json, JsonDocumentOptions documentOptions)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json, documentOptions);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 复制一份反序列化选项，并把错误处理策略强制为抛出异常
    /// </summary>
    /// <remarks>
    /// 供 Try 方法使用：Try 方法用返回值表达成败，不能再让 UseDefault/Ignore/Log 把失败吞成"成功且结果为 null"。
    /// 复制而不是就地改写，避免污染调用方持有的选项实例。
    /// </remarks>
    /// <param name="options">调用方给定的反序列化选项</param>
    /// <returns>错误处理策略为 ThrowException 的副本</returns>
    private static JsonDeserializeOptions WithThrowOnError(JsonDeserializeOptions options)
    {
        return new JsonDeserializeOptions
        {
            PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive,
            AllowTrailingCommas = options.AllowTrailingCommas,
            ReadCommentHandling = options.ReadCommentHandling,
            IgnoreUnknownProperties = options.IgnoreUnknownProperties,
            UseDefaultValues = options.UseDefaultValues,
            PropertyNamingPolicy = options.PropertyNamingPolicy,
            NumberHandling = options.NumberHandling,
            MaxDepth = options.MaxDepth,
            Encoder = options.Encoder,
            DefaultIgnoreCondition = options.DefaultIgnoreCondition,
            CustomConverters = options.CustomConverters,
            ValidateJson = options.ValidateJson,
            MaxStringLength = options.MaxStringLength,
            MaxArrayLength = options.MaxArrayLength,
            ErrorHandling = JsonErrorHandling.ThrowException
        };
    }

    /// <summary>
    /// 比较两个 JsonElement 是否相等
    /// </summary>
    /// <remarks>
    /// 字符串原来和其他标量一样走 GetRawText 比对，比的是"原文里的转义形式"而不是字符串的值：
    /// 同一个中文既可能以原样出现，也可能以严格编码器写出的 \uXXXX 形式出现
    /// （JsonNode.ToJsonString 没有指定 Encoder，RemoveNode / AddNode / UpdateNode 的返回值就是后者），
    /// 两者语义完全相同却被判为不等，与 CompareJson 承诺的"结构化比较"相悖
    /// （该方法已明确忽略属性顺序与空白，转义形式同属文本层差异，应当一并忽略）。
    /// 因此字符串改为比较解码后的值；数字、布尔与 null 继续按原文比对：
    /// 布尔与 null 的原文本身唯一，数字则只有原文能无损表达（转成 decimal/double 会引入精度与溢出问题）。
    /// </remarks>
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
            JsonValueKind.String => element1.ValueEquals(element2.GetString()),
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
