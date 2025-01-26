#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileResult
// Guid:06be9dd0-9069-48f1-b2e4-8eefa52826b1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 7:00:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Results;

/// <summary>
/// 文件处理结果
/// </summary>
public class FileResult
{
    /// <summary>
    /// 输出的文件流
    /// </summary>
    public required Stream OutputStream { get; set; }

    /// <summary>
    /// 额外元信息
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
