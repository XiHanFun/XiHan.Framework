#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
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
using XiHan.Framework.Utils.Caching;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Serialization.Json.Converters;

namespace XiHan.Framework.Utils.Serialization.Json;

/// <summary>
/// 序列化参数帮助类
/// </summary>
public static class JsonSerializerOptionsHelper
{
    /// <summary>
    /// 公共参数
    /// </summary>
    public static JsonSerializerOptions DefaultJsonSerializerOptions => CacheHelper.GetOrAdd("JsonSerializerOptions", () => GetDefaultJsonSerializerOptions());

    /// <summary>
    /// 获取默认序列化参数
    /// </summary>
    /// <returns></returns>
    public static JsonSerializerOptions GetDefaultJsonSerializerOptions()
    {
        JsonSerializerOptions options = new()
        {
            // 格式化输出
            WriteIndented = true,
            // 属性名称策略，null 为不改变大小写样式
            PropertyNamingPolicy = null,
            // 忽略循环引用
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            // 数字类型
            NumberHandling = JsonNumberHandling.Strict,
            // 注释处理，允许在 JSON 输入中使用注释并忽略它们
            ReadCommentHandling = JsonCommentHandling.Skip,
            // 忽略只读属性
            IgnoreReadOnlyProperties = true,
            // 允许尾随逗号
            AllowTrailingCommas = true,
            // 不转义字符
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // 布尔类型
        options.Converters.Add(new BooleanJsonConverter());

        // 数字类型
        options.Converters.Add(new IntJsonConverter());
        options.Converters.Add(new LongJsonConverter());
        options.Converters.Add(new DecimalJsonConverter());

        // 日期类型
        options.Converters.Add(new DateTimeOffsetJsonConverter("yyyy-MM-dd HH:mm:ss", false));
        options.Converters.Add(new DateTimeOffsetNullableConverter("yyyy-MM-dd HH:mm:ss", false));

        options.Converters.Add(new DateTimeJsonConverter("yyyy-MM-dd HH:mm:ss", false));
        options.Converters.Add(new DateTimeNullableConverter("yyyy-MM-dd HH:mm:ss", false));

        options.Converters.Add(new DateOnlyJsonConverter("yyyy-MM-dd"));
        options.Converters.Add(new DateOnlyNullableConverter("yyyy-MM-dd"));

        options.Converters.Add(new TimeOnlyJsonConverter("HH:mm:ss"));
        options.Converters.Add(new TimeOnlyNullableConverter("HH:mm:ss"));

        return options;
    }

    /// <summary>
    /// 获取默认配置的副本（可安全修改）
    /// </summary>
    /// <param name="configure">可选配置委托</param>
    /// <returns>JsonSerializerOptions 副本</returns>
    public static JsonSerializerOptions GetClonedDefault(Action<JsonSerializerOptions>? configure = null)
    {
        var clone = new JsonSerializerOptions(DefaultJsonSerializerOptions);
        configure?.Invoke(clone);
        return clone;
    }

    /// <summary>
    /// 使用 baseOptions 作为基础，移除 removeConverter，并添加 addConverters 中的转换器(如果它们尚不存在)
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
    /// 使用 baseOptions 作为基础，移除匹配 removeConverterPredicate 谓词的转换器，并添加 addConverters 中的转换器(如果它们尚不存在)
    /// </summary>
    /// <param name="baseOptions"></param>
    /// <param name="removeConverterPredicate"></param>
    /// <param name="addConverters"></param>
    /// <returns></returns>
    public static JsonSerializerOptions Create(JsonSerializerOptions baseOptions, Func<JsonConverter, bool> removeConverterPredicate, params JsonConverter[] addConverters)
    {
        JsonSerializerOptions options = new(baseOptions);
        _ = options.Converters.RemoveAllWhere(removeConverterPredicate);
        _ = options.Converters.AddIfNotContains(addConverters);
        return options;
    }
}
