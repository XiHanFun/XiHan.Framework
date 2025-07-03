#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TreeNode
// Guid:f055cf1c-8a82-4378-83f1-1940fcdaf523
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 3:36:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Collections.Dtos;

/// <summary>
/// 树节点数据传输对象
/// </summary>
/// <typeparam name="T"></typeparam>
public class TreeNode<T>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">节点值</param>
    public TreeNode(T value)
    {
        Value = value;
    }

    /// <summary>
    /// 节点值
    /// </summary>
    public T Value { get; set; }

    /// <summary>
    /// 子节点
    /// </summary>
    public List<TreeNode<T>> Children { get; set; } = [];
}
