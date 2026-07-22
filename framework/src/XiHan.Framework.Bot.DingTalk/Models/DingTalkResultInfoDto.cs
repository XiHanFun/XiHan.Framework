// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Bot.DingTalk.Models;

/// <summary>
/// 结果信息
/// </summary>
public record DingTalkResultInfoDto
{
    /// <summary>
    /// 结果代码
    /// </summary>
    [JsonPropertyName("errcode")]
    public int ErrCode { get; set; }

    /// <summary>
    /// 结果消息
    /// </summary>
    [JsonPropertyName("errmsg")]
    public string ErrMsg { get; set; } = string.Empty;
}
