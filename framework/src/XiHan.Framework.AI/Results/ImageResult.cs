#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ImageResult
// Guid:8f0f49dd-d203-412a-ac10-1d6e7a9704bc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/27 6:52:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Results;

/// <summary>
/// 图片处理结果
/// </summary>
public class ImageResult
{
    /// <summary>
    /// 处理后的图片数据
    /// </summary>
    public byte[] OutputImage { get; set; } = [];

    /// <summary>
    /// 附加元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}
