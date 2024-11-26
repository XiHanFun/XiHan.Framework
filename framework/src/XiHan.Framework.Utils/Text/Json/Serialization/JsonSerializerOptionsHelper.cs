#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonSerializerOptionsHelper
// Guid:86f5669e-8854-4105-8073-6147be5d7b7e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/22 3:05:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Extensions.System.Collections.Generic;
using XiHan.Framework.Utils.Text.Json.Converter;

namespace XiHan.Framework.Utils.Text.Json.Serialization;

/// <summary>
/// 序列化参数帮助类
/// </summary>
public static class JsonSerializerOptionsHelper
{
    /// <summary>
    /// 公共参数
    /// </summary>
    public static JsonSerializerOptions DefaultJsonSerializerOptions => GetDefaultJsonSerializerOptions();

    /// <summary>
    /// 获取默认序列化参数
    /// </summary>
    /// <returns></returns>
    public static JsonSerializerOptions GetDefaultJsonSerializerOptions()
    {
        JsonSerializerOptions options = new()
        {
            // 序列化格式
            WriteIndented = true,
            // 忽略循环引用
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            // 数字类型
            NumberHandling = JsonNumberHandling.Strict,
            // 允许额外符号
            AllowTrailingCommas = true,
            // 注释处理，允许在 JSON 输入中使用注释并忽略它们
            ReadCommentHandling = JsonCommentHandling.Skip,
            // 属性名称不使用不区分大小写的比较
            PropertyNameCaseInsensitive = false,
            // 数据格式首字母小写 JsonNamingPolicy.CamelCase 驼峰样式，null 则为不改变大小写
            PropertyNamingPolicy = null,
            // 获取或设置要在转义字符串时使用的编码器，不转义字符
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        // 布尔类型
        options.Converters.Add(new BooleanJsonConverter());
        // 数字类型
        options.Converters.Add(new IntJsonConverter());
        options.Converters.Add(new LongJsonConverter());
        options.Converters.Add(new DecimalJsonConverter());
        // 日期类型
        options.Converters.Add(new DateTimeJsonConverter("yyyy-MM-dd HH:mm:ss"));
        return options;
    }

    /// <summary>
    /// 使用 baseOptions 作为基础，移除 removeConverter，并添加 addConverters 中的转换器（如果它们尚不存在）
    /// </summary>
    /// <param name="baseOptions"></param>
    /// <param name="removeConverter"></param>
    /// <param name="addConverters"></param>
    /// <returns></returns>
    public static JsonSerializerOptions Create(JsonSerializerOptions baseOptions, JsonConverter removeConverter, params JsonConverter[] addConverters)
    {
        return Create(baseOptions, x => x == removeConverter, addConverters);
    }

    /// <summary>
    /// 使用 baseOptions 作为基础，移除匹配 removeConverterPredicate 谓词的转换器，并添加 addConverters 中的转换器（如果它们尚不存在）
    /// </summary>
    /// <param name="baseOptions"></param>
    /// <param name="removeConverterPredicate"></param>
    /// <param name="addConverters"></param>
    /// <returns></returns>
    public static JsonSerializerOptions Create(JsonSerializerOptions baseOptions, Func<JsonConverter, bool> removeConverterPredicate, params JsonConverter[] addConverters)
    {
        JsonSerializerOptions? options = new(baseOptions);
        _ = options.Converters.RemoveAll(removeConverterPredicate);
        _ = options.Converters.AddIfNotContains(addConverters);
        return options;
    }
}