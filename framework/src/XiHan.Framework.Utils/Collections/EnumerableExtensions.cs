#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumerableExtensions
// Guid:3d50f5ab-2bbb-4643-a8bc-03e137b0428f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/22 2:33:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Collections;

/// <summary>
/// 可列举扩展方法
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// 使用指定的分隔符连接构造的 <see cref="IEnumerable{T}"/> 集合(类型为 System.String)的成员
    /// 这是 string.Join(...) 的快捷方式
    /// </summary>
    /// <param name="source">包含要连接的字符串的集合</param>
    /// <param name="separator">要用作分隔符的字符串只有当 values 有多个元素时，separator 才会包含在返回的字符串中</param>
    /// <returns>由 values 的成员组成的字符串，这些成员由 separator 字符串分隔。如果 values 没有成员，则方法返回 System.String.Empty</returns>
    public static string JoinAsString(this IEnumerable<string> source, string separator)
    {
        return string.Join(separator, source);
    }

    /// <summary>
    /// 使用指定的分隔符连接集合的成员
    /// 这是 string.Join(...) 的快捷方式
    /// </summary>
    /// <param name="source">包含要连接的对象的集合</param>
    /// <param name="separator">要用作分隔符的字符串只有当 values 有多个元素时，separator 才会包含在返回的字符串中</param>
    /// <typeparam name="T">values 成员的类型</typeparam>
    /// <returns>由 values 的成员组成的字符串，这些成员由 separator 字符串分隔如果 values 没有成员，则方法返回 System.String.Empty</returns>
    public static string JoinAsString<T>(this IEnumerable<T> source, string separator)
    {
        return string.Join(separator, source);
    }

    /// <summary>
    /// 如果给定的条件为真，则使用给定的谓词对 <see cref="IEnumerable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的枚举对象</param>
    /// <param name="condition">第三方条件</param>
    /// <param name="predicate">用于选择枚举对象的谓词</param>
    /// <returns>基于 <paramref name="condition"/> 的选择或未选择的枚举对象</returns>
    public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> source, bool condition, Func<T, bool> predicate)
    {
        return condition ? source.Where(predicate) : source;
    }

    /// <summary>
    /// 如果给定的条件为真，则使用给定的谓词对 <see cref="IEnumerable{T}"/> 进行选择
    /// </summary>
    /// <param name="source">要应用选择的枚举对象</param>
    /// <param name="condition">第三方条件</param>
    /// <param name="predicate">用于选择枚举对象的谓词，包含索引</param>
    /// <returns>基于 <paramref name="condition"/> 的选择或未选择的枚举对象</returns>
    public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> source, bool condition, Func<T, int, bool> predicate)
    {
        return condition ? source.Where(predicate) : source;
    }

    /// <summary>
    /// 从列表中随机获取一个元素
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <returns>随机选中的元素</returns>
    /// <exception cref="ArgumentException">当列表为空时抛出异常</exception>
    public static T GetRandom<T>(this IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        if (!source.Any())
        {
            throw new ArgumentException("列表不能为空", nameof(source));
        }

        var randomIndex = Random.Shared.Next(source.Count());
        return source.ElementAt(randomIndex);
    }

    /// <summary>
    /// 通过考虑对象之间的依赖关系对列表进行拓扑排序
    /// </summary>
    /// <typeparam name="T">列表项的类型</typeparam>
    /// <param name="source">要排序的对象列表</param>
    /// <param name="getDependencies">用于解析项依赖关系的函数</param>
    /// <param name="comparer">依赖关系的相等比较器</param>
    /// <returns>返回按依赖关系排序的新列表</returns>
    public static List<T> SortByDependencies<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> getDependencies, IEqualityComparer<T>? comparer = null)
        where T : notnull
    {
        // 初始化排序列表、访问标记字典
        List<T> sorted = [];
        Dictionary<T, bool> visited = new(comparer);

        // 遍历源列表中的每个项并进行拓扑排序
        foreach (var item in source)
        {
            SortByDependenciesVisit(item, getDependencies, sorted, visited);
        }

        return sorted;
    }

    /// <summary>
    /// 递归地对项进行拓扑排序，考虑其依赖关系
    /// </summary>
    /// <typeparam name="T">项的类型</typeparam>
    /// <param name="item">要解析的项</param>
    /// <param name="getDependencies">用于解析项依赖关系的函数</param>
    /// <param name="sorted">包含排序后项的列表</param>
    /// <param name="visited">包含已访问项的字典</param>
    private static void SortByDependenciesVisit<T>(T item, Func<T, IEnumerable<T>> getDependencies, List<T> sorted, Dictionary<T, bool> visited)
        where T : notnull
    {
        // 检查项是否已经在处理中或已访问过
        var alreadyVisited = visited.TryGetValue(item, out var inProcess);

        if (alreadyVisited)
        {
            if (inProcess)
            {
                throw new ArgumentException("发现循环依赖项:" + item);
            }
        }
        else
        {
            // 标记为正在处理
            visited[item] = true;

            var dependencies = getDependencies(item);
            // 递归地对每个依赖进行拓扑排序
            foreach (var dependency in dependencies)
            {
                SortByDependenciesVisit(dependency, getDependencies, sorted, visited);
            }

            // 标记为已处理
            visited[item] = false;
            sorted.Add(item);
        }
    }
}
