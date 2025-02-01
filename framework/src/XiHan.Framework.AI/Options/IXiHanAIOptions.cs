#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanAIOptions
// Guid:737255cb-49b7-42e0-8d00-f7666b117233
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 7:27:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Options;

/// <summary>
/// 曦寒 AI 配置接口
/// </summary>
public interface IXiHanAIOptions
{
    /// <summary>
    /// API Key
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// 模型
    /// </summary>
    public string Model { get; set; }
}
