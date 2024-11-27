#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SortDirectionEnum
// Guid:515da136-9391-4bad-b892-4131478583fd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 6:35:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;

namespace XiHan.Framework.Utils.DataFilter.Enums;

/// <summary>
/// 排序方向枚举
/// </summary>
public enum SortDirectionEnum
{
    /// <summary>
    /// 升序
    /// </summary>
    [Description("升序")]
    Asc,

    /// <summary>
    /// 降序
    /// </summary>
    [Description("降序")]
    Desc
}
