// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO.Enumeration;

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 初始化选取的公共判定
/// </summary>
internal static class DbInitializationFilters
{
    /// <summary>
    /// 判断分组是否被包含/排除名单放行
    /// </summary>
    /// <param name="group">分组名称，未标注分组为 null</param>
    /// <param name="includedGroups">包含分组名单，为空表示不限</param>
    /// <param name="excludedGroups">排除分组名单</param>
    /// <returns>放行返回 true</returns>
    public static bool IsGroupAllowed(string? group, List<string> includedGroups, List<string> excludedGroups)
    {
        if (includedGroups.Count > 0 &&
            (group is null || !includedGroups.Any(item => string.Equals(item?.Trim(), group, StringComparison.OrdinalIgnoreCase))))
        {
            return false;
        }

        return group is null ||
               !excludedGroups.Any(item => string.Equals(item?.Trim(), group, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 判断候选名称中是否有任一命中名单（支持 <c>*</c>、<c>?</c> 通配，忽略大小写）
    /// </summary>
    /// <param name="patterns">名称/通配模式名单</param>
    /// <param name="names">候选名称</param>
    /// <returns>命中返回 true</returns>
    public static bool MatchesAny(List<string> patterns, params string?[] names)
    {
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            foreach (var name in names)
            {
                if (!string.IsNullOrWhiteSpace(name) &&
                    FileSystemName.MatchesSimpleExpression(pattern.Trim(), name, ignoreCase: true))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 判断声明的目标库是否覆盖当前库
    /// </summary>
    /// <param name="declaredTarget">特性声明的目标库</param>
    /// <param name="context">当前库上下文</param>
    /// <returns>覆盖返回 true</returns>
    public static bool IsTargetAllowed(DbInitializationTarget declaredTarget, DbInitializationContext context)
    {
        return (declaredTarget & context.Target) != 0;
    }

    /// <summary>
    /// 判断声明的连接名单是否覆盖当前连接，名单为空或当前连接标识未知时一律放行
    /// </summary>
    /// <param name="declaredConnectionConfigIds">特性声明的连接配置标识名单</param>
    /// <param name="context">当前库上下文</param>
    /// <returns>覆盖返回 true</returns>
    public static bool IsConnectionAllowed(string[] declaredConnectionConfigIds, DbInitializationContext context)
    {
        if (declaredConnectionConfigIds.Length == 0 || string.IsNullOrWhiteSpace(context.ConnectionConfigId))
        {
            return true;
        }

        return declaredConnectionConfigIds.Any(
            configId => string.Equals(configId?.Trim(), context.ConnectionConfigId, StringComparison.OrdinalIgnoreCase));
    }
}
