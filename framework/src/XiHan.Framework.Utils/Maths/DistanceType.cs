#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DistanceType
// Guid:cf1bd6f2-798c-4bfd-a761-a0aa63394b1f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 2:45:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Maths;

/// <summary>
/// 距离计算类型枚举
/// </summary>
public enum DistanceType
{
    /// <summary>
    /// 欧几里得距离
    /// </summary>
    Euclidean,

    /// <summary>
    /// 曼哈顿距离
    /// </summary>
    Manhattan,

    /// <summary>
    /// 切比雪夫距离
    /// </summary>
    Chebyshev
}
