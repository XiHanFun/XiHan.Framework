#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDeepSeekOptions
// Guid:0ff36526-6a2d-4988-9027-bf0a1dc40327
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 7:28:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options.Base;

/// <summary>
/// 曦寒 DeepSeek 配置
/// </summary>
public class XiHanDeepSeekOptions : XiHanAIBaseOptions
{
    /// <summary>
    /// API Key
    /// </summary>
    public override string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 模型
    /// </summary>
    public override string Model { get; set; } = "deepseek-r1";
}
