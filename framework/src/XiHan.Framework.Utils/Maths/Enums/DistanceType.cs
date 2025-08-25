#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DistanceType
// Guid:d06fa815-d63e-48ca-ac57-7f45f33ecd73
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/8 17:06:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Maths.Enums;

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
