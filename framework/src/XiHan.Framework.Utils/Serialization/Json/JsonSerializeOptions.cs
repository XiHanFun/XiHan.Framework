#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonSerializeOptions
// Guid:b8f3d421-9c7e-4a2d-8f6b-1e4c2d3a4b5c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 08:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Serialization.Json;

/// <summary>
/// JSON 序列化选项
/// </summary>
public class JsonSerializeOptions
{
    /// <summary>
    /// 创建默认选项
    /// </summary>
    /// <returns>默认序列化选项</returns>
    public static JsonSerializeOptions Default => new();

    /// <summary>
    /// 创建紧凑格式选项（无缩进、驼峰命名）
    /// </summary>
    /// <returns>紧凑格式选项</returns>
    public static JsonSerializeOptions Compact => new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IgnoreNullValues = true,
        IgnoreReadOnlyProperties = true
    };

    /// <summary>
    /// 创建格式化选项（带缩进、易读格式）
    /// </summary>
    /// <returns>格式化选项</returns>
    public static JsonSerializeOptions Formatted => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IgnoreNullValues = false,
        IgnoreReadOnlyProperties = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 创建严格格式选项（保持原始命名、包含所有属性）
    /// </summary>
    /// <returns>严格格式选项</returns>
    public static JsonSerializeOptions Strict => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        IgnoreNullValues = false,
        IgnoreReadOnlyProperties = false,
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false
    };

    /// <summary>
    /// 创建 Web API 兼容选项
    /// </summary>
    /// <returns>Web API 选项</returns>
    public static JsonSerializeOptions WebApi => new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IgnoreNullValues = true,
        IgnoreReadOnlyProperties = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        CustomConverters = JsonConverterFactory.GetAllConverters()
    };

    /// <summary>
    /// 是否格式化输出（缩进）
    /// </summary>
    public bool WriteIndented { get; set; } = true;

    /// <summary>
    /// 属性命名策略
    /// </summary>
    public JsonNamingPolicy? PropertyNamingPolicy { get; set; } = JsonNamingPolicy.CamelCase;

    /// <summary>
    /// 是否忽略 null 值
    /// </summary>
    public bool IgnoreNullValues { get; set; }

    /// <summary>
    /// 是否忽略只读属性
    /// </summary>
    public bool IgnoreReadOnlyProperties { get; set; }

    /// <summary>
    /// 属性名是否大小写不敏感
    /// </summary>
    public bool PropertyNameCaseInsensitive { get; set; } = true;

    /// <summary>
    /// 是否允许尾随逗号
    /// </summary>
    public bool AllowTrailingCommas { get; set; } = true;

    /// <summary>
    /// 是否允许注释
    /// </summary>
    public bool ReadCommentHandling { get; set; } = true;

    /// <summary>
    /// 编码器
    /// </summary>
    public JavaScriptEncoder? Encoder { get; set; } = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    /// <summary>
    /// 最大嵌套深度
    /// </summary>
    public int MaxDepth { get; set; } = 64;

    /// <summary>
    /// 数字处理方式
    /// </summary>
    public JsonNumberHandling NumberHandling { get; set; } = JsonNumberHandling.AllowReadingFromString;

    /// <summary>
    /// 默认忽略条件
    /// </summary>
    public JsonIgnoreCondition DefaultIgnoreCondition { get; set; } = JsonIgnoreCondition.Never;

    /// <summary>
    /// 字符串编码
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// 自定义转换器
    /// </summary>
    public List<JsonConverter>? CustomConverters { get; set; }

    /// <summary>
    /// 转换为 JsonSerializerOptions
    /// </summary>
    /// <returns>系统的 JsonSerializerOptions</returns>
    public JsonSerializerOptions ToSystemOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = WriteIndented,
            PropertyNamingPolicy = PropertyNamingPolicy,
            PropertyNameCaseInsensitive = PropertyNameCaseInsensitive,
            AllowTrailingCommas = AllowTrailingCommas,
            ReadCommentHandling = ReadCommentHandling ? JsonCommentHandling.Skip : JsonCommentHandling.Disallow,
            Encoder = Encoder,
            MaxDepth = MaxDepth,
            NumberHandling = NumberHandling,
            DefaultIgnoreCondition = DefaultIgnoreCondition,
            IgnoreReadOnlyProperties = IgnoreReadOnlyProperties
        };

        // 添加自定义转换器
        if (CustomConverters?.Count > 0)
        {
            foreach (var converter in CustomConverters)
            {
                options.Converters.Add(converter);
            }
        }

        return options;
    }
}
