#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonFileHelper
// Guid:227522db-7512-4a80-972c-bbedb715da02
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-11-21 上午 01:06:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using XiHan.Framework.Utils.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Text.Json;

/// <summary>
/// JSON 文件操作帮助类
/// </summary>
public static class JsonFileHelper
{
    private static readonly JsonSerializerOptions _defaultOptions = JsonSerializerOptionsHelper.DefaultJsonSerializerOptions;

    #region 基础文件操作

    /// <summary>
    /// 从 JSON 文件读取并反序列化为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    /// <param name="encoding">文件编码</param>
    /// <returns>反序列化后的对象</returns>
    public static T? ReadFromFile<T>(string filePath, JsonSerializerOptions? options = null, Encoding? encoding = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            return default;
        }

        try
        {
            encoding ??= Encoding.UTF8;
            options ??= _defaultOptions;

            var jsonContent = File.ReadAllText(filePath, encoding);
            return JsonSerializer.Deserialize<T>(jsonContent, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"读取 JSON 文件失败: {filePath}", ex);
        }
    }

    /// <summary>
    /// 异步从 JSON 文件读取并反序列化为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    /// <param name="encoding">文件编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化后的对象</returns>
    public static async Task<T?> ReadFromFileAsync<T>(string filePath, JsonSerializerOptions? options = null, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            return default;
        }

        try
        {
            encoding ??= Encoding.UTF8;
            options ??= _defaultOptions;

            var jsonContent = await File.ReadAllTextAsync(filePath, encoding, cancellationToken);
            return JsonSerializer.Deserialize<T>(jsonContent, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"异步读取 JSON 文件失败: {filePath}", ex);
        }
    }

    /// <summary>
    /// 将对象序列化并写入 JSON 文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <param name="encoding">文件编码</param>
    /// <param name="createDirectory">是否自动创建目录</param>
    public static void WriteToFile<T>(string filePath, T obj, JsonSerializerOptions? options = null, Encoding? encoding = null, bool createDirectory = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        try
        {
            if (createDirectory)
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            encoding ??= Encoding.UTF8;
            options ??= _defaultOptions;

            var jsonContent = JsonSerializer.Serialize(obj, options);
            File.WriteAllText(filePath, jsonContent, encoding);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"写入 JSON 文件失败: {filePath}", ex);
        }
    }

    /// <summary>
    /// 异步将对象序列化并写入 JSON 文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <param name="encoding">文件编码</param>
    /// <param name="createDirectory">是否自动创建目录</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task WriteToFileAsync<T>(string filePath, T obj, JsonSerializerOptions? options = null, Encoding? encoding = null, bool createDirectory = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        try
        {
            if (createDirectory)
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            encoding ??= Encoding.UTF8;
            options ??= _defaultOptions;

            var jsonContent = JsonSerializer.Serialize(obj, options);
            await File.WriteAllTextAsync(filePath, jsonContent, encoding, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"异步写入 JSON 文件失败: {filePath}", ex);
        }
    }

    /// <summary>
    /// 尝试从 JSON 文件读取对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="result">读取结果</param>
    /// <param name="options">序列化选项</param>
    /// <param name="encoding">文件编码</param>
    /// <returns>是否成功读取</returns>
    public static bool TryReadFromFile<T>(string filePath, out T? result, JsonSerializerOptions? options = null, Encoding? encoding = null)
    {
        result = default;

        try
        {
            result = ReadFromFile<T>(filePath, options, encoding);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试将对象写入 JSON 文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <param name="encoding">文件编码</param>
    /// <param name="createDirectory">是否自动创建目录</param>
    /// <returns>是否成功写入</returns>
    public static bool TryWriteToFile<T>(string filePath, T obj, JsonSerializerOptions? options = null, Encoding? encoding = null, bool createDirectory = true)
    {
        try
        {
            WriteToFile(filePath, obj, options, encoding, createDirectory);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 流式操作

    /// <summary>
    /// 从流中读取 JSON 并反序列化
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="stream">输入流</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化后的对象</returns>
    public static async Task<T?> ReadFromStreamAsync<T>(Stream stream, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            options ??= _defaultOptions;
            return await JsonSerializer.DeserializeAsync<T>(stream, options, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("从流读取 JSON 失败", ex);
        }
    }

    /// <summary>
    /// 将对象序列化并写入流
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="stream">输出流</param>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task WriteToStreamAsync<T>(Stream stream, T obj, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            options ??= _defaultOptions;
            await JsonSerializer.SerializeAsync(stream, obj, options, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("向流写入 JSON 失败", ex);
        }
    }

    #endregion

    #region JSON 路径操作

    /// <summary>
    /// 从 JSON 文件中根据路径获取值
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="jsonPath">JSON 路径(如：$.user.name)</param>
    /// <param name="encoding">文件编码</param>
    /// <returns>路径对应的值</returns>
    public static JsonNode? GetValueFromFile(string filePath, string jsonPath, Encoding? encoding = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            encoding ??= Encoding.UTF8;
            var jsonContent = File.ReadAllText(filePath, encoding);
            return JsonHelper.GetValueByPath(jsonContent, jsonPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"从文件获取 JSON 路径值失败: {filePath}", ex);
        }
    }

    /// <summary>
    /// 向 JSON 文件中设置指定路径的值
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="jsonPath">JSON 路径</param>
    /// <param name="value">要设置的值</param>
    /// <param name="encoding">文件编码</param>
    /// <param name="createDirectory">是否自动创建目录</param>
    public static void SetValueToFile(string filePath, string jsonPath, JsonNode? value, Encoding? encoding = null, bool createDirectory = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        try
        {
            encoding ??= Encoding.UTF8;

            // 读取现有内容或创建新的 JSON 对象
            JsonNode? rootNode;
            if (File.Exists(filePath))
            {
                var jsonContent = File.ReadAllText(filePath, encoding);
                rootNode = JsonNode.Parse(jsonContent) ?? new JsonObject();
            }
            else
            {
                rootNode = new JsonObject();
            }

            // 设置值(这里需要实现路径设置逻辑)
            SetValueByPath(rootNode, jsonPath, value);

            // 写回文件
            if (createDirectory)
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            var updatedJson = rootNode.ToJsonString(_defaultOptions);
            File.WriteAllText(filePath, updatedJson, encoding);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"向文件设置 JSON 路径值失败: {filePath}", ex);
        }
    }

    /// <summary>
    /// 根据路径设置 JsonNode 的值
    /// </summary>
    /// <param name="rootNode">根节点</param>
    /// <param name="path">JSON 路径</param>
    /// <param name="value">要设置的值</param>
    private static void SetValueByPath(JsonNode rootNode, string path, JsonNode? value)
    {
        if (rootNode == null || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var segments = path.TrimStart('$', '.').Split('.');
        var current = rootNode;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];

            if (segment.Contains('[') && segment.Contains(']'))
            {
                // 处理数组索引
                var propertyName = segment[..segment.IndexOf('[')];
                var indexStr = segment.Substring(segment.IndexOf('[') + 1, segment.IndexOf(']') - segment.IndexOf('[') - 1);

                if (!string.IsNullOrEmpty(propertyName))
                {
                    if (current[propertyName] == null)
                    {
                        current[propertyName] = new JsonArray();
                    }
                    current = current[propertyName]!;
                }

                if (int.TryParse(indexStr, out var index) && current is JsonArray array)
                {
                    // 确保数组有足够的元素
                    while (array.Count <= index)
                    {
                        array.Add(new JsonObject());
                    }
                    current = array[index]!;
                }
            }
            else
            {
                // 处理普通属性
                if (current[segment] == null)
                {
                    current[segment] = new JsonObject();
                }
                current = current[segment]!;
            }
        }

        // 设置最终值
        var lastSegment = segments[^1];
        if (lastSegment.Contains('[') && lastSegment.Contains(']'))
        {
            var propertyName = lastSegment[..lastSegment.IndexOf('[')];
            var indexStr = lastSegment.Substring(lastSegment.IndexOf('[') + 1, lastSegment.IndexOf(']') - lastSegment.IndexOf('[') - 1);

            if (!string.IsNullOrEmpty(propertyName))
            {
                if (current[propertyName] == null)
                {
                    current[propertyName] = new JsonArray();
                }
                current = current[propertyName]!;
            }

            if (int.TryParse(indexStr, out var index) && current is JsonArray array)
            {
                while (array.Count <= index)
                {
                    array.Add((JsonNode?)null);
                }
                array[index] = value;
            }
        }
        else
        {
            current[lastSegment] = value;
        }
    }

    #endregion

    #region 配置文件操作

    /// <summary>
    /// 读取配置文件
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="configPath">配置文件路径</param>
    /// <param name="defaultConfig">默认配置(文件不存在时使用)</param>
    /// <param name="createIfNotExists">文件不存在时是否创建</param>
    /// <returns>配置对象</returns>
    public static T ReadConfig<T>(string configPath, T? defaultConfig = default, bool createIfNotExists = true) where T : new()
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("配置文件路径不能为空", nameof(configPath));
        }

        try
        {
            if (File.Exists(configPath))
            {
                return ReadFromFile<T>(configPath) ?? new T();
            }

            // 文件不存在时的处理
            var config = defaultConfig ?? new T();

            if (createIfNotExists)
            {
                WriteToFile(configPath, config);
            }

            return config;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"读取配置文件失败: {configPath}", ex);
        }
    }

    /// <summary>
    /// 保存配置文件
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="configPath">配置文件路径</param>
    /// <param name="config">配置对象</param>
    public static void SaveConfig<T>(string configPath, T config)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("配置文件路径不能为空", nameof(configPath));
        }

        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        try
        {
            WriteToFile(configPath, config);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"保存配置文件失败: {configPath}", ex);
        }
    }

    /// <summary>
    /// 更新配置文件中的特定值
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="configPath">配置文件路径</param>
    /// <param name="updateAction">更新操作</param>
    public static void UpdateConfig<T>(string configPath, Action<T> updateAction) where T : new()
    {
        ArgumentNullException.ThrowIfNull(updateAction);

        try
        {
            var config = ReadConfig<T>(configPath);
            updateAction(config);
            SaveConfig(configPath, config);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"更新配置文件失败: {configPath}", ex);
        }
    }

    #endregion

    #region 批量操作

    /// <summary>
    /// 批量读取 JSON 文件
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="filePaths">文件路径列表</param>
    /// <param name="options">序列化选项</param>
    /// <param name="encoding">文件编码</param>
    /// <returns>读取结果字典(文件路径 -> 对象)</returns>
    public static Dictionary<string, T?> BatchReadFromFiles<T>(IEnumerable<string> filePaths, JsonSerializerOptions? options = null, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var results = new Dictionary<string, T?>();

        foreach (var filePath in filePaths)
        {
            try
            {
                results[filePath] = ReadFromFile<T>(filePath, options, encoding);
            }
            catch
            {
                results[filePath] = default;
            }
        }

        return results;
    }

    /// <summary>
    /// 批量写入 JSON 文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="fileData">文件数据字典(文件路径 -> 对象)</param>
    /// <param name="options">序列化选项</param>
    /// <param name="encoding">文件编码</param>
    /// <param name="createDirectory">是否自动创建目录</param>
    /// <returns>写入结果字典(文件路径 -> 是否成功)</returns>
    public static Dictionary<string, bool> BatchWriteToFiles<T>(Dictionary<string, T> fileData, JsonSerializerOptions? options = null, Encoding? encoding = null, bool createDirectory = true)
    {
        ArgumentNullException.ThrowIfNull(fileData);

        var results = new Dictionary<string, bool>();

        foreach (var kvp in fileData)
        {
            try
            {
                WriteToFile(kvp.Key, kvp.Value, options, encoding, createDirectory);
                results[kvp.Key] = true;
            }
            catch
            {
                results[kvp.Key] = false;
            }
        }

        return results;
    }

    #endregion
}
