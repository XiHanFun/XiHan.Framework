#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OpenAIOptions
// Guid:f6a13682-51cb-46aa-9d64-8054d5552eb4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// OpenAI服务配置
/// </summary>
public class OpenAiOptions
{
    /// <summary>
    /// 服务Id
    /// </summary>
    public string ServiceId { get; set; } = "OpenAI";

    /// <summary>
    /// API密钥
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 组织Id(可选)
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// API基础地址(可选，默认使用官方地址)
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    /// <summary>
    /// 默认模型名称
    /// </summary>
    public string ModelName { get; set; } = "gpt-4";

    /// <summary>
    /// 默认嵌入模型名称
    /// </summary>
    public string EmbeddingModelName { get; set; } = "text-embedding-ada-002";

    /// <summary>
    /// 默认温度
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// 默认最大令牌数
    /// </summary>
    public int MaxTokens { get; set; } = 4000;
}
