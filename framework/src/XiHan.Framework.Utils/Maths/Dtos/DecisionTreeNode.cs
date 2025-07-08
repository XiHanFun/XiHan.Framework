#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DecisionTreeNode
// Guid:4286f69e-9fca-4d10-a789-3d7a80e61c89
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/8 17:07:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Maths.Dtos;

/// <summary>
/// 决策树节点
/// </summary>
public class DecisionTreeNode
{
    /// <summary>
    /// 节点预测值
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// 分割特征索引
    /// </summary>
    public int FeatureIndex { get; set; } = -1;

    /// <summary>
    /// 分割阈值
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// 左子树
    /// </summary>
    public DecisionTreeNode? Left { get; set; }

    /// <summary>
    /// 右子树
    /// </summary>
    public DecisionTreeNode? Right { get; set; }

    /// <summary>
    /// 判断是否为叶子节点
    /// </summary>
    public bool IsLeaf => Left == null && Right == null;
}
