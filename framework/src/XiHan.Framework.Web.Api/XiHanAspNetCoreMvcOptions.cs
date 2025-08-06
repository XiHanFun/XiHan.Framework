#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebCoreMvcOptions
// Guid:680e435e-92b7-49bd-bdb2-39981206a474
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/1 17:54:04
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Text.Json.Converters;

namespace XiHan.Framework.Web.Api;

/// <summary>
/// XiHanWebCoreMvcOptions
/// </summary>
public class XiHanWebCoreMvcOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanWebCoreMvcOptions()
    {
        MvcOptions = new MvcOptions();
        ApiBehaviorOptions = new ApiBehaviorOptions();
        JsonOptions = new JsonOptions();
        FormatterOptions = new FormatterMappings();
        CorsOptions = new CorsOptions();
    }

    /// <summary>
    /// Mvc 配置项
    /// </summary>
    public MvcOptions MvcOptions { get; }

    /// <summary>
    /// Api 行为配置项
    /// </summary>
    public ApiBehaviorOptions ApiBehaviorOptions { get; }

    /// <summary>
    /// Json 序列化配置项
    /// </summary>
    public JsonOptions JsonOptions { get; }

    /// <summary>
    /// 格式化 配置项
    /// </summary>
    public FormatterMappings FormatterOptions { get; }

    /// <summary>
    /// Cors 配置项
    /// </summary>
    public CorsOptions CorsOptions { get; }

    /// <summary>
    /// 配置 MvcOptions
    /// </summary>
    /// <param name="configure"></param>
    /// <returns></returns>
    public XiHanWebCoreMvcOptions ConfigureMvcOptions(Action<MvcOptions> configure)
    {
        configure(MvcOptions);
        return this;
    }

    /// <summary>
    /// 配置 ApiBehaviorOptions
    /// </summary>
    /// <param name="configure"></param>
    /// <returns></returns>
    public XiHanWebCoreMvcOptions ConfigureApiBehaviorOptions(Action<ApiBehaviorOptions> configure)
    {
        configure(ApiBehaviorOptions);
        return this;
    }

    /// <summary>
    /// 配置 JsonOptions
    /// </summary>
    /// <param name="configure"></param>
    /// <returns></returns>
    public XiHanWebCoreMvcOptions ConfigureJsonOptions(Action<JsonOptions> configure)
    {
        configure(JsonOptions);
        return this;
    }

    /// <summary>
    /// 配置默认 JsonOptions
    /// </summary>
    /// <returns></returns>
    public XiHanWebCoreMvcOptions ConfigureJsonOptionsDefault()
    {
        // 序列化格式
        JsonOptions.JsonSerializerOptions.WriteIndented = true;
        // 忽略循环引用
        JsonOptions.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        // 数字类型
        JsonOptions.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        // 允许额外符号
        JsonOptions.JsonSerializerOptions.AllowTrailingCommas = true;
        // 注释处理，允许在 JSON 输入中使用注释并忽略它们
        JsonOptions.JsonSerializerOptions.ReadCommentHandling = JsonCommentHandling.Skip;
        // 属性名称使用区分大小写的比较
        JsonOptions.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
        // 数据格式首字母小写 JsonNamingPolicy.CamelCase 驼峰样式，null则为不改变大小写
        JsonOptions.JsonSerializerOptions.PropertyNamingPolicy = null;
        // 获取或设置要在转义字符串时使用的编码器，UnsafeRelaxedJsonEscaping 为不转义字符
        JsonOptions.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        // 允许额外的元数据属性
        JsonOptions.JsonSerializerOptions.AllowOutOfOrderMetadataProperties = true;
        // 允许额外的属性
        JsonOptions.JsonSerializerOptions.IgnoreReadOnlyFields = false;
        // 忽略空值
        JsonOptions.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

        // 枚举类型
        JsonOptions.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // 布尔类型
        JsonOptions.JsonSerializerOptions.Converters.Add(new BooleanJsonConverter());
        // 数字类型
        JsonOptions.JsonSerializerOptions.Converters.Add(new IntJsonConverter());
        JsonOptions.JsonSerializerOptions.Converters.Add(new LongJsonConverter());
        JsonOptions.JsonSerializerOptions.Converters.Add(new DecimalJsonConverter());
        // 时间类型
        JsonOptions.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
        JsonOptions.JsonSerializerOptions.Converters.Add(new TimeOnlyNullableConverter());
        // 日期类型
        JsonOptions.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
        JsonOptions.JsonSerializerOptions.Converters.Add(new DateOnlyNullableConverter());
        JsonOptions.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());
        JsonOptions.JsonSerializerOptions.Converters.Add(new DateTimeNullableConverter());
        JsonOptions.JsonSerializerOptions.Converters.Add(new DateTimeOffsetJsonConverter());
        JsonOptions.JsonSerializerOptions.Converters.Add(new DateTimeOffsetNullableConverter());

        return this;
    }

    /// <summary>
    /// 配置 FormatterOptions
    /// </summary>
    /// <param name="configure"></param>
    /// <returns></returns>
    public XiHanWebCoreMvcOptions ConfigureFormatterOptions(Action<FormatterMappings> configure)
    {
        configure(FormatterOptions);
        return this;
    }

    /// <summary>
    /// 配置 CorsOptions
    /// </summary>
    /// <param name="configure"></param>
    /// <returns></returns>
    public XiHanWebCoreMvcOptions ConfigureCorsOptions(Action<CorsOptions> configure)
    {
        configure(CorsOptions);
        return this;
    }
}
