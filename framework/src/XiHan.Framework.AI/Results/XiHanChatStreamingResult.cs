#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanChatStreamingResult
// Guid:7091834d-1feb-47e3-b20b-767a42f08bd5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Results;

/// <summary>
/// 流式聊天结果
/// </summary>
public class XiHanChatStreamingResult
{
    /// <summary>
    /// 是否结束
    /// </summary>
    public bool IsEnd { get; set; }

    /// <summary>
    /// 内容片段
    /// </summary>
    public string ContentDelta { get; set; } = string.Empty;

    /// <summary>
    /// 完整内容(累积)
    /// </summary>
    public string ContentSoFar { get; set; } = string.Empty;
}
