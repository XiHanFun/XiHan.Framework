#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AIOptions
// Guid:99854915-8507-418c-937b-b3331dde1f6a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// AI服务基础配置
/// </summary>
public class AIOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:AI";

    /// <summary>
    /// 默认服务提供商名称
    /// </summary>
    public string DefaultProvider { get; set; } = "OpenAI";

    /// <summary>
    /// 是否启用调试模式
    /// </summary>
    public bool EnableDebug { get; set; } = false;

    /// <summary>
    /// 超时时间(毫秒)
    /// </summary>
    public int TimeoutMs { get; set; } = 60000;
}
