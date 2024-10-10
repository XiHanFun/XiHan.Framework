#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2022 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SerializeHelper
// Guid:71a6060f-74fa-4e1d-846d-2ec166b3ed78
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-09-07 上午 03:00:55
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Utils.Serializes.Converters;

namespace XiHan.Framework.Utils.Serializes;

/// <summary>
/// 序列化帮助类
/// </summary>
public static class SerializeHelper
{
    /// <summary>
    /// 公共参数
    /// </summary>
    public static JsonSerializerOptions JsonSerializerOptionsInstance { get; set; } = GetJsonSerializerOptions();

    /// <summary>
    /// 获取默认序列化参数
    /// </summary>
    /// <returns></returns>
    private static JsonSerializerOptions GetJsonSerializerOptions()
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
            // 数据格式首字母小写 JsonNamingPolicy.CamelCase驼峰样式，null则为不改变大小写
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
    /// 序列化为Json
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public static string SerializeTo(this object item)
    {
        return JsonSerializer.Serialize(item, JsonSerializerOptionsInstance);
    }

    /// <summary>
    /// 反序列化为对象
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="jsonString"></param>
    /// <returns></returns>
    public static TEntity? DeserializeTo<TEntity>(this string jsonString)
    {
        return JsonSerializer.Deserialize<TEntity>(jsonString, JsonSerializerOptionsInstance);
    }

    /// <summary>
    /// 反序列化为对象
    /// </summary>
    /// <param name="jsonString"></param>
    /// <returns></returns>
    public static object? DeserializeTo(this string jsonString)
    {
        return JsonSerializer.Deserialize(jsonString.ToStream(), JsonTypeInfo.CreateJsonTypeInfo(typeof(object), JsonSerializerOptionsInstance));
    }
}