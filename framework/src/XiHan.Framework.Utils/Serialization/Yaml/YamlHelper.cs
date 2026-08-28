// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Utils.Serialization.Yaml;

/// <summary>
/// YAML 操作帮助类
/// 提供 YAML 解析、序列化、反序列化等功能
/// </summary>
public static partial class YamlHelper
{
    // ParseNestedYaml 使用默认解析选项时的层级分隔符，格式化侧要按同一个分隔符拆键
    private const string DefaultKeySeparator = ".";

    private static readonly string[] Separator = ["\r\n", "\n"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    #region 对象序列化与反序列化

    /// <summary>
    /// 将对象序列化为 YAML 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项</param>
    /// <returns>YAML 字符串</returns>
    /// <exception cref="ArgumentNullException">当对象为空时抛出</exception>
    public static string Serialize<T>(T obj, YamlSerializeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(obj);

        options ??= new YamlSerializeOptions();

        try
        {
            // 先转换为 JSON，再转换为 YAML
            var json = JsonSerializer.Serialize(obj, JsonOptions);
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

            return ConvertJsonElementToYaml(jsonElement, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"序列化失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 从 YAML 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    /// <exception cref="ArgumentException">当 YAML 字符串为空时抛出</exception>
    /// <exception cref="InvalidOperationException">当反序列化失败时抛出</exception>
    public static T Deserialize<T>(string yaml, YamlDeserializeOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new ArgumentException("YAML 字符串不能为空", nameof(yaml));
        }

        options ??= new YamlDeserializeOptions();

        try
        {
            // 先转换为 JSON，再反序列化
            var json = ConvertYamlToJson(yaml, options);
            var result = JsonSerializer.Deserialize<T>(json, JsonOptions);

            return result ?? throw new InvalidOperationException("反序列化失败：结果为空");
        }
        catch (Exception ex) when (ex is not (ArgumentException or InvalidOperationException))
        {
            throw new InvalidOperationException($"反序列化失败：{ex.Message}", ex);
        }
    }

    #endregion 对象序列化与反序列化

    #region 文件操作

    /// <summary>
    /// 从 YAML 文件加载字典
    /// </summary>
    /// <param name="filePath">YAML 文件路径</param>
    /// <param name="options">解析选项</param>
    /// <returns>键值对字典</returns>
    public static Dictionary<string, string> LoadFromFile(string filePath, YamlParseOptions? options = null)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var yaml = File.ReadAllText(filePath, Encoding.UTF8);
        return ParseYaml(yaml, options);
    }

    /// <summary>
    /// 保存字典到 YAML 文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">要保存的数据</param>
    /// <param name="options">序列化选项</param>
    public static void SaveToFile(string filePath, Dictionary<string, string> data, YamlSerializeOptions? options = null)
    {
        var yaml = ConvertToYaml(data, options);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(filePath, yaml, Encoding.UTF8);
    }

    /// <summary>
    /// 从 YAML 文件反序列化对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    public static T DeserializeFromFile<T>(string filePath, YamlDeserializeOptions? options = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在：{filePath}");
        }

        var yaml = File.ReadAllText(filePath, Encoding.UTF8);
        return Deserialize<T>(yaml, options);
    }

    /// <summary>
    /// 将对象序列化并保存到 YAML 文件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    public static void SerializeToFile<T>(T obj, string filePath, YamlSerializeOptions? options = null)
    {
        var yaml = Serialize(obj, options);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(filePath, yaml, Encoding.UTF8);
    }

    #region Try 方法

