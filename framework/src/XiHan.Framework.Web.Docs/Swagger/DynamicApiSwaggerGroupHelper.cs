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
using XiHan.Framework.Web.Api.DynamicApi.Helpers;

namespace XiHan.Framework.Web.Docs.Swagger;

/// <summary>
/// 动态 API 文档分组辅助
/// </summary>
internal static class DynamicApiSwaggerGroupHelper
{
    internal sealed class DynamicApiDocGroupDefinition
    {
        public string Group { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int Order { get; init; }
    }

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
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 从特性扫描分组定义
    /// </summary>
    internal static IReadOnlyList<DynamicApiDocGroupDefinition> GetGroupDefinitionsFromAttributes()
    {
        var groupDefinitions = new Dictionary<string, DynamicApiDocGroupDefinition>(StringComparer.OrdinalIgnoreCase);
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

                CollectGroupDefinitions(type, groupDefinitions);
            }
        }

        return groupDefinitions.Values
            .OrderBy(definition => definition.Order)
            .ThenBy(definition => definition.Group, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 从特性扫描分组名称（仅文档键）
    /// </summary>
    internal static IReadOnlyList<string> GetGroupNamesFromAttributes()
    {
        return GetGroupDefinitionsFromAttributes()
            .Select(definition => definition.Group)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CollectGroupDefinitions(
        Type type,
        IDictionary<string, DynamicApiDocGroupDefinition> groupDefinitions)
    {
        var classAttributes = DynamicApiAttributeMergeHelper.GetOrderedClassAttributes(type);
        AddGroupDefinitions(classAttributes, groupDefinitions);

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var methodAttributes = DynamicApiAttributeMergeHelper.GetOrderedMethodAttributes(method);
            AddGroupDefinitions(methodAttributes, groupDefinitions);
        }
    }

    private static void AddGroupDefinitions(
        IEnumerable<DynamicApiAttribute> attributes,
        IDictionary<string, DynamicApiDocGroupDefinition> groupDefinitions)
    {
        foreach (var attribute in attributes)
        {
            if (!attribute.IsEnabled || !attribute.VisibleInApiExplorer)
            {
                continue;
            }

            var group = attribute.Group?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(group))
            {
                continue;
            }

            var displayName = string.IsNullOrWhiteSpace(attribute.GroupName)
                ? group
                : attribute.GroupName.Trim();

            if (!groupDefinitions.TryGetValue(group, out var current))
            {
                groupDefinitions[group] = new DynamicApiDocGroupDefinition
                {
                    Group = group,
                    DisplayName = displayName,
                    Order = attribute.Order
                };
                continue;
            }

            var shouldOverrideByOrder = attribute.Order > current.Order;
            var shouldFillDisplayName =
                !string.IsNullOrWhiteSpace(attribute.GroupName) &&
                string.Equals(current.DisplayName, current.Group, StringComparison.OrdinalIgnoreCase);
            if (!shouldOverrideByOrder && !shouldFillDisplayName)
            {
                continue;
            }

            groupDefinitions[group] = new DynamicApiDocGroupDefinition
            {
                Group = group,
                DisplayName = shouldFillDisplayName && !shouldOverrideByOrder
                    ? attribute.GroupName.Trim()
                    : displayName,
                Order = shouldOverrideByOrder ? attribute.Order : current.Order
            };
        }
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
