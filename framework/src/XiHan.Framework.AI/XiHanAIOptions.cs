#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAIOptions
// Guid:dc61c385-5d8c-49e1-a525-94303cabb68e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 7:21:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AI.Options.Base;

namespace XiHan.Framework.AI;

/// <summary>
/// 曦寒框架人工智能模块配置
/// </summary>
public class XiHanAIOptions
{
    /// <summary>
    /// 人工智能模块名称
    /// </summary>
    public string Name { get; set; } = "XiHanAI";

    /// <summary>
    /// OpenAI 配置
    /// </summary>
    public XiHanOpenAIOptions OpenAI { get; set; } = new XiHanOpenAIOptions();

    /// <summary>
    /// DeepSeek 配置
    /// </summary>
    public XiHanDeepSeekOptions DeepSeek { get; set; } = new XiHanDeepSeekOptions();
}
