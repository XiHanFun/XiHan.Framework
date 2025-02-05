#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanHuggingFaceOptions
// Guid:f04e740a-bad9-4bd9-ab65-9e593387098b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/4 17:48:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// XiHanHuggingFaceOptions
/// </summary>
public class XiHanHuggingFaceOptions
{
    /// <summary>
    /// 模型
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// 服务端地址
    /// </summary>
    public string Endpoint { get; set; } = "https://api-inference.huggingface.co";

    /// <summary>
    /// API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 服务标识
    /// </summary>
    public string ServiceId { get; set; } = "HuggingFace";
}
