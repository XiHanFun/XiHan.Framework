#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CommandLineBinder
// Guid:a7b983d2-aa3b-41ea-9d02-1b156ae0afcc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;
using System.Reflection;
using XiHan.Framework.DevTools.CommandLine.Arguments;
using XiHan.Framework.DevTools.CommandLine.Attributes;
using XiHan.Framework.DevTools.CommandLine.Validators;

namespace XiHan.Framework.DevTools.CommandLine;

/// <summary>
/// 命令行参数绑定器
/// </summary>
public class CommandLineBinder
{
    /// <summary>
    /// 将解析结果绑定到对象
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="parsedArgs">解析结果</param>
    /// <returns>绑定后的对象</returns>
    public static T Bind<T>(ParsedArguments parsedArgs) where T : new()
    {
        var instance = new T();
        var type = typeof(T);

        // 获取所有属性和字段
        var members = GetMembers(type);

        // 绑定选项
        BindOptions(instance, members, parsedArgs);

        // 绑定位置参数
        BindArguments(instance, members, parsedArgs);

        // 验证必填参数
        ValidateRequired(instance, members);

        // 执行自定义验证
        ValidateCustom(instance, members);

        return instance;
    }

    /// <summary>
    /// 获取类型的所有成员
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>成员信息列表</returns>
    private static List<MemberInfo> GetMembers(Type type)
    {
        var members = new List<MemberInfo>();

        // 获取所有公共属性
        members.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite));

        // 获取所有公共字段
        members.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => !f.IsInitOnly));

        return members;
    }

    /// <summary>
    /// 绑定选项参数
    /// </summary>
    /// <param name="instance">目标对象</param>
    /// <param name="members">成员列表</param>
    /// <param name="parsedArgs">解析结果</param>
    private static void BindOptions(object instance, List<MemberInfo> members, ParsedArguments parsedArgs)
    {
        foreach (var member in members)
        {
            var optionAttr = member.GetCustomAttribute<CommandOptionAttribute>();
            if (optionAttr == null)
            {
                continue;
            }

            var optionNames = new List<string> { optionAttr.LongName };
            if (!string.IsNullOrEmpty(optionAttr.ShortName))
            {
                optionNames.Add(optionAttr.ShortName);
            }

            string? foundOption = null;
            List<string>? values = null;

            // 查找匹配的选项
            foreach (var optionName in optionNames)
            {
                if (parsedArgs.HasOption(optionName))
                {
                    foundOption = optionName;
                    values = parsedArgs.GetOptions(optionName);
                    break;
                }
            }

            if (foundOption != null && values != null)
            {
                // 设置值
                SetMemberValue(member, instance, values, optionAttr);
            }
            else if (optionAttr.DefaultValue != null)
            {
                // 设置默认值
                SetMemberValue(member, instance, optionAttr.DefaultValue);
            }
        }
    }

    /// <summary>
    /// 绑定位置参数
    /// </summary>
    /// <param name="instance">目标对象</param>
    /// <param name="members">成员列表</param>
    /// <param name="parsedArgs">解析结果</param>
    private static void BindArguments(object instance, List<MemberInfo> members, ParsedArguments parsedArgs)
    {
        var argumentMembers = members
            .Where(m => m.GetCustomAttribute<CommandArgumentAttribute>() != null)
            .OrderBy(m => m.GetCustomAttribute<CommandArgumentAttribute>()!.Position)
            .ToList();

        for (var i = 0; i < argumentMembers.Count; i++)
        {
            var member = argumentMembers[i];
            var argAttr = member.GetCustomAttribute<CommandArgumentAttribute>()!;

            if (argAttr.AllowMultiple && i == argumentMembers.Count - 1)
            {
                // 最后一个参数支持多值，收集剩余所有参数
                var remainingValues = parsedArgs.Arguments.Skip(i).ToList();
                if (remainingValues.Count > 0)
                {
                    SetMemberValue(member, instance, remainingValues, null);
                }
                else if (argAttr.DefaultValue != null)
                {
                    SetMemberValue(member, instance, argAttr.DefaultValue);
                }
            }
            else
            {
                // 单值参数
                var value = parsedArgs.GetArgument(i);
                if (value != null)
                {
                    SetMemberValue(member, instance, [value], null);
                }
                else if (argAttr.DefaultValue != null)
                {
                    SetMemberValue(member, instance, argAttr.DefaultValue);
                }
            }
        }
    }

    /// <summary>
    /// 设置成员值
    /// </summary>
    /// <param name="member">成员信息</param>
    /// <param name="instance">目标对象</param>
    /// <param name="values">值列表</param>
    /// <param name="optionAttr">选项属性</param>
    private static void SetMemberValue(MemberInfo member, object instance, List<string> values, CommandOptionAttribute? optionAttr)
    {
        var memberType = GetMemberType(member);

        if (memberType == null)
        {
            return;
        }

        try
        {
            object? convertedValue;

            if (optionAttr?.IsSwitch == true || (values.Count == 0 && memberType == typeof(bool)))
            {
                // 布尔开关
                convertedValue = true;
            }
            else if (optionAttr?.AllowMultiple == true || IsCollectionType(memberType))
            {
                // 多值处理
                convertedValue = ConvertToCollection(values, memberType, optionAttr?.Separator ?? ',');
            }
            else
            {
                // 单值处理
                var stringValue = values.Count > 0 ? values[0] : "";
                convertedValue = ConvertValue(stringValue, memberType);
            }

            SetMemberValue(member, instance, convertedValue);
        }
        catch (Exception ex)
        {
            throw new ArgumentParseException($"无法设置参数 '{member.Name}' 的值: {ex.Message}", member.Name);
        }
    }

    /// <summary>
    /// 设置成员值
    /// </summary>
    /// <param name="member">成员信息</param>
    /// <param name="instance">目标对象</param>
    /// <param name="value">值</param>
    private static void SetMemberValue(MemberInfo member, object instance, object? value)
    {
        switch (member)
        {
            case PropertyInfo property:
                property.SetValue(instance, value);
                break;

            case FieldInfo field:
                field.SetValue(instance, value);
                break;
        }
    }

    /// <summary>
    /// 获取成员类型
    /// </summary>
    /// <param name="member">成员信息</param>
    /// <returns>成员类型</returns>
    private static Type? GetMemberType(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => null
        };
    }

    /// <summary>
    /// 检查是否为集合类型
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>是否为集合类型</returns>
    private static bool IsCollectionType(Type type)
    {
        if (type == typeof(string))
        {
            return false;
        }

        return type.IsArray ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) ||
               type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    /// <summary>
    /// 转换为集合
    /// </summary>
    /// <param name="values">值列表</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="separator">分隔符</param>
    /// <returns>转换后的集合</returns>
    private static object? ConvertToCollection(List<string> values, Type targetType, char separator)
    {
        // 展开分隔符分割的值
        var expandedValues = new List<string>();
        foreach (var value in values)
        {
            if (value.Contains(separator))
            {
                expandedValues.AddRange(value.Split(separator, StringSplitOptions.RemoveEmptyEntries));
            }
            else
            {
                expandedValues.Add(value);
            }
        }

        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType()!;
            var array = Array.CreateInstance(elementType, expandedValues.Count);
            for (var i = 0; i < expandedValues.Count; i++)
            {
                array.SetValue(ConvertValue(expandedValues[i], elementType), i);
            }
            return array;
        }

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = targetType.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod("Add")!;

            foreach (var value in expandedValues)
            {
                addMethod.Invoke(list, [ConvertValue(value, elementType)]);
            }
            return list;
        }

        return expandedValues;
    }

    /// <summary>
    /// 转换值类型
    /// </summary>
    /// <param name="value">字符串值</param>
    /// <param name="targetType">目标类型</param>
    /// <returns>转换后的值</returns>
    private static object? ConvertValue(string value, Type targetType)
    {
        if (string.IsNullOrEmpty(value) && targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
        {
            return Activator.CreateInstance(targetType);
        }

        // 处理可空类型
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            targetType = underlyingType;
        }

        // 特殊类型处理
        if (targetType == typeof(string))
        {
            return value;
        }

        if (targetType == typeof(bool))
        {
            return string.IsNullOrEmpty(value) ||
                   value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value, true);
        }

        // 使用TypeConverter
        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromString(value);
        }

        // 使用Convert
        return Convert.ChangeType(value, targetType);
    }

    /// <summary>
    /// 验证必填参数
    /// </summary>
    /// <param name="instance">对象实例</param>
    /// <param name="members">成员列表</param>
    private static void ValidateRequired(object instance, List<MemberInfo> members)
    {
        foreach (var member in members)
        {
            var optionAttr = member.GetCustomAttribute<CommandOptionAttribute>();
            var argumentAttr = member.GetCustomAttribute<CommandArgumentAttribute>();

            var isRequired = optionAttr?.Required == true || argumentAttr?.Required == true;
            if (!isRequired)
            {
                continue;
            }

            var value = GetMemberValue(member, instance);
            var memberType = GetMemberType(member);

            if (IsValueEmpty(value, memberType))
            {
                var memberName = optionAttr?.LongName ?? argumentAttr?.Name ?? member.Name;
                throw new ArgumentParseException($"参数 '{memberName}' 是必填的", memberName);
            }
        }
    }

    /// <summary>
    /// 执行自定义验证
    /// </summary>
    /// <param name="instance">对象实例</param>
    /// <param name="members">成员列表</param>
    private static void ValidateCustom(object instance, List<MemberInfo> members)
    {
        foreach (var member in members)
        {
            var validationAttrs = member.GetCustomAttributes<ValidationAttribute>();
            if (!validationAttrs.Any())
            {
                continue;
            }

            var value = GetMemberValue(member, instance);

            foreach (var validationAttr in validationAttrs)
            {
                if (Activator.CreateInstance(validationAttr.ValidatorType) is not IValidator validator)
                {
                    continue;
                }

                var result = validator.Validate(value, validationAttr.Parameters);
                if (!result.IsValid)
                {
                    var errorMessage = validationAttr.ErrorMessage ?? result.ErrorMessage ?? $"参数 '{member.Name}' 验证失败";
                    throw new ArgumentParseException(errorMessage, member.Name);
                }
            }
        }
    }

    /// <summary>
    /// 获取成员值
    /// </summary>
    /// <param name="member">成员信息</param>
    /// <param name="instance">对象实例</param>
    /// <returns>成员值</returns>
    private static object? GetMemberValue(MemberInfo member, object instance)
    {
        return member switch
        {
            PropertyInfo property => property.GetValue(instance),
            FieldInfo field => field.GetValue(instance),
            _ => null
        };
    }

    /// <summary>
    /// 检查值是否为空
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="type">类型</param>
    /// <returns>是否为空</returns>
    private static bool IsValueEmpty(object? value, Type? type)
    {
        if (value == null)
        {
            return true;
        }

        if (value is string str)
        {
            return string.IsNullOrEmpty(str);
        }

        if (type != null && type.IsValueType)
        {
            return value.Equals(Activator.CreateInstance(type));
        }

        return false;
    }
}

/// <summary>
/// 命令行绑定器扩展
/// </summary>
public static class CommandLineBinderExtensions
{
    /// <summary>
    /// 绑定到指定类型
    /// </summary>
    /// <param name="binder">绑定器</param>
    /// <param name="type">目标类型</param>
    /// <param name="parsedArgs">解析结果</param>
    /// <returns>绑定后的对象</returns>
    public static object Bind(this CommandLineBinder binder, Type type, ParsedArguments parsedArgs)
    {
        var method = typeof(CommandLineBinder).GetMethod("Bind")!;
        var genericMethod = method.MakeGenericMethod(type);
        return genericMethod.Invoke(binder, [parsedArgs])!;
    }
}
