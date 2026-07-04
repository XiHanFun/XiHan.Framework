#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AiProviderOptions
// Guid:a1b2c3d4-e5f6-4a01-9c01-0a0b0c0d0e01
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Abstractions.Configuration;

/// <summary>
/// AI Provider 配置（单个 provider 一份：密钥/端点/模型/采样等）
/// </summary>
/// <remarks>
/// 多数 provider（OpenAI/DeepSeek/Ollama/vLLM/自训模型）走 OpenAI 兼容协议：
/// 设 BaseUrl 指向对应端点 + Model + ApiKey 即可，一个适配器覆盖。
/// </remarks>
public class AiProviderOptions
{
    /// <summary>
    /// Provider 名称（如 OpenAI/Claude/DeepSeek/Ollama/Custom；多 provider 解析用）
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// API 密钥（store 化时加密落库、读侧解密）
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 端点基址（OpenAI 兼容；指向 Ollama/vLLM/DeepSeek 等，空则用 provider 默认端点）
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// 模型名（会话模型）
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 嵌入模型名（RAG 用；与会话模型同端点同密钥、仅模型 id 不同，如 text-embedding-3-small；空则该 provider 不支持嵌入）
    /// </summary>
    public string? EmbeddingModel { get; set; }

    /// <summary>
    /// 最大输出 token（空则用模型默认）
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// 采样温度（空则用模型默认）
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// 请求超时秒（空则用默认）
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// 扩展参数 JSON（新 provider 专属参数，免改配置结构）
    /// </summary>
    public string? ExtraJson { get; set; }
}
