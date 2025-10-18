#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSequentialGuidGeneratorOptions
// Guid:b9e783a0-b90d-4e94-b703-eae69c041fdf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 5:38:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DistributedIds.Guids;

/// <summary>
/// 顺序 GUID 选项
/// </summary>
public class SequentialGuidOptions
{
    /// <summary>
    /// 默认顺序 GUID 类型
    /// </summary>
    public SequentialGuidType? DefaultSequentialGuidType { get; set; }

    /// <summary>
    /// 获取默认顺序 GUID 类型
    /// </summary>
    public SequentialGuidType GetDefaultSequentialGuidType() => DefaultSequentialGuidType ?? SequentialGuidType.SequentialAtEnd;
}
