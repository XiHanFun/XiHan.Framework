// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Serialization.Json;
using XiHan.Framework.Utils.Serialization.Json.Converters;

namespace XiHan.Framework.Web.Api;

/// <summary>
/// XiHanWebCoreMvcOptions
/// </summary>
public class XiHanWebCoreMvcOptions
{
    /// <summary>
    /// 共享 HttpContext 访问器：HttpContextAccessor 内部为静态 AsyncLocal，任意实例均读取当前请求上下文
    /// （依赖 AddHttpContextAccessor 已注册，由 Web.Core 提供）。
    /// </summary>
    private static readonly IHttpContextAccessor HttpContextAccessor = new HttpContextAccessor();

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
        JsonOptions.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        JsonOptions.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        // 获取或设置要在转义字符串时使用的编码器，UnsafeRelaxedJsonEscaping 为不转义字符
        JsonOptions.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        // 允许额外的元数据属性
        JsonOptions.JsonSerializerOptions.AllowOutOfOrderMetadataProperties = true;
        // 允许额外的属性
        JsonOptions.JsonSerializerOptions.IgnoreReadOnlyFields = false;
        // 忽略空值
        JsonOptions.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

        // 用户时区感知 DateTime / DateTimeOffset：按请求头 X-Timezone 把时间换算为用户本地时间后输出。
        // 须先于默认转换器加入以取得优先（System.Text.Json 取首个匹配类型的转换器）。
        // 注意：审计/业务时间多为 DateTimeOffset，必须一并覆盖，否则换时区无效（其默认带偏移，前端按浏览器时区渲染）。
        JsonOptions.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter(ResolveUserTimeZone));
        JsonOptions.JsonSerializerOptions.Converters.Add(new DateTimeNullableConverter(ResolveUserTimeZone));
        JsonOptions.JsonSerializerOptions.Converters.Add(new DateTimeOffsetJsonConverter(ResolveUserTimeZone));
        JsonOptions.JsonSerializerOptions.Converters.Add(new DateTimeOffsetNullableConverter(ResolveUserTimeZone));
        // 枚举类型
        JsonOptions.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        JsonOptions.JsonSerializerOptions.ConfigureConverters();

        return this;
    }

    /// <summary>
    /// 解析当前请求的用户时区（请求头 X-Timezone，IANA 标识，如 Asia/Shanghai）；无请求或无头时返回 null。
    /// </summary>
    /// <returns>IANA 时区标识或 null</returns>
    private static string? ResolveUserTimeZone()
    {
        return HttpContextAccessor.HttpContext?.Request.Headers["X-Timezone"].ToString();
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