    /// <summary>
    /// 尝试将对象序列化为 YAML 字符串（不抛出异常）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="result">序列化结果，失败时为 null</param>
    /// <param name="options">序列化选项</param>
    /// <returns>是否序列化成功</returns>
    public static bool TrySerialize<T>(T obj, out string? result, YamlSerializeOptions? options = null)
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
    /// 尝试从 YAML 字符串反序列化为对象（不抛出异常）
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="result">反序列化结果，失败时为 default(T)</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>是否反序列化成功</returns>
    public static bool TryDeserialize<T>(string yaml, out T? result, YamlDeserializeOptions? options = null)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(yaml))
        {
            return false;
        }

        try
        {
            result = Deserialize<T>(yaml, options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试从 YAML 文件反序列化对象（不抛出异常）
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="filePath">文件路径</param>
    /// <param name="result">反序列化结果，失败时为 default(T)</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>是否反序列化成功</returns>
    public static bool TryDeserializeFromFile<T>(string filePath, out T? result, YamlDeserializeOptions? options = null)
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
    /// 尝试将对象序列化并保存到 YAML 文件（不抛出异常）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="options">序列化选项</param>
    /// <returns>是否保存成功</returns>
    public static bool TrySerializeToFile<T>(T obj, string filePath, YamlSerializeOptions? options = null)
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

    /// <summary>
    /// 尝试从 YAML 文件加载字典（不抛出异常）
    /// </summary>
    /// <param name="filePath">YAML 文件路径</param>
    /// <param name="result">加载结果，失败时为空字典</param>
    /// <param name="options">解析选项</param>
    /// <returns>是否加载成功</returns>
    public static bool TryLoadFromFile(string filePath, out Dictionary<string, string> result, YamlParseOptions? options = null)
    {
        result = [];

        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            result = LoadFromFile(filePath, options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试保存字典到 YAML 文件（不抛出异常）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="data">要保存的数据</param>
    /// <param name="options">序列化选项</param>
    /// <returns>是否保存成功</returns>
    public static bool TrySaveToFile(string filePath, Dictionary<string, string> data, YamlSerializeOptions? options = null)
    {
        try
        {
            SaveToFile(filePath, data, options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion Try 方法

    #endregion 文件操作

    #region 字典操作

    /// <summary>
    /// 解析 YAML 字符串为字典
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">解析选项</param>
    /// <returns>键值对字典</returns>
    /// <remarks>
    /// 只做一层键值对：既不还原缩进层级，也不还原序列（短横线列表会被跳过）。
    /// 需要层级或集合请改用 <see cref="ParseNestedYaml(string, YamlParseOptions)"/> 或 <see cref="Deserialize{T}"/>。
    /// </remarks>
    public static Dictionary<string, string> ParseYaml(string yaml, YamlParseOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return [];
        }

        options ??= new YamlParseOptions();
        var result = new Dictionary<string, string>();

        // 分行处理
        var lines = yaml.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            // 跳过注释行和空行
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) ||
                (options.IgnoreComments && trimmedLine.StartsWith('#')))
            {
                continue;
            }

            // 解析键值对(格式：key: value)
            var match = YamlKeyValueRegex().Match(trimmedLine);
            if (!match.Success)
            {
                continue;
            }

            var key = match.Groups[1].Value.Trim();
            var value = match.Groups[2].Value.Trim();

            // 处理引号包裹的值
            value = ProcessQuotedValue(value);

            // 类型转换
            if (options.ConvertTypes)
            {
                value = ConvertValueType(value);
            }

            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// 将字典转换为 YAML 字符串
    /// </summary>
    /// <param name="data">字典数据</param>
    /// <param name="options">序列化选项</param>
    /// <returns>YAML 格式字符串</returns>
    /// <remarks>
    /// 入参是一层扁平的字符串字典，没有嵌套集合可折行，
    /// 因此 UseFlowStyle 与 MaxLineLength 在这条路径上没有作用对象，只在 <see cref="Serialize{T}"/> 一侧生效。
    /// </remarks>
    public static string ConvertToYaml(Dictionary<string, string> data, YamlSerializeOptions? options = null)
    {
        if (data.Count == 0)
        {
            return string.Empty;
        }

        options ??= new YamlSerializeOptions();
        var sb = new StringBuilder();

        // 添加文档头
        if (options.IncludeDocumentMarkers)
        {
            sb.AppendLine("---");
        }

        // 添加注释
        if (!string.IsNullOrEmpty(options.HeaderComment))
        {
            foreach (var commentLine in options.HeaderComment.Split('\n'))
            {
                sb.AppendLine($"# {commentLine.Trim()}");
            }
        }

        // SortKeys 原来完全没有读取点，这里恢复它的语义：关闭时按字典自身的枚举顺序输出。
        // 开启时仍沿用原来的默认字符串比较器排序，避免顺带改变既有输出的键顺序。
        IEnumerable<KeyValuePair<string, string>> entries = options.SortKeys ? data.OrderBy(x => x.Key) : data;

        foreach (var kvp in entries)
        {
            var key = kvp.Key;
            var value = kvp.Value;

            // 检查值是否需要引号
            if (NeedsQuotes(value, options))
            {
                value = $"\"{EscapeYamlString(value)}\"";
            }

            sb.AppendLine($"{key}: {value}");
        }

        // 添加文档尾
        if (options.IncludeDocumentMarkers)
        {
            sb.AppendLine("...");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 解析多层级的 YAML（扁平化处理）
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">解析选项</param>
    /// <returns>扁平化的键值对字典，键使用点号分隔层级</returns>
    /// <remarks>
    /// 序列（短横线列表与 [a, b] 流式列表）按下标展开成 tags.0、tags.1 这样的扁平键，
    /// 与配置系统惯用的数组扁平化写法一致。
    /// </remarks>
    public static Dictionary<string, string> ParseNestedYaml(string yaml, YamlParseOptions? options = null)
    {
        return ParseNestedYaml(yaml, options, out _);
    }

    /// <summary>
    /// 解析多层级的 YAML（扁平化处理），同时输出被识别为序列的节点路径
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">解析选项</param>
    /// <param name="sequenceKeys">被识别为序列的节点路径（根序列为空串）</param>
    /// <returns>扁平化的键值对字典，键使用点号分隔层级</returns>
    /// <remarks>
    /// 扁平字典本身表达不了"这一层是数组还是以下标为键的对象"，
    /// 所以把序列路径单独带出来，供 <see cref="BuildNestedStructure"/> 还原成 JSON 数组。
    /// </remarks>
    private static Dictionary<string, string> ParseNestedYaml(string yaml, YamlParseOptions? options, out HashSet<string> sequenceKeys)
    {
        sequenceKeys = [];

        if (string.IsNullOrWhiteSpace(yaml))
        {
            return [];
        }

        options ??= new YamlParseOptions();
        var result = new Dictionary<string, string>();
        var lines = yaml.Split(Separator, StringSplitOptions.None);

        var currentPrefix = "";
        var indentStack = new Stack<(string Prefix, int Indent, bool IsSequenceItem)>();
        // 各序列节点的下标游标：键是序列所在的完整路径，值是下一个可用下标
        var sequenceCursors = new Dictionary<string, int>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                (options.IgnoreComments && line.Trim().StartsWith('#')))
            {
                continue;
            }

            // 计算当前行的缩进级别
            var leadingSpaces = line.Length - line.TrimStart().Length;
            var trimmedLine = line.Trim();
            var isSequenceItem = trimmedLine == "-" || trimmedLine.StartsWith("- ", StringComparison.Ordinal);

            // 回退缩进栈，直到找到合适的父级
            while (indentStack.Count > 0)
            {
                var parent = indentStack.Peek();

                // 块序列允许与父键同缩进（tags:\n- 甲 是合法 YAML），这种同缩进不能把父键弹掉；
                // 但同缩进的上一个序列项必须弹掉，否则兄弟项会被挂到前一项下面。
                var shouldPop = isSequenceItem && !parent.IsSequenceItem
                    ? leadingSpaces < parent.Indent
                    : leadingSpaces <= parent.Indent;

                if (!shouldPop)
                {
                    break;
                }

                indentStack.Pop();
                currentPrefix = indentStack.Count > 0 ? indentStack.Peek().Prefix : "";
            }

            // 序列行（- item）：原实现只认 key: value，短横线列表整段匹配不上正则被静默跳过，
            // 而 Serialize 恰恰把集合写成短横线列表，于是 Deserialize(Serialize(obj)) 会丢光集合成员。
            // 这里按下标把序列展开成扁平键，让写出去的语法自己能读回来。
            if (isSequenceItem)
            {
                sequenceKeys.Add(currentPrefix);

                var index = sequenceCursors.TryGetValue(currentPrefix, out var cursor) ? cursor : 0;
                sequenceCursors[currentPrefix] = index + 1;

                var indexText = index.ToString(CultureInfo.InvariantCulture);
                var itemKey = string.IsNullOrEmpty(currentPrefix)
                    ? indexText
                    : $"{currentPrefix}{options.KeySeparator}{indexText}";
                var itemValue = trimmedLine.Length > 1 ? trimmedLine[2..].Trim() : string.Empty;

                if (itemValue.Length == 0)
                {
                    // 光秃秃的短横线表示这一项是对象或子序列，后续更深缩进的行都挂在它下面
                    indentStack.Push((itemKey, leadingSpaces, true));
                    currentPrefix = itemKey;
                }
                else if (!TryFlattenFlowValue(itemValue, itemKey, options, result, sequenceKeys))
                {
                    itemValue = ProcessQuotedValue(itemValue);

                    if (options.ConvertTypes)
                    {
                        itemValue = ConvertValueType(itemValue);
                    }

                    result[itemKey] = itemValue;
                }

                continue;
            }

            var keyValueMatch = YamlKeyValueRegex().Match(trimmedLine);

            if (!keyValueMatch.Success)
            {
                continue;
            }

            var key = keyValueMatch.Groups[1].Value.Trim();
            var value = keyValueMatch.Groups[2].Value.Trim();

            var fullKey = string.IsNullOrEmpty(currentPrefix) ? key : $"{currentPrefix}{options.KeySeparator}{key}";

            // 如果值不为空，则是叶子节点
            if (!string.IsNullOrEmpty(value))
            {
                // 流式集合先展开，展开不了才当普通标量
                if (TryFlattenFlowValue(value, fullKey, options, result, sequenceKeys))
                {
                    continue;
                }

                value = ProcessQuotedValue(value);

                if (options.ConvertTypes)
                {
                    value = ConvertValueType(value);
                }

                result[fullKey] = value;
            }
            // 如果值为空，则是中间节点
            else
            {
                indentStack.Push((fullKey, leadingSpaces, false));
                currentPrefix = fullKey;
            }
        }

        return result;
    }

    #endregion 字典操作

    #region 验证和转换

    /// <summary>
    /// 验证 YAML 字符串是否有效
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="errorMessage">错误信息</param>
    /// <returns>是否有效</returns>
    /// <remarks>
    /// 本类是逐行正则的轻量解析器，判定口径是"能解析出内容"：
    /// 含冒号的散文行仍可能被当成键值对，这是行级解析的固有限制，不是完整 YAML 语法校验。
    /// </remarks>
    public static bool IsValidYaml(string yaml, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(yaml))
        {
            errorMessage = "YAML 字符串为空";
            return false;
        }

        try
        {
            // 原实现是"ParseYaml 不抛异常即有效"，而解析器对任何匹配不上的行都是静默跳过、从不抛异常，
            // 于是除空白外的一切输入（包括纯散文）都被判为合法。这里改为要求至少解析出一个键值对或序列项。
            var parsed = ParseNestedYaml(yaml, null, out var sequenceKeys);

            if (parsed.Count == 0 && sequenceKeys.Count == 0)
            {
                errorMessage = "未解析到任何 YAML 键值对或序列项";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 验证 YAML 字符串是否有效（简化版本）
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <returns>是否有效</returns>
    public static bool IsValidYaml(string yaml)
    {
        return IsValidYaml(yaml, out _);
    }

    /// <summary>
    /// YAML 转 JSON 字符串
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">转换选项</param>
    /// <returns>JSON 字符串</returns>
    public static string YamlToJson(string yaml, YamlDeserializeOptions? options = null)
    {
        return ConvertYamlToJson(yaml, options);
    }

    /// <summary>
    /// JSON 转 YAML 字符串
    /// </summary>
    /// <param name="json">JSON 字符串</param>
    /// <param name="options">序列化选项</param>
    /// <returns>YAML 字符串</returns>
    public static string JsonToYaml(string json, YamlSerializeOptions? options = null)
    {
        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
            return ConvertJsonElementToYaml(jsonElement, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"JSON 转 YAML 失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 格式化 YAML 字符串
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">序列化选项</param>
    /// <returns>格式化后的 YAML 字符串</returns>
    /// <remarks>
    /// 只规范缩进、键序与引号，不改变文档层级。
    /// 值一律按字符串处理，因此 18、true 这类字面量会被加引号以保住字符串语义。
    /// </remarks>
    public static string FormatYaml(string yaml, YamlSerializeOptions? options = null)
    {
        try
        {
            options ??= new YamlSerializeOptions();

            // 原实现是 ParseNestedYaml + ConvertToYaml：扁平化出来的点号键被当成顶层键写回，
            // a:\n  b: 1 会被"格式化"成 a.b: 1，等于把文档结构一起改了。
            // 这里仍用扁平解析拿到值，但按点号键重新还原层级后再输出。
            var flat = ParseNestedYaml(yaml, null, out var sequenceKeys);

            if (flat.Count == 0)
            {
                return string.Empty;
            }

            var root = BuildRawNodeTree(flat, DefaultKeySeparator);
            var sb = new StringBuilder();

            // 添加文档头
            if (options.IncludeDocumentMarkers)
            {
                sb.AppendLine("---");
            }

            // 添加注释
            if (!string.IsNullOrEmpty(options.HeaderComment))
            {
                foreach (var commentLine in options.HeaderComment.Split('\n'))
                {
                    sb.AppendLine($"# {commentLine.Trim()}");
                }
            }

            AppendFormattedNode(root, string.Empty, sequenceKeys, options, 0, sb);

            // 添加文档尾
            if (options.IncludeDocumentMarkers)
            {
                sb.AppendLine("...");
            }

            return sb.ToString();
        }
        catch
        {
            return yaml; // 如果格式化失败，返回原始字符串
        }
    }

    #endregion 验证和转换

    #region 私有辅助方法

    /// <summary>
    /// 处理引号包裹的值
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>处理后的值</returns>
    private static string ProcessQuotedValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // 处理引号包裹的值
        if ((value.StartsWith('\'') && value.EndsWith('\'')) ||
            (value.StartsWith('\"') && value.EndsWith('\"')))
        {
            value = value[1..^1];
            // 处理转义字符
            value = UnescapeYamlString(value);
        }

        return value;
    }

    /// <summary>
    /// 转换值类型
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>转换后的值</returns>
    private static string ConvertValueType(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // 布尔值转换
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant();
        }

        // null 值转换
        if (value.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("~", StringComparison.OrdinalIgnoreCase))
        {
            return "null";
        }

        // 数字格式化
        if (NumericValueRegex().IsMatch(value))
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue))
            {
                return decimalValue.ToString(CultureInfo.InvariantCulture);
            }
        }

        return value;
    }

    /// <summary>
    /// 判断字符串是否需要引号包裹
    /// </summary>
    /// <param name="value">要检查的字符串</param>
    /// <param name="options">序列化选项</param>
    /// <returns>是否需要引号</returns>
    private static bool NeedsQuotes(string value, YamlSerializeOptions? options = null)
    {
        options ??= new YamlSerializeOptions();

        // 空字符串需要引号
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        // 包含特殊字符需要引号
        if (value.Contains(':') || value.Contains('#') || value.Contains('\n') ||
            value.Contains('{') || value.Contains('}') || value.Contains('[') ||
            value.Contains(']') || value.StartsWith(' ') || value.EndsWith(' ') ||
            value.Contains('\t') || value.Contains('\r'))
        {
            return true;
        }

        // 如果强制引号字符串
        if (options.ForceQuoteStrings)
        {
            return !NumericValueRegex().IsMatch(value) &&
                   !value.Equals("true", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("false", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("null", StringComparison.OrdinalIgnoreCase);
        }

        // 纯数字或布尔值需要引号来确保被视为字符串
        return value == "true" || value == "false" || value == "null" ||
               NumericValueRegex().IsMatch(value);
    }

    /// <summary>
    /// 转义 YAML 字符串中的特殊字符
    /// </summary>
    /// <param name="value">需要转义的字符串</param>
    /// <returns>转义后的字符串</returns>
    private static string EscapeYamlString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// 反转义 YAML 字符串中的特殊字符
    /// </summary>
    /// <param name="value">需要反转义的字符串</param>
    /// <returns>反转义后的字符串</returns>
    private static string UnescapeYamlString(string value)
    {
        return value
            .Replace("\\\"", "\"")
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\\", "\\");
    }

    /// <summary>
    /// 将 JsonElement 转换为 YAML 字符串
    /// </summary>
    /// <param name="element">JSON 元素</param>
    /// <param name="options">序列化选项</param>
    /// <param name="indent">缩进级别</param>
    /// <returns>YAML 字符串</returns>
    private static string ConvertJsonElementToYaml(JsonElement element, YamlSerializeOptions? options = null, int indent = 0)
    {
        options ??= new YamlSerializeOptions();
        var sb = new StringBuilder();
        var indentStr = new string(' ', indent * options.IndentSize);

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                // SortKeys 原来只在字典路径上被"无条件排序"顶替，对象路径压根没读；这里统一按选项决定是否排序
                foreach (var property in EnumerateProperties(element, options))
                {
                    sb.Append($"{indentStr}{property.Name}:");

                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        // 流式样式：能折成一行就折，折不下（超过 MaxLineLength）再退回块式
                        if (TryConvertToFlowYaml(property.Value, options, indentStr.Length + property.Name.Length + 2, out var flow))
                        {
                            sb.AppendLine($" {flow}");
                        }
                        else
                        {
                            sb.AppendLine();
                            sb.Append(ConvertJsonElementToYaml(property.Value, options, indent + 1));
                        }
                    }
                    else
                    {
                        sb.AppendLine($" {ConvertJsonElementToYaml(property.Value, options).Trim()}");
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    // 原来硬编码 "- "，ArrayPrefix 选项从未生效
                    sb.Append($"{indentStr}{options.ArrayPrefix}");
                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        if (TryConvertToFlowYaml(item, options, indentStr.Length + options.ArrayPrefix.Length, out var flow))
                        {
                            sb.AppendLine(flow);
                        }
                        else
                        {
                            sb.AppendLine();
                            sb.Append(ConvertJsonElementToYaml(item, options, indent + 1));
                        }
                    }
                    else
                    {
                        sb.AppendLine(ConvertJsonElementToYaml(item, options).Trim());
                    }
                }
                break;

            case JsonValueKind.String:
                var stringValue = element.GetString() ?? "";
                if (NeedsQuotes(stringValue, options))
                {
                    sb.Append($"\"{EscapeYamlString(stringValue)}\"");
                }
                else
                {
                    sb.Append(stringValue);
                }
                break;

            case JsonValueKind.Number:
                sb.Append(element.GetRawText());
                break;

            case JsonValueKind.True:
                sb.Append("true");
                break;

            case JsonValueKind.False:
                sb.Append("false");
                break;

            case JsonValueKind.Null:
                sb.Append("null");
                break;
        }

        return sb.ToString();
    }

    /// <summary>
    /// 将 YAML 转换为 JSON 字符串
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>JSON 字符串</returns>
    private static string ConvertYamlToJson(string yaml, YamlDeserializeOptions? options = null)
    {
        options ??= new YamlDeserializeOptions();

        try
        {
            var dict = ParseNestedYaml(yaml, new YamlParseOptions
            {
                IgnoreComments = options.IgnoreComments,
                ConvertTypes = options.ConvertTypes,
                KeySeparator = options.KeySeparator
            }, out var sequenceKeys);

            // 重建嵌套结构（序列路径要还原成数组，否则集合会变成以下标为键的对象）
            var nested = BuildNestedStructure(dict, options.KeySeparator, sequenceKeys);

            return JsonSerializer.Serialize(nested, JsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"YAML 转 JSON 失败：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 从扁平化字典重建嵌套结构
    /// </summary>
    /// <param name="flatDict">扁平化字典</param>
    /// <param name="separator">键分隔符</param>
    /// <param name="sequenceKeys">被识别为序列的节点路径</param>
    /// <returns>嵌套结构</returns>
    private static object BuildNestedStructure(Dictionary<string, string> flatDict, string separator, HashSet<string>? sequenceKeys = null)
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

            // 尝试转换值类型
            var value = ConvertStringValue(kvp.Value);
            current[keys[^1]] = value;
        }

        return sequenceKeys is { Count: > 0 }
            ? NormalizeSequenceNodes(result, string.Empty, separator, sequenceKeys)
            : result;
    }

    /// <summary>
    /// 把序列路径上的下标字典还原成数组
    /// </summary>
    /// <param name="node">当前节点</param>
    /// <param name="path">当前节点的完整路径（根为空串）</param>
    /// <param name="separator">键分隔符</param>
    /// <param name="sequenceKeys">被识别为序列的节点路径</param>
    /// <returns>还原后的节点：序列返回列表，其余返回原字典</returns>
    /// <remarks>
    /// 序列在扁平字典里长得和"以 0、1 为键的对象"一模一样，
    /// 只按形状猜会把 {"0": ...} 这类合法映射误判成数组，因此严格按解析阶段记录的路径还原。
    /// </remarks>
    private static object NormalizeSequenceNodes(Dictionary<string, object> node, string path, string separator, HashSet<string> sequenceKeys)
    {
        foreach (var key in node.Keys.ToList())
        {
            if (node[key] is Dictionary<string, object> child)
            {
                var childPath = string.IsNullOrEmpty(path) ? key : $"{path}{separator}{key}";
                node[key] = NormalizeSequenceNodes(child, childPath, separator, sequenceKeys);
            }
        }

        if (!sequenceKeys.Contains(path))
        {
            return node;
        }

        return node
            .OrderBy(pair => int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : int.MaxValue)
            .Select(pair => pair.Value)
            .ToList();
    }

    /// <summary>
    /// 转换字符串值为适当的类型
    /// </summary>
    /// <param name="value">字符串值</param>
    /// <returns>转换后的值</returns>
    private static object ConvertStringValue(string value)
    {
        return string.IsNullOrEmpty(value) || value == "null"
            ? null!
            : value.Equals("true", StringComparison.OrdinalIgnoreCase)
            ? true
            : value.Equals("false", StringComparison.OrdinalIgnoreCase)
            ? false
            : int.TryParse(value, out var intValue)
            ? intValue
            : double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue) ? doubleValue : value;
    }

    /// <summary>
    /// 按选项枚举对象属性
    /// </summary>
    /// <param name="element">JSON 对象元素</param>
    /// <param name="options">序列化选项</param>
    /// <returns>属性序列</returns>
    private static IEnumerable<JsonProperty> EnumerateProperties(JsonElement element, YamlSerializeOptions options)
    {
        IEnumerable<JsonProperty> properties = element.EnumerateObject();
        return options.SortKeys ? properties.OrderBy(property => property.Name, StringComparer.Ordinal) : properties;
    }

    /// <summary>
    /// 尝试把集合折成流式样式
    /// </summary>
    /// <param name="element">JSON 元素</param>
    /// <param name="options">序列化选项</param>
    /// <param name="usedWidth">该行已被键名与缩进占用的宽度</param>
    /// <param name="flow">折行结果</param>
    /// <returns>是否折成了流式样式</returns>
    /// <remarks>
    /// UseFlowStyle 与 MaxLineLength 原来都没有读取点。这里把两者接起来：
    /// 开启流式样式后集合优先折成一行，折出来的行超过 MaxLineLength 就退回块式，避免紧凑输出变成超长行。
    /// 折出来的语法（{a: 1} / [x, y]）解析侧同样支持，往返不会丢内容。
    /// </remarks>
    private static bool TryConvertToFlowYaml(JsonElement element, YamlSerializeOptions options, int usedWidth, out string flow)
    {
        flow = string.Empty;

        if (!options.UseFlowStyle || element.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            return false;
        }

        var candidate = ConvertJsonElementToFlowYaml(element, options);

        if (usedWidth + candidate.Length > options.MaxLineLength)
        {
            return false;
        }

        flow = candidate;
        return true;
    }

    /// <summary>
    /// 将 JSON 元素整体渲染为流式样式
    /// </summary>
    /// <param name="element">JSON 元素</param>
    /// <param name="options">序列化选项</param>
    /// <returns>流式样式字符串</returns>
    /// <remarks>YAML 的流式集合内部不能再出现块式，因此子元素也必须走流式。</remarks>
    private static string ConvertJsonElementToFlowYaml(JsonElement element, YamlSerializeOptions options)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var pairs = EnumerateProperties(element, options)
                    .Select(property => $"{property.Name}: {ConvertJsonElementToFlowYaml(property.Value, options)}");
                return $"{{{string.Join(", ", pairs)}}}";

            case JsonValueKind.Array:
                var items = element.EnumerateArray()
                    .Select(item => ConvertJsonElementToFlowYaml(item, options));
                return $"[{string.Join(", ", items)}]";

            default:
                var scalar = ConvertJsonElementToYaml(element, options).Trim();

                // 流式样式里逗号是成员分隔符，含逗号的标量必须加引号，否则会被读成多个成员
                return element.ValueKind == JsonValueKind.String && scalar.Contains(',') && !scalar.StartsWith('\"')
                    ? $"\"{EscapeYamlString(element.GetString() ?? string.Empty)}\""
                    : scalar;
        }
    }

    /// <summary>
    /// 尝试把流式集合展开成扁平键
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="fullKey">该值所在的完整路径</param>
    /// <param name="options">解析选项</param>
    /// <param name="result">扁平字典</param>
    /// <param name="sequenceKeys">被识别为序列的节点路径</param>
    /// <returns>是否是流式集合</returns>
    /// <remarks>
    /// NeedsQuotes 会给含花括号/方括号的字符串加引号，
    /// 所以未加引号却以括号成对包裹的值只可能是本类自己写出去的流式集合。
    /// </remarks>
    private static bool TryFlattenFlowValue(string value, string fullKey, YamlParseOptions options, Dictionary<string, string> result, HashSet<string> sequenceKeys)
    {
        var trimmed = value.Trim();

        if (trimmed.Length < 2)
        {
            return false;
        }

        if (trimmed[0] == '{' && trimmed[^1] == '}')
        {
            foreach (var member in SplitFlowMembers(trimmed[1..^1]))
            {
                var match = YamlKeyValueRegex().Match(member.Trim());

                if (!match.Success)
                {
                    continue;
                }

                var childKey = $"{fullKey}{options.KeySeparator}{match.Groups[1].Value.Trim()}";
                AppendFlowLeaf(match.Groups[2].Value, childKey, options, result, sequenceKeys);
            }

            return true;
        }

        if (trimmed[0] == '[' && trimmed[^1] == ']')
        {
            sequenceKeys.Add(fullKey);
            var index = 0;

            foreach (var member in SplitFlowMembers(trimmed[1..^1]))
            {
                if (member.Trim().Length == 0)
                {
                    continue;
                }

                AppendFlowLeaf(member, $"{fullKey}{options.KeySeparator}{index.ToString(CultureInfo.InvariantCulture)}", options, result, sequenceKeys);
                index++;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 写入流式集合的一个成员
    /// </summary>
    /// <param name="value">成员原始文本</param>
    /// <param name="fullKey">成员的完整路径</param>
    /// <param name="options">解析选项</param>
    /// <param name="result">扁平字典</param>
    /// <param name="sequenceKeys">被识别为序列的节点路径</param>
    private static void AppendFlowLeaf(string value, string fullKey, YamlParseOptions options, Dictionary<string, string> result, HashSet<string> sequenceKeys)
    {
        if (TryFlattenFlowValue(value, fullKey, options, result, sequenceKeys))
        {
            return;
        }

        var leaf = ProcessQuotedValue(value.Trim());

        if (options.ConvertTypes)
        {
            leaf = ConvertValueType(leaf);
        }

        result[fullKey] = leaf;
    }

    /// <summary>
    /// 拆分流式集合的顶层成员
    /// </summary>
    /// <param name="content">去掉外层括号后的内容</param>
    /// <returns>顶层成员文本列表</returns>
    /// <remarks>嵌套括号内与引号内的逗号不算分隔符。</remarks>
    private static List<string> SplitFlowMembers(string content)
    {
        var members = new List<string>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return members;
        }

        var depth = 0;
        var quote = '\0';
        var start = 0;

        for (var i = 0; i < content.Length; i++)
        {
            var current = content[i];

            if (quote != '\0')
            {
                if (current == '\\')
                {
                    i++;
                }
                else if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            switch (current)
            {
                case '\"':
                case '\'':
                    quote = current;
                    break;

                case '{':
                case '[':
                    depth++;
                    break;

                case '}':
                case ']':
                    depth--;
                    break;

                case ',' when depth == 0:
                    members.Add(content[start..i]);
                    start = i + 1;
                    break;
            }
        }

        members.Add(content[start..]);
        return members;
    }

    /// <summary>
    /// 按点号键把扁平字典还原成原始字符串的层级树
    /// </summary>
    /// <param name="flatDict">扁平字典</param>
    /// <param name="separator">键分隔符</param>
    /// <returns>层级树，叶子是未做类型转换的原始字符串</returns>
    /// <remarks>
    /// 与 <see cref="BuildNestedStructure"/> 的区别是不把值转成 int/bool，
    /// 格式化要保住 "18" 这类字符串字面量的引号语义。
    /// </remarks>
    private static Dictionary<string, object> BuildRawNodeTree(Dictionary<string, string> flatDict, string separator)
    {
        var root = new Dictionary<string, object>();

        foreach (var kvp in flatDict)
        {
            var keys = kvp.Key.Split(separator);
            var current = root;

            for (var i = 0; i < keys.Length - 1; i++)
            {
                if (current.TryGetValue(keys[i], out var existing) && existing is Dictionary<string, object> childNode)
                {
                    current = childNode;
                    continue;
                }

                var created = new Dictionary<string, object>();
                current[keys[i]] = created;
                current = created;
            }

            current[keys[^1]] = kvp.Value;
        }

        return root;
    }

    /// <summary>
    /// 按层级输出格式化后的 YAML 节点
    /// </summary>
    /// <param name="node">当前节点</param>
    /// <param name="path">当前节点的完整路径（根为空串）</param>
    /// <param name="sequenceKeys">被识别为序列的节点路径</param>
    /// <param name="options">序列化选项</param>
    /// <param name="indent">缩进级别</param>
    /// <param name="sb">输出缓冲</param>
    private static void AppendFormattedNode(Dictionary<string, object> node, string path, HashSet<string> sequenceKeys, YamlSerializeOptions options, int indent, StringBuilder sb)
    {
        var indentStr = new string(' ', indent * options.IndentSize);
        var isSequence = sequenceKeys.Contains(path);

        IEnumerable<KeyValuePair<string, object>> entries = node;

        if (isSequence)
        {
            // 序列必须按下标顺序输出，不能参与键排序
            entries = node.OrderBy(pair => int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : int.MaxValue);
        }
        else if (options.SortKeys)
        {
            entries = node.OrderBy(pair => pair.Key);
        }

        foreach (var entry in entries)
        {
            var childPath = path.Length == 0 ? entry.Key : $"{path}{DefaultKeySeparator}{entry.Key}";

            if (entry.Value is Dictionary<string, object> childNode)
            {
                sb.AppendLine(isSequence ? $"{indentStr}{options.ArrayPrefix.TrimEnd()}" : $"{indentStr}{entry.Key}:");
                AppendFormattedNode(childNode, childPath, sequenceKeys, options, indent + 1, sb);
                continue;
            }

            var value = entry.Value as string ?? string.Empty;

            if (NeedsQuotes(value, options))
            {
                value = $"\"{EscapeYamlString(value)}\"";
            }

            sb.AppendLine(isSequence ? $"{indentStr}{options.ArrayPrefix}{value}" : $"{indentStr}{entry.Key}: {value}");
        }
    }

    #endregion 私有辅助方法

    #region 正则表达式

    [GeneratedRegex(@"^([^:]+):\s*(.*)$")]
    private static partial Regex YamlKeyValueRegex();

    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex NumericValueRegex();

    #endregion 正则表达式
}
