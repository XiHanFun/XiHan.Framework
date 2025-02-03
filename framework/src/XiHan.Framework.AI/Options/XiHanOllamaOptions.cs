#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanOllamaOptions
// Guid:0ff36526-6a2d-4988-9027-bf0a1dc40327
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 7:28:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// 曦寒 Ollama 配置
/// </summary>
public class XiHanOllamaOptions
{
    /// <summary>
    /// 模型
    /// </summary>
    public string ModelId { get; set; } = "deepseek-r1";

    /// <summary>
    /// 服务端地址
    /// </summary>
    public string Endpoint { get; set; } = "http://127.0.0.1:11434";

    /// <summary>
    /// 服务标识
    /// </summary>
    public string ServiceId { get; set; } = "Ollama";
}
