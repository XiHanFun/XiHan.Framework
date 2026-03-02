#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiSwaggerGroupHelper
// Guid:3d1b6f93-5a9b-4a8c-9af5-30f4e7f6c4e2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Contracts.Services;

namespace XiHan.Framework.Web.Docs.Swagger;

/// <summary>
/// 动态 API Swagger 分组辅助
/// </summary>
internal static class DynamicApiSwaggerGroupHelper
{
    /// <summary>
    /// 默认文档名称
    /// </summary>
    internal const string DefaultDocName = "v1";

    /// <summary>
    /// 默认文档标题
    /// </summary>
    internal const string DefaultDocTitle = "API V1";

    /// <summary>
    /// 获取分组名称列表
    /// </summary>
    internal static IReadOnlyList<string> GetGroupNames(IApiDescriptionGroupCollectionProvider provider)
    {
        return provider.ApiDescriptionGroups.Items
            .Select(group => group.GroupName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 从特性扫描分组名称
    /// </summary>
    internal static IReadOnlyList<string> GetGroupNamesFromAttributes()
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            if (assembly.IsDynamic || IsSystemAssembly(assembly))
            {
                continue;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(type => type != null).Cast<Type>().ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var type in types)
            {
                if (!typeof(IApplicationService).IsAssignableFrom(type))
                {
                    continue;
                }

                CollectGroupNames(type, groups);
            }
        }

        return groups
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CollectGroupNames(Type type, ISet<string> groups)
    {
        foreach (var attr in type.GetCustomAttributes<DynamicApiAttribute>())
        {
            if (!attr.IsEnabled || !attr.VisibleInApiExplorer)
            {
                continue;
            }

            AddGroupName(attr.Group, groups);
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var attr in method.GetCustomAttributes<DynamicApiAttribute>())
            {
                if (!attr.IsEnabled || !attr.VisibleInApiExplorer)
                {
                    continue;
                }

                AddGroupName(attr.Group, groups);
            }
        }
    }

    private static void AddGroupName(string? groupName, ISet<string> groups)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return;
        }

        groups.Add(groupName.Trim());
    }

    private static bool IsSystemAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? string.Empty;

        return name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Serilog", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Newtonsoft", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("System", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("netstandard", StringComparison.OrdinalIgnoreCase);
    }
}
