// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;

namespace XiHan.Framework.Domain.Shared.Paging.Enums;

/// <summary>
/// 排序方向
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// 升序
    /// </summary>
    [Description("升序")]
    Ascending = 1000,

    /// <summary>
    /// 降序
    /// </summary>
    [Description("降序")]
    Descending = 1001
}
