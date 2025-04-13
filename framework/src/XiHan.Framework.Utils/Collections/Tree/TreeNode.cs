#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TreeNodeDto
// Guid:9438d2f5-dfb8-46c7-b3b8-7e11dde63be8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/2 2:37:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Collections.Tree;

/// <summary>
/// 树节点数据传输对象
/// </summary>
/// <typeparam name="T"></typeparam>
public class TreeNode<T>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value"></param>
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
