#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LarkResultInfoDto
// Guid:ae851ef8-8bde-452b-a0d0-e5cb2ad8c6cc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 01:08:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json.Serialization;

namespace XiHan.Framework.Bot.Providers.Lark;

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
