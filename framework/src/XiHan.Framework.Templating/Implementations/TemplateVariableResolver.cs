#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateVariableResolver
// Guid:5m0o4o9k-3n6p-4l9m-0o5o-4k9n6p3m0o5o
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Templating.Abstractions;

namespace XiHan.Framework.Templating.Implementations;

/// <summary>
/// 模板变量解析器实现
/// </summary>
public class TemplateVariableResolver : ITemplateVariableResolver
{
    /// <summary>
    /// 解析变量表达式
    /// </summary>
    /// <param name="expression">变量表达式</param>
    /// <param name="context">模板上下文</param>
    /// <returns>解析结果</returns>
    public object? ResolveVariable(string expression, ITemplateContext context)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        // 处理简单变量
        if (!expression.Contains('.') && !expression.Contains('['))
        {
            return context.GetVariable(expression);
        }

        // 处理复杂表达式，如 user.profile.name 或 items[0].name
        return ResolveComplexExpression(expression, context);
    }

    /// <summary>
    /// 解析属性路径
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="propertyPath">属性路径（如：user.profile.name）</param>
    /// <returns>属性值</returns>
    public object? ResolvePropertyPath(object target, string propertyPath)
    {
        if (target == null || string.IsNullOrWhiteSpace(propertyPath))
        {
            return null;
        }

        var parts = propertyPath.Split('.');
        var current = target;

        foreach (var part in parts)
        {
            if (current == null)
            {
                return null;
            }

            // 处理数组索引，如 items[0]
            current = part.Contains('[') && part.Contains(']') ? ResolveIndexedProperty(current, part) : ResolveSimpleProperty(current, part);
        }

        return current;
    }

    /// <summary>
    /// 设置属性值
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="propertyPath">属性路径</param>
    /// <param name="value">属性值</param>
    public void SetPropertyValue(object target, string propertyPath, object? value)
    {
        if (target == null || string.IsNullOrWhiteSpace(propertyPath))
        {
            return;
        }

        var parts = propertyPath.Split('.');
        var current = target;

        // 导航到最后一个属性的父对象
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (current == null)
            {
                return;
            }

            var part = parts[i];
            current = part.Contains('[') && part.Contains(']') ? ResolveIndexedProperty(current, part) : ResolveSimpleProperty(current, part);
        }

        if (current == null)
        {
            return;
        }

        // 设置最后一个属性的值
        var lastPart = parts[^1];
        SetSimpleProperty(current, lastPart, value);
    }

    /// <summary>
    /// 调用方法
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="methodName">方法名</param>
    /// <param name="arguments">方法参数</param>
    /// <returns>方法返回值</returns>
    public object? InvokeMethod(object target, string methodName, params object?[] arguments)
    {
        if (target == null || string.IsNullOrWhiteSpace(methodName))
        {
            return null;
        }

        var type = target.GetType();
        var argumentTypes = arguments?.Select(arg => arg?.GetType()).ToArray() ?? [];

        // 查找匹配的方法
        var method = FindBestMatchingMethod(type, methodName, argumentTypes);
        if (method == null)
        {
            return null;
        }

        try
        {
            // 转换参数类型
            var convertedArgs = ConvertArguments(arguments, method.GetParameters());
            return method.Invoke(target, convertedArgs);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"调用方法 {methodName} 失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 解析简单属性
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="propertyName">属性名</param>
    /// <returns>属性值</returns>
    private static object? ResolveSimpleProperty(object target, string propertyName)
    {
        if (target == null)
        {
            return null;
        }

        var type = target.GetType();

        // 尝试获取属性
        var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property?.CanRead == true)
        {
            return property.GetValue(target);
        }

        // 尝试获取字段
        var field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            return field.GetValue(target);
        }

        // 如果是字典类型
        if (target is IDictionary<string, object?> dictionary)
        {
            return dictionary.TryGetValue(propertyName, out var value) ? value : null;
        }

        return null;
    }

    /// <summary>
    /// 解析数组表达式
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <returns>变量名和索引</returns>
    private static (string variableName, int index) ParseArrayExpression(string expression)
    {
        var startBracket = expression.IndexOf('[');
        var endBracket = expression.IndexOf(']');

        if (startBracket == -1 || endBracket == -1 || endBracket <= startBracket)
        {
            throw new InvalidOperationException($"无效的数组表达式: {expression}");
        }

        var variableName = expression[..startBracket];
        var indexString = expression[(startBracket + 1)..endBracket];

        if (!int.TryParse(indexString, out var index))
        {
            throw new InvalidOperationException($"无效的数组索引: {indexString}");
        }

        return (variableName, index);
    }

    /// <summary>
    /// 获取数组元素
    /// </summary>
    /// <param name="array">数组对象</param>
    /// <param name="index">索引</param>
    /// <returns>数组元素</returns>
    private static object? GetArrayElement(object? array, int index)
    {
        if (array == null)
        {
            return null;
        }

        if (array is Array arr)
        {
            if (index >= 0 && index < arr.Length)
            {
                return arr.GetValue(index);
            }
        }
        else if (array is System.Collections.IList list)
        {
            if (index >= 0 && index < list.Count)
            {
                return list[index];
            }
        }

        return null;
    }

    /// <summary>
    /// 转换值类型
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="targetType">目标类型</param>
    /// <returns>转换后的值</returns>
    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType.IsAssignableFrom(value.GetType()))
        {
            return value;
        }

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }

    /// <summary>
    /// 检查类型是否可以转换
    /// </summary>
    /// <param name="sourceType">源类型</param>
    /// <param name="targetType">目标类型</param>
    /// <returns>是否可以转换</returns>
    private static bool CanConvert(Type sourceType, Type targetType)
    {
        if (targetType.IsAssignableFrom(sourceType))
        {
            return true;
        }

        // 检查基本类型转换
        if (sourceType.IsPrimitive && targetType.IsPrimitive)
        {
            return true;
        }

        // 检查字符串转换
        if (sourceType == typeof(string) || targetType == typeof(string))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 解析索引属性
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="indexedProperty">索引属性字符串</param>
    /// <returns>属性值</returns>
    private static object? ResolveIndexedProperty(object target, string indexedProperty)
    {
        var (variableName, index) = ParseArrayExpression(indexedProperty);
        var property = ResolveSimpleProperty(target, variableName);
        return GetArrayElement(property, index);
    }

    /// <summary>
    /// 设置简单属性
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">属性值</param>
    private static void SetSimpleProperty(object target, string propertyName, object? value)
    {
        if (target == null)
        {
            return;
        }

        var type = target.GetType();

        // 尝试设置属性
        var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property?.CanWrite == true)
        {
            var convertedValue = ConvertValue(value, property.PropertyType);
            property.SetValue(target, convertedValue);
            return;
        }

        // 尝试设置字段
        var field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            var convertedValue = ConvertValue(value, field.FieldType);
            field.SetValue(target, convertedValue);
            return;
        }

        // 如果是字典类型
        if (target is IDictionary<string, object?> dictionary)
        {
            dictionary[propertyName] = value;
        }
    }

    /// <summary>
    /// 查找最佳匹配的方法
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="methodName">方法名</param>
    /// <param name="argumentTypes">参数类型</param>
    /// <returns>方法信息</returns>
    private static MethodInfo? FindBestMatchingMethod(Type type, string methodName, Type?[] argumentTypes)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == methodName)
            .ToArray();

        // 精确匹配
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == argumentTypes.Length)
            {
                var isMatch = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (argumentTypes[i] != null && !parameters[i].ParameterType.IsAssignableFrom(argumentTypes[i]!))
                    {
                        isMatch = false;
                        break;
                    }
                }
                if (isMatch)
                {
                    return method;
                }
            }
        }

        // 兼容匹配
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == argumentTypes.Length)
            {
                var isMatch = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (argumentTypes[i] != null && !CanConvert(argumentTypes[i]!, parameters[i].ParameterType))
                    {
                        isMatch = false;
                        break;
                    }
                }
                if (isMatch)
                {
                    return method;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 转换参数
    /// </summary>
    /// <param name="arguments">原始参数</param>
    /// <param name="parameters">目标参数类型</param>
    /// <returns>转换后的参数</returns>
    private static object?[] ConvertArguments(object?[]? arguments, ParameterInfo[] parameters)
    {
        if (arguments == null || arguments.Length == 0)
        {
            return [];
        }

        var result = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length && i < arguments.Length; i++)
        {
            result[i] = ConvertValue(arguments[i], parameters[i].ParameterType);
        }

        return result;
    }

    /// <summary>
    /// 解析复杂表达式
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="context">模板上下文</param>
    /// <returns>解析结果</returns>
    private object? ResolveComplexExpression(string expression, ITemplateContext context)
    {
        var parts = expression.Split('.');
        var rootVariableName = parts[0];

        // 处理数组索引，如 items[0]
        if (rootVariableName.Contains('[') && rootVariableName.Contains(']'))
        {
            var (variableName, index) = ParseArrayExpression(rootVariableName);
            var arrayVariable = context.GetVariable(variableName);
            var root = GetArrayElement(arrayVariable, index);

            if (parts.Length == 1)
            {
                return root;
            }

            var remainingPath = string.Join(".", parts[1..]);
            return root != null ? ResolvePropertyPath(root, remainingPath) : null;
        }
        else
        {
            var root = context.GetVariable(rootVariableName);
            if (parts.Length == 1)
            {
                return root;
            }

            var remainingPath = string.Join(".", parts[1..]);
            return root != null ? ResolvePropertyPath(root, remainingPath) : null;
        }
    }
}
