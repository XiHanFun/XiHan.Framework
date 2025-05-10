#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OllamaOptions
// Guid:5e860c8d-edb2-4a6a-9360-894a1fc562a1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// Ollama服务配置
/// </summary>
public class OllamaOptions
{
    /// <summary>
    /// 服务ID
    /// </summary>
    public string ServiceId { get; set; } = "Ollama";

    /// <summary>
    /// API基础地址
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// 默认模型名称
    /// </summary>
    public string ModelName { get; set; } = "llama3";

    /// <summary>
    /// 默认嵌入模型名称(如果支持)
    /// </summary>
    public string? EmbeddingModelName { get; set; }

    /// <summary>
    /// 默认温度
    /// </summary>
    public float Temperature { get; set; } = 0.8f;

    /// <summary>
    /// 默认最大令牌数
    /// </summary>
    public int MaxTokens { get; set; } = 2048;
}
