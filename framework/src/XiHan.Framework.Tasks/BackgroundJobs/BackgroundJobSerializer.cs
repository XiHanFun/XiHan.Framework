// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

namespace XiHan.Framework.Tasks.BackgroundJobs;

/// <summary>
/// 基于 System.Text.Json 的后台作业参数序列化器
/// </summary>
public class BackgroundJobSerializer : IBackgroundJobSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,

        // 原来只设了 WriteIndented（本来就是默认值），漏掉了编码器，于是这里用的是 System.Text.Json 的
        // 默认严格编码器：非 ASCII 一律转义，中文"订单"落库后是两段 uXXXX 转义序列。本仓所有 JSON 出口
        // （Utils 的 JsonSerializeOptions/JsonDeserializeOptions/JsonHelper、Serialization 的 DynamicJson*、
        // Web.Api 的 MVC JsonOptions）都统一显式用 UnsafeRelaxedJsonEscaping，唯独这里漏配。
        // 后果不是数据错误（转义序列可无损还原），而是作业参数在库里/Redis 里全是 \uXXXX，
        // 运维排查队列时读不了，中文载荷体积还翻三倍。此处补齐以对齐全仓口径。
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 序列化
    /// </summary>
    /// <param name="obj">对象</param>
    /// <returns>序列化字符串</returns>
    public string Serialize(object obj)
    {
        return JsonSerializer.Serialize(obj, SerializerOptions);
    }

    /// <summary>
    /// 反序列化
    /// </summary>
    /// <param name="value">序列化字符串</param>
    /// <param name="type">目标类型</param>
    /// <returns>对象</returns>
    public object Deserialize(string value, Type type)
    {
        return JsonSerializer.Deserialize(value, type, SerializerOptions)
            ?? throw new InvalidOperationException($"反序列化后台作业参数失败，目标类型：{type.FullName}");
    }
}
