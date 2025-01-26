#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAIBaseOptions
// Guid:737255cb-49b7-42e0-8d00-f7666b117233
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 7:27:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options.Base;

/// <summary>
/// 曦寒 AI 基础配置
/// </summary>
public abstract class XiHanAIBaseOptions
{
    /// <summary>
    /// API Key
    /// </summary>
    public virtual string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 模型
    /// </summary>
    public virtual string Model { get; set; } = string.Empty;
}
