#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonDeserializeOptions
// Guid:c9f4e532-ad8f-4b3e-9f7c-2f5d3e4a5b6c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/6 8:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Serialization.Json;

/// <summary>
/// JSON 反序列化选项
/// </summary>
public class JsonDeserializeOptions
{
    /// <summary>
    /// 创建默认选项
    /// </summary>
    /// <returns>默认反序列化选项</returns>
    public static JsonDeserializeOptions Default => new();

    /// <summary>
    /// 创建严格模式选项（不允许任何容错处理）
    /// </summary>
    /// <returns>严格模式选项</returns>
    public static JsonDeserializeOptions Strict => new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false,
        ReadCommentHandling = false,
        IgnoreUnknownProperties = false,
        UseDefaultValues = false,
        NumberHandling = JsonNumberHandling.Strict
    };

    /// <summary>
    /// 创建宽松模式选项（允许各种容错处理）
    /// </summary>
    /// <returns>宽松模式选项</returns>
    public static JsonDeserializeOptions Lenient => new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = true,
        IgnoreUnknownProperties = true,
        UseDefaultValues = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    /// <summary>
    /// 创建 Web API 兼容选项
    /// </summary>
    /// <returns>Web API 选项</returns>
    public static JsonDeserializeOptions WebApi => new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = true,
        IgnoreUnknownProperties = true,
        UseDefaultValues = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        CustomConverters = JsonConverterFactory.GetAllConverters()
    };

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
    /// 是否忽略未知属性
    /// </summary>
    public bool IgnoreUnknownProperties { get; set; } = true;

    /// <summary>
    /// 是否使用默认值填充缺失属性
    /// </summary>
    public bool UseDefaultValues { get; set; } = true;

    /// <summary>
    /// 属性命名策略
    /// </summary>
    public JsonNamingPolicy? PropertyNamingPolicy { get; set; } = JsonNamingPolicy.CamelCase;

    /// <summary>
    /// 数字处理方式
    /// </summary>
    public JsonNumberHandling NumberHandling { get; set; } = JsonNumberHandling.AllowReadingFromString;

    /// <summary>
    /// 最大嵌套深度
    /// </summary>
    public int MaxDepth { get; set; } = 64;

    /// <summary>
    /// 编码器
    /// </summary>
    public JavaScriptEncoder? Encoder { get; set; } = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    /// <summary>
    /// 默认忽略条件
    /// </summary>
    public JsonIgnoreCondition DefaultIgnoreCondition { get; set; } = JsonIgnoreCondition.Never;

    /// <summary>
    /// 自定义转换器
    /// </summary>
    public List<JsonConverter>? CustomConverters { get; set; }

    /// <summary>
    /// 错误处理策略
    /// </summary>
    public JsonErrorHandling ErrorHandling { get; set; } = JsonErrorHandling.ThrowException;

    /// <summary>
    /// 是否验证 JSON 格式
    /// </summary>
    public bool ValidateJson { get; set; } = true;

    /// <summary>
    /// 最大字符串长度限制（0 表示无限制）
    /// </summary>
    public long MaxStringLength { get; set; } = 0;

    /// <summary>
    /// 最大数组长度限制（0 表示无限制）
    /// </summary>
    public long MaxArrayLength { get; set; } = 0;

    /// <summary>
    /// 转换为 JsonSerializerOptions
    /// </summary>
    /// <returns>系统的 JsonSerializerOptions</returns>
    public JsonSerializerOptions ToSystemOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = PropertyNameCaseInsensitive,
            AllowTrailingCommas = AllowTrailingCommas,
            ReadCommentHandling = ReadCommentHandling ? JsonCommentHandling.Skip : JsonCommentHandling.Disallow,
            PropertyNamingPolicy = PropertyNamingPolicy,
            NumberHandling = NumberHandling,
            MaxDepth = MaxDepth,
            Encoder = Encoder,
            DefaultIgnoreCondition = DefaultIgnoreCondition
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

/// <summary>
/// JSON 错误处理策略
/// </summary>
public enum JsonErrorHandling
{
    /// <summary>
    /// 抛出异常（默认）
    /// </summary>
    ThrowException,

    /// <summary>
    /// 忽略错误，继续处理
    /// </summary>
    Ignore,

    /// <summary>
    /// 使用默认值替换
    /// </summary>
    UseDefault,

    /// <summary>
    /// 记录错误但继续处理
    /// </summary>
    Log
}
