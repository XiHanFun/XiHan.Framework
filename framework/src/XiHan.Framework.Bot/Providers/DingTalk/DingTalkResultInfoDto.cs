#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DingTalkResultInfoDto
// Guid:c6e6bf2a-d8d5-40f3-8834-019cc8ae4b28
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json.Serialization;

namespace XiHan.Framework.Bot.Providers.DingTalk;

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
