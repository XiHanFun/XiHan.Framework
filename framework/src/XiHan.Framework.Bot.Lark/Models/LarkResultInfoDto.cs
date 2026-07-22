// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Bot.Lark.Models;

/// <summary>
/// 结果信息
/// </summary>
public record LarkResultInfoDto
{
    /// <summary>
    /// 结果代码
    /// 成功 0
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// 结果消息
    /// 成功 success
    /// </summary>
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = string.Empty;

    /// <summary>
    /// 数据
    /// </summary>
    [JsonPropertyName("data")]
    public object Data { get; set; } = new();
}
