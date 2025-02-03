#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanOpenAIOptions
// Guid:08182ea3-ea3c-4a5b-8c7c-ba3362097645
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 7:28:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// 曦寒 OpenAI 配置
/// </summary>
public class XiHanOpenAIOptions
{
    /// <summary>
    /// 模型
    /// </summary>
    public string ModelId { get; set; } = "gpt-4o";

    /// <summary>
    /// 服务端地址
    /// </summary>
    public string Endpoint { get; set; } = "https://api.openai.com";

    /// <summary>
    /// API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 服务标识
    /// </summary>
    public string ServiceId { get; set; } = "OpenAI";
}
