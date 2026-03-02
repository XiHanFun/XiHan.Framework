#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DynamicApiAttributeMergeHelper
// Guid:6bb045de-2f1f-4a13-8c9a-32b08f4c42a1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/03 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Application.Attributes;

namespace XiHan.Framework.Web.Api.DynamicApi.Helpers;

/// <summary>
/// DynamicApiAttribute 合并辅助
/// 统一约定：配置默认值 &lt; 类级特性 &lt; 方法级特性；同层级按 Order 升序合并，后写入覆盖先写入。
/// 仅做同名属性合并，不做跨属性兜底。
/// </summary>
public static class DynamicApiAttributeMergeHelper
{
    /// <summary>
    /// 获取类级特性（已按 Order 排序）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <returns>按 Order 排序的类级特性列表</returns>
    public static IReadOnlyList<DynamicApiAttribute> GetOrderedClassAttributes(Type serviceType)
    {
        return OrderAttributes(serviceType.GetCustomAttributes<DynamicApiAttribute>());
    }

    /// <summary>
    /// 获取方法级特性（已按 Order 排序）
    /// </summary>
    /// <param name="methodInfo">方法信息</param>
    /// <returns>按 Order 排序的方法级特性列表</returns>
    public static IReadOnlyList<DynamicApiAttribute> GetOrderedMethodAttributes(MethodInfo methodInfo)
    {
        return OrderAttributes(methodInfo.GetCustomAttributes<DynamicApiAttribute>());
    }

    /// <summary>
    /// 是否启用动态 API（任一层级存在 IsEnabled=false 即禁用）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodInfo">方法信息</param>
    /// <returns>是否启用动态 API</returns>
    public static bool IsEnabled(Type serviceType, MethodInfo? methodInfo = null)
    {
        var classEnabled = GetOrderedClassAttributes(serviceType).All(attribute => attribute.IsEnabled);
        if (!classEnabled)
        {
            return false;
        }

        if (methodInfo == null)
        {
            return true;
        }

        return GetOrderedMethodAttributes(methodInfo).All(attribute => attribute.IsEnabled);
    }

    /// <summary>
    /// 解析路由模板（方法优先，类兜底，单值覆盖）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodInfo">方法信息</param>
    /// <returns>解析后的路由模板</returns>
    public static string? ResolveRouteTemplate(Type serviceType, MethodInfo? methodInfo = null)
    {
        return ResolveStringFromMethodThenClass(serviceType, methodInfo, attribute => attribute.RouteTemplate);
    }

    /// <summary>
    /// 解析 API 描述（方法优先，类兜底，单值覆盖）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodInfo">方法信息</param>
    /// <returns>解析后的 API 描述</returns>
    public static string? ResolveDescription(Type serviceType, MethodInfo? methodInfo = null)
    {
        return ResolveStringFromMethodThenClass(serviceType, methodInfo, attribute => attribute.Description);
    }

    /// <summary>
    /// 解析 API 版本（方法优先，类兜底，单值覆盖）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodInfo">方法信息</param>
    /// <returns>解析后的 API 版本</returns>
    public static string? ResolveVersion(Type serviceType, MethodInfo? methodInfo = null)
    {
        return ResolveStringFromMethodThenClass(serviceType, methodInfo, attribute => attribute.Version);
    }

    /// <summary>
    /// 解析 API 标签（可叠加去重）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodInfo">方法信息</param>
    /// <returns>解析后的 API 标签列表</returns>
    public static IReadOnlyList<string> ResolveTags(Type serviceType, MethodInfo? methodInfo = null)
    {
        return ResolveDistinctValues(serviceType, methodInfo, attribute => attribute.Tag);
    }

    /// <summary>
    /// 解析版本列表（可叠加去重）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodInfo">方法信息</param>
    /// <returns>解析后的版本列表</returns>
    public static IReadOnlyList<string> ResolveVersions(Type serviceType, MethodInfo? methodInfo = null)
    {
        return ResolveDistinctValues(serviceType, methodInfo, attribute => attribute.Version);
    }

    /// <summary>
    /// 解析布尔值（方法层存在则取方法最后一个，否则取类最后一个，最终回退默认值）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodInfo">方法信息</param>
    /// <param name="selector">选择器函数</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>解析后的布尔值</returns>
    public static bool ResolveBoolFromMethodThenClass(
        Type serviceType,
        MethodInfo? methodInfo,
        Func<DynamicApiAttribute, bool> selector,
        bool defaultValue)
    {
        if (methodInfo != null)
        {
            var methodAttributes = GetOrderedMethodAttributes(methodInfo);
            if (methodAttributes.Count != 0)
            {
                return selector(methodAttributes[^1]);
            }
        }

        var classAttributes = GetOrderedClassAttributes(serviceType);
        if (classAttributes.Count != 0)
        {
            return selector(classAttributes[^1]);
        }

        return defaultValue;
    }

    /// <summary>
    /// 解析自定义属性（可叠加，后写入覆盖同名键）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodInfo">方法信息</param>
    /// <returns>解析后的自定义属性字典</returns>
    public static IReadOnlyDictionary<string, string> ResolveCustomProperties(Type serviceType, MethodInfo? methodInfo = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MergeCustomProperties(result, GetOrderedClassAttributes(serviceType));
        if (methodInfo != null)
        {
            MergeCustomProperties(result, GetOrderedMethodAttributes(methodInfo));
        }

        return result;
    }

