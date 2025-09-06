#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TypeExtensions
// Guid:31e3c3dd-e34d-4f03-a890-6f27dd69487f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/22 0:14:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.ComponentModel;
using System.Text;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.Extensions;

/// <summary>
/// 类型扩展方法
/// </summary>
public static class TypeExtensions
{
    #region 判断类型

    /// <summary>
    /// 判断类型是否为 Nullable 类型
    /// </summary>
    /// <param name="type"> 要处理的类型 </param>
    /// <returns> 是返回 True，不是返回 False </returns>
    public static bool IsNullableType(this Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    /// <summary>
    /// 判断类型是否不为 Nullable 类型
    /// </summary>
    /// <param name="type"> 要处理的类型 </param>
    /// <returns> 是返回 True，不是返回 False </returns>
    public static bool IsNotNullableType(this Type type)
    {
        return !type.IsNullableType();
    }

    /// <summary>
    /// 判断类型是否为集合类型
    /// </summary>
    /// <param name="type">要处理的类型</param>
    /// <returns>是返回 True，不是返回 False</returns>
    public static bool IsEnumerable(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }

    /// <summary>
    /// 判断当前类型是否可派生至指定类型
    /// </summary>
    /// <typeparam name="TBaseType"></typeparam>
    /// <param name="type"></param>
    /// <param name="canAbstract"></param>
    /// <returns></returns>
    public static bool IsAssignableTo<TBaseType>(this Type type, bool canAbstract = false)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        return type.IsAssignableTo(typeof(TBaseType), canAbstract);
    }

