#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TreeExtensions
// Guid:c53a69fe-2e7e-4c1a-a3c4-a94988509e6b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/2 2:44:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.DataFilter.Trees.Dtos;

namespace XiHan.Framework.Utils.DataFilter.Trees;

/// <summary>
/// 树扩展方法
/// </summary>
public static class TreeExtensions
{
    /// <summary>
    /// 转为树形结构
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="isChild"></param>
    /// <returns></returns>
    public static IEnumerable<TreeNodeDto<T>> ToTree<T>(this IEnumerable<T> source, Func<T, T, bool> isChild)
    {
        var nodes = source.Select(value => new TreeNodeDto<T>(value)).ToList();
        foreach (var node in nodes)
        {
            foreach (var child in nodes)
            {
                if (isChild(node.Value, child.Value))
                {
                    node.Children.Add(child);
                }
            }
        }
        return nodes.Where(node => !nodes.Any(n => n.Children.Contains(node)));
    }

    /// <summary>
    /// 深度优先遍历 (DFS)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="root"></param>
    /// <returns></returns>
    public static IEnumerable<TreeNodeDto<T>> DepthFirstTraversal<T>(this TreeNodeDto<T> root)
    {
        if (root == null)
        {
            yield break;
        }

        yield return root;

        foreach (var child in root.Children)
        {
            foreach (var descendant in child.DepthFirstTraversal())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// 广度优先遍历 (BFS)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="root"></param>
    /// <returns></returns>
    public static IEnumerable<TreeNodeDto<T>> BreadthFirstTraversal<T>(this TreeNodeDto<T> root)
    {
        if (root == null)
        {
            yield break;
        }

        var queue = new Queue<TreeNodeDto<T>>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            foreach (var child in current.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// 查找节点 (DFS)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="root"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static TreeNodeDto<T>? FindNode<T>(this TreeNodeDto<T> root, T value)
    {
        return root.DepthFirstTraversal().FirstOrDefault(node => EqualityComparer<T>.Default.Equals(node.Value, value));
    }

    /// <summary>
    /// 获取节点路径
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="root"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static List<TreeNodeDto<T>>? GetPath<T>(this TreeNodeDto<T> root, T value)
    {
        var path = new List<TreeNodeDto<T>>();
        return FindPath(root, value, path) ? path : null;
    }

    /// <summary>
    /// 添加子节点
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="parent"></param>
    /// <param name="value"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void AddChild<T>(this TreeNodeDto<T> parent, T value)
    {
        ArgumentNullException.ThrowIfNull(parent);

        parent.Children.Add(new TreeNodeDto<T>(value));
    }

    /// <summary>
    /// 删除节点
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="root"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool RemoveNode<T>(this TreeNodeDto<T> root, T value)
    {
        if (root == null)
        {
            return false;
        }

        foreach (var child in root.Children.ToList())
        {
            if (EqualityComparer<T>.Default.Equals(child.Value, value))
            {
                _ = root.Children.Remove(child);
                return true;
            }

            if (RemoveNode(child, value))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 获取树的高度
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="root"></param>
    /// <returns></returns>
    public static int GetHeight<T>(this TreeNodeDto<T> root)
    {
        return root == null ? 0 : 1 + root.Children.Select(child => child.GetHeight()).DefaultIfEmpty(0).Max();
    }

    /// <summary>
    /// 获取叶子节点
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="root"></param>
    /// <returns></returns>
    public static IEnumerable<TreeNodeDto<T>> GetLeafNodes<T>(this TreeNodeDto<T> root)
    {
        return root.DepthFirstTraversal().Where(node => node.Children.Count == 0);
    }

    #region 私有方法

    /// <summary>
    /// 查找路径
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="node"></param>
    /// <param name="value"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    private static bool FindPath<T>(TreeNodeDto<T> node, T value, List<TreeNodeDto<T>> path)
    {
        if (node == null)
        {
            return false;
        }

        path.Add(node);

        if (EqualityComparer<T>.Default.Equals(node.Value, value))
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (FindPath(child, value, path))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    #endregion
}