    /// <summary>
    /// 按顺序解析字符串（空值会被忽略）
    /// </summary>
    /// <param name="attributes">特性列表</param>
    /// <param name="selector">选择器函数</param>
    /// <returns>解析后的字符串</returns>
    public static string? ResolveStringFromAttributes(IEnumerable<DynamicApiAttribute> attributes, Func<DynamicApiAttribute, string> selector)
    {
        return ResolveString(OrderAttributes(attributes), selector);
    }

    /// <summary>
    /// 按 Order 升序排序特性，同 Order 按定义顺序（稳定排序）
    /// </summary>
    /// <param name="attributes">特性列表</param>
    /// <returns>排序后的特性列表</returns>
    private static List<DynamicApiAttribute> OrderAttributes(IEnumerable<DynamicApiAttribute> attributes)
    {
        return attributes
            .Select((attribute, index) => new { Attribute = attribute, Index = index })
            .OrderBy(item => item.Attribute.Order)
            .ThenBy(item => item.Index)
            .Select(item => item.Attribute)
            .ToList();
    }

    /// <summary>
    /// 按方法优先、类兜底的顺序解析字符串（空值会被忽略）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodInfo">方法信息</param>
    /// <param name="selector">选择器函数</param>
    /// <returns>解析后的字符串</returns>
    private static string? ResolveStringFromMethodThenClass(
        Type serviceType,
        MethodInfo? methodInfo,
        Func<DynamicApiAttribute, string> selector)
    {
        if (methodInfo != null)
        {
            var methodValue = ResolveString(GetOrderedMethodAttributes(methodInfo), selector);
            if (!string.IsNullOrWhiteSpace(methodValue))
            {
                return methodValue;
            }
        }

        return ResolveString(GetOrderedClassAttributes(serviceType), selector);
    }

    /// <summary>
    /// 按顺序解析字符串（空值会被忽略，后值覆盖前值）
    /// </summary>
    /// <param name="attributes">特性列表</param>
    /// <param name="selector">选择器函数</param>
    /// <returns>解析后的字符串</returns>
    private static string? ResolveString(IReadOnlyList<DynamicApiAttribute> attributes, Func<DynamicApiAttribute, string> selector)
    {
        string? value = null;
        foreach (var attribute in attributes)
        {
            var candidate = selector(attribute);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                value = candidate.Trim();
            }
        }

        return value;
    }

    /// <summary>
    /// 按方法优先、类兜底的顺序解析可叠加字符串列表（空值会被忽略，最终结果去重）
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="methodInfo">方法信息</param>
    /// <param name="selector">选择器函数</param>
    /// <returns>解析后的字符串列表</returns>
    private static IReadOnlyList<string> ResolveDistinctValues(
        Type serviceType,
        MethodInfo? methodInfo,
        Func<DynamicApiAttribute, string> selector)
    {
        var values = new List<string>();
        AppendDistinctValues(values, GetOrderedClassAttributes(serviceType), selector);
        if (methodInfo != null)
        {
            AppendDistinctValues(values, GetOrderedMethodAttributes(methodInfo), selector);
        }

        return values;
    }

    /// <summary>
    /// 从特性列表中提取字符串值并添加到目标集合，确保去重（空值会被忽略）
    /// </summary>
    /// <param name="target">目标集合</param>
    /// <param name="attributes">特性列表</param>
    /// <param name="selector">选择器函数</param>
    private static void AppendDistinctValues(
        ICollection<string> target,
        IEnumerable<DynamicApiAttribute> attributes,
        Func<DynamicApiAttribute, string> selector)
    {
        foreach (var attribute in attributes)
        {
            var value = selector(attribute);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalized = value.Trim();
            if (!target.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(normalized);
            }
        }
    }

    /// <summary>
    /// 从特性列表中提取自定义属性并合并到目标字典，后写入覆盖同名键（空值会被忽略）
    /// </summary>
    /// <param name="target">目标字典</param>
    /// <param name="attributes">特性列表</param>
    private static void MergeCustomProperties(
        IDictionary<string, string> target,
        IEnumerable<DynamicApiAttribute> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (!TryParseCustomProperty(attribute.CustomProperties, out var key, out var value))
            {
                continue;
            }

            target[key] = value;
        }
    }

    /// <summary>
    /// 尝试解析自定义属性，格式为 "key=value" 或 "key"（后者表示值为空字符串），空值或无效格式会被忽略
    /// </summary>
    /// <param name="text">要解析的文本</param>
    /// <param name="key">解析出的键</param>
    /// <param name="value">解析出的值</param>
    /// <returns>解析是否成功</returns>
    private static bool TryParseCustomProperty(string text, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var raw = text.Trim();
        var separatorIndex = raw.IndexOf('=');
        if (separatorIndex <= 0)
        {
            key = raw;
            return true;
        }

        key = raw[..separatorIndex].Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        value = raw[(separatorIndex + 1)..].Trim();
        return true;
    }
}