    /// <summary>
    /// 判断当前类型是否可派生至指定类型
    /// </summary>
    /// <param name="type"></param>
    /// <param name="baseType"></param>
    /// <param name="canAbstract"></param>
    /// <returns></returns>
    public static bool IsAssignableTo(this Type type, Type baseType, bool canAbstract = false)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        return type.IsClass && (canAbstract || !type.IsAbstract) && type.IsAssignableFrom(baseType);
    }

    /// <summary>
    /// 判断当前类型是否可派生至指定泛型类型
    /// </summary>
    /// <param name="type">当前类型</param>
    /// <param name="genericType">目标泛型类型</param>
    /// <returns>如果当前类型可以分配给指定的泛型类型，则返回 true；否则返回 false</returns>
    /// <exception cref="ArgumentException">当 genericType 不是泛型类型时抛出</exception>
    public static bool IsAssignableToGeneric(this Type type, Type genericType)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));
        ArgumentNullException.ThrowIfNull(genericType, nameof(genericType));

        if (!genericType.IsGenericType)
        {
            throw new ArgumentException("该功能只支持泛型类型的调用，非泛型类型可使用 IsAssignableTo 方法", nameof(genericType));
        }

        // 获取泛型类型定义
        var genericTypeDefinition = genericType.IsGenericTypeDefinition
            ? genericType
            : genericType.GetGenericTypeDefinition();

        // 检查当前类型及其所有基类和接口
        var typesToCheck = new List<Type> { type };

        // 添加所有基类
        var baseType = type.BaseType;
        while (baseType is not null)
        {
            typesToCheck.Add(baseType);
            baseType = baseType.BaseType;
        }

        // 添加所有接口
        typesToCheck.AddRange(type.GetInterfaces());

        // 检查每个类型是否匹配泛型定义
        foreach (var typeToCheck in typesToCheck)
        {
            if (typeToCheck.IsGenericType)
            {
                var typeDefinition = typeToCheck.GetGenericTypeDefinition();
                if (typeDefinition == genericTypeDefinition)
                {
                    return true;
                }
            }
            else if (typeToCheck == genericTypeDefinition)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断当前类型是否可派生至指定泛型类型
    /// </summary>
    /// <typeparam name="TGenericType">目标泛型类型</typeparam>
    /// <param name="type">当前类型</param>
    /// <returns>如果当前类型可以分配给指定的泛型类型，则返回 true；否则返回 false</returns>
    public static bool IsAssignableToGeneric<TGenericType>(this Type type)
    {
        return type.IsAssignableToGeneric(typeof(TGenericType));
    }

    /// <summary>
    /// 返回当前类型是否可派生自指定基类
    /// </summary>
    /// <param name="type"></param>
    /// <param name="baseType"></param>
    /// <returns></returns>
    public static bool IsAssignableFrom(this Type type, Type baseType)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        return baseType.IsGenericTypeDefinition
            ? baseType.IsAssignableFromGeneric(type)
            : baseType.IsAssignableFrom(type);
    }

    /// <summary>
    /// 返回当前类型是否可派生自指定基类
    /// </summary>
    /// <typeparam name="TBaseType"></typeparam>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsAssignableFrom<TBaseType>(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        var baseType = typeof(TBaseType);
        return type.IsAssignableFrom(baseType);
    }

    /// <summary>
    /// 判断当前泛型类型是否可派生自指定类型
    /// </summary>
    /// <param name="genericType"></param>
    /// <param name="baseType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static bool IsAssignableFromGeneric(this Type genericType, Type baseType)
    {
        ArgumentNullException.ThrowIfNull(genericType, nameof(genericType));

        if (!genericType.IsGenericType)
        {
            throw new ArgumentException("该功能只支持泛型类型的调用，非泛型类型可使用 IsAssignableFrom 方法");
        }

        List<Type> allOthers = [baseType];
        if (genericType.IsInterface)
        {
            allOthers.AddRange(baseType.GetInterfaces());
        }

        foreach (var other in allOthers)
        {
            var cur = other;
            while (cur is not null)
            {
                if (cur.IsGenericType)
                {
                    cur = cur.GetGenericTypeDefinition();
                }

                if (cur.IsSubclassOf(genericType) || cur == genericType)
                {
                    return true;
                }

                if (cur.BaseType is not null)
                {
                    cur = cur.BaseType;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 判断当前泛型类型是否可派生自指定类型
    /// </summary>
    /// <typeparam name="TBaseType">基类类型</typeparam>
    /// <param name="genericType">泛型类型</param>
    /// <returns>如果泛型类型可以从指定的基类类型派生，则返回 true；否则返回 false</returns>
    /// <exception cref="ArgumentException">当 genericType 不是泛型类型时抛出</exception>
    public static bool IsAssignableFromGeneric<TBaseType>(this Type genericType)
    {
        return genericType.IsAssignableFromGeneric(typeof(TBaseType));
    }

    #endregion 判断类型

    #region 获取基类

    /// <summary>
    /// 获取指定类型的所有基类
    /// </summary>
    /// <param name="type">要获取其基类的类型</param>
    /// <param name="includeObject">如果为 true，则在返回结果中包含标准的 <see cref="object"/> 类型</param>
    public static Type[] GetBaseClasses(this Type type, bool includeObject = true)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        var types = new List<Type>();
        AddTypeAndBaseTypesRecursively(types, type.BaseType, includeObject);
        return [.. types];
    }

    /// <summary>
    /// 获取指定类型的所有基类
    /// </summary>
    /// <param name="type">要获取其基类的类型</param>
    /// <param name="stoppingType">停止查找的基类类型，该类型也会包含在返回结果中</param>
    /// <param name="includeObject">如果为 true，则在返回结果中包含标准的 <see cref="object"/> 类型</param>
    public static Type[] GetBaseClasses(this Type type, Type stoppingType, bool includeObject = true)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        var types = new List<Type>();
        AddTypeAndBaseTypesRecursively(types, type.BaseType, includeObject, stoppingType);
        return [.. types];
    }

    #endregion

    #region 空类型

    /// <summary>
    /// 由类型的 Nullable 类型返回实际类型
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static Type GetNonNullableType(this Type type)
    {
        return type.IsNullableType() ? type.GetGenericArguments()[0] : type;
    }

    /// <summary>
    /// 通过类型转换器获取 Nullable 类型的基础类型
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static Type GetUnNullableType(this Type type)
    {
        if (!type.IsNullableType())
        {
            return type;
        }

        NullableConverter nullableConverter = new(type);
        return nullableConverter.UnderlyingType;
    }

    #endregion 空类型

    #region 获取描述

    /// <summary>
    /// 获取类型的 Description 特性描述信息
    /// </summary>
    /// <param name="type">类型对象</param>
    /// <param name="inherit">是否搜索类型的继承链以查找 Description 特性</param>
    /// <returns>返回 Description 特性描述信息，如不存在则返回类型的全名</returns>
    public static string GetDescription(this Type type, bool inherit = true)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        var result = string.Empty;
        var fullName = type.FullName ?? result;

        var desc = type.GetSingleAttributeOrNull<DescriptionAttribute>(inherit);

        if (desc is null)
        {
            return result;
        }

        var description = desc.Description;
        result = fullName + "(" + description + ")";

        return result;
    }

    #endregion 获取描述

    #region 类型名称

    /// <summary>
    /// 获取类型的全名
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static string GetFullNameWithAssemblyName(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        return $"{type.FullName},{type.Assembly.GetName().Name}";
    }

    /// <summary>
    /// 获取类型的全名，附带所在类库
    /// </summary>
    public static string GetFullNameWithModule(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        return $"{type.FullName},{type.Module.Name.Replace(".dll", string.Empty).Replace(".exe", string.Empty)}";
    }

    /// <summary>
    /// 获取类型的显示短名称
    /// </summary>
    public static string GetShortDisplayName(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        return type.GetDisplayName(false);
    }

    /// <summary>
    /// 获取类型的显示名称
    /// </summary>
    public static string GetDisplayName(this Type type, bool fullName = true)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        StringBuilder sb = new();
        ProcessType(sb, type, fullName);
        return sb.ToString();
    }

    #endregion 类型名称

    #region 私有方法

    /// <summary>
    /// 内置类型名称
    /// </summary>
    private static readonly Dictionary<Type, string> BuiltInTypeNames = new()
    {
        {
            typeof(bool), "bool"
        },
        {
            typeof(byte), "byte"
        },
        {
            typeof(char), "char"
        },
        {
            typeof(decimal), "decimal"
        },
        {
            typeof(double), "double"
        },
        {
            typeof(float), "float"
        },
        {
            typeof(int), "int"
        },
        {
            typeof(long), "long"
        },
        {
            typeof(object), "object"
        },
        {
            typeof(sbyte), "sbyte"
        },
        {
            typeof(short), "short"
        },
        {
            typeof(string), "string"
        },
        {
            typeof(uint), "uint"
        },
        {
            typeof(ulong), "ulong"
        },
        {
            typeof(ushort), "ushort"
        },
        {
            typeof(void), "void"
        }
    };

    /// <summary>
    /// 处理类型
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="type"></param>
    /// <param name="fullName"></param>
    private static void ProcessType(StringBuilder builder, Type type, bool fullName)
    {
        if (type.IsGenericType)
        {
            var genericArguments = type.GetGenericArguments();
            ProcessGenericType(builder, type, genericArguments, genericArguments.Length, fullName);
        }
        else if (type.IsArray)
        {
            ProcessArrayType(builder, type, fullName);
        }
        else if (BuiltInTypeNames.TryGetValue(type, out var builtInName))
        {
            builder.Append(builtInName);
        }
        else if (!type.IsGenericParameter)
        {
            builder.Append(fullName ? type.FullName : type.Name);
        }
    }

    /// <summary>
    /// 处理数组类型
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="type"></param>
    /// <param name="fullName"></param>
    private static void ProcessArrayType(StringBuilder builder, Type type, bool fullName)
    {
        var innerType = type;
        while (innerType!.IsArray)
        {
            innerType = innerType.GetElementType();
        }

        ProcessType(builder, innerType, fullName);

        while (type.IsArray)
        {
            builder.Append('[');
            builder.Append(',', type.GetArrayRank() - 1);
            builder.Append(']');
            type = type.GetElementType()!;
        }
    }

    /// <summary>
    /// 处理泛型类型
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="type"></param>
    /// <param name="genericArguments"></param>
    /// <param name="length"></param>
    /// <param name="fullName"></param>
    private static void ProcessGenericType(StringBuilder builder, Type type, IReadOnlyList<Type> genericArguments, int length,
        bool fullName)
    {
        var offset = type.IsNested ? type.DeclaringType!.GetGenericArguments().Length : 0;

        if (fullName)
        {
            if (type.IsNested)
            {
                ProcessGenericType(builder, type.DeclaringType!, genericArguments, offset, fullName);
                builder.Append('+');
            }
            else
            {
                builder.Append(type.Namespace);
                builder.Append('.');
            }
        }

        var genericPartIndex = type.Name.IndexOf('`');
        if (genericPartIndex <= 0)
        {
            builder.Append(type.Name);
            return;
        }

        builder.Append(type.Name, 0, genericPartIndex);
        builder.Append('<');

        for (var i = offset; i < length; i++)
        {
            ProcessType(builder, genericArguments[i], fullName);
            if (i + 1 == length)
            {
                continue;
            }

            builder.Append(',');
            if (!genericArguments[i + 1].IsGenericParameter)
            {
                builder.Append(' ');
            }
        }

        builder.Append('>');
    }

    /// <summary>
    /// 递归添加类型及其基类到列表中
    /// </summary>
    /// <param name="types">用于收集类型的列表</param>
    /// <param name="type">当前要添加的类型</param>
    /// <param name="includeObject">是否包含 object 类型</param>
    /// <param name="stoppingType">如果指定了该类型，则在遇到它时停止递归(包含该类型)</param>
    private static void AddTypeAndBaseTypesRecursively(List<Type> types, Type? type, bool includeObject, Type? stoppingType = null)
    {
        if (type == null || type == stoppingType)
        {
            return;
        }

        if (!includeObject && type == typeof(object))
        {
            return;
        }

        AddTypeAndBaseTypesRecursively(types, type.BaseType, includeObject, stoppingType);
        types.Add(type);
    }

    #endregion 私有方法
}
