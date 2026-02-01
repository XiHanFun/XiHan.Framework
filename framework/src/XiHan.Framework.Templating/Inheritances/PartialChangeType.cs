#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PartialChangeType
// Guid:ff838d08-9c3e-4613-a6f5-d4e55e6a83ad
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:15:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Inheritances;

/// <summary>
/// 片段变化类型
/// </summary>
public enum PartialChangeType
{
    /// <summary>
    /// 创建
    /// </summary>
    Created,

    /// <summary>
    /// 修改
    /// </summary>
    Modified,

    /// <summary>
    /// 删除
    /// </summary>
    Deleted,

    /// <summary>
    /// 重命名
    /// </summary>
    Renamed
}
