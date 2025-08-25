#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AccuracyMetrics
// Guid:78cf40ca-1ae2-4c60-a006-31353d859226
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/8 17:08:04
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Maths.Dtos;

/// <summary>
/// 预测准确性指标
/// </summary>
public class AccuracyMetrics
{
    /// <summary>
    /// 平均绝对误差 (Mean Absolute Error)
    /// </summary>
    public double Mae { get; set; }

    /// <summary>
    /// 均方误差 (Mean Squared Error)
    /// </summary>
    public double Mse { get; set; }

    /// <summary>
    /// 均方根误差 (Root Mean Squared Error)
    /// </summary>
    public double Rmse { get; set; }

    /// <summary>
    /// 平均绝对百分比误差 (Mean Absolute Percentage Error)
    /// </summary>
    public double Mape { get; set; }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    /// <returns>格式化的准确性指标</returns>
    public override string ToString()
    {
        return $"MAE: {Mae:F4}, MSE: {Mse:F4}, RMSE: {Rmse:F4}, MAPE: {Mape:F2}%";
    }
}
