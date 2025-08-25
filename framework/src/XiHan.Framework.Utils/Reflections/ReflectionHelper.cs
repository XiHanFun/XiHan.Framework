#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ReflectionHelper
// Guid:e8f234f6-9d3e-4dbc-aee6-d02fbf424954
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2023-04-11 下午 09:28:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Reflections;

/// <summary>
/// 反射帮助类
/// 提供程序集、类型、属性等反射操作的工具方法
/// </summary>
public static class ReflectionHelper
{
    #region 程序集

    /// <summary>
    /// 获取应用程序的入口程序集
    /// </summary>
    /// <returns>入口程序集，如果无法确定则返回 null</returns>
    public static Assembly? GetEntryAssembly()
    {
        return Assembly.GetEntryAssembly();
    }

    /// <summary>
    /// 获取入口程序集的名称
    /// </summary>
    /// <returns>程序集名称，如果无法获取则返回 null</returns>
    public static string? GetEntryAssemblyName()
    {
        return GetEntryAssembly()?.GetName().Name;
    }

    /// <summary>
    /// 获取入口程序集的版本信息
    /// </summary>
    /// <returns>程序集版本，如果无法获取则返回 null</returns>
    public static Version? GetEntryAssemblyVersion()
    {
        return GetEntryAssembly()?.GetName().Version;
    }

    /// <summary>
    /// 获取指定目录下的程序集文件路径
    /// </summary>
    /// <param name="folderPath">要搜索的文件夹路径</param>
    /// <param name="searchOption">搜索选项（是否包含子目录）</param>
    /// <returns>程序集文件路径集合（.dll 和 .exe 文件）</returns>
    /// <exception cref="ArgumentException">当文件夹路径为空或 null 时</exception>
    public static IEnumerable<string> GetAssemblyFiles(string folderPath, SearchOption searchOption)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        return Directory
            .EnumerateFiles(folderPath, "*.*", searchOption)
            .Where(s => s.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                       s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 从指定目录加载所有程序集
    /// </summary>
    /// <param name="folderPath">要搜索的文件夹路径</param>
    /// <param name="searchOption">搜索选项（是否包含子目录）</param>
    /// <returns>加载的程序集列表</returns>
    /// <exception cref="ArgumentException">当文件夹路径为空或 null 时</exception>
    public static List<Assembly> LoadAssemblies(string folderPath, SearchOption searchOption)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        return [.. GetAssemblyFiles(folderPath, searchOption).Select(AssemblyLoadContext.Default.LoadFromAssemblyPath)];
    }

    /// <summary>
    /// 获取当前应用程序域中加载的所有程序集
    /// </summary>
    /// <returns>已加载的程序集集合</returns>
    public static IEnumerable<Assembly> GetAllAssemblies()
    {
        return AssemblyLoadContext.Default.Assemblies;
    }

    /// <summary>
    /// 获取所有被引用的程序集（递归获取所有依赖项）
    /// </summary>
    /// <param name="skipSystemAssemblies">是否跳过系统程序集（默认为 true）</param>
    /// <returns>被引用的程序集集合</returns>
    public static IEnumerable<Assembly> GetAllReferencedAssemblies(bool skipSystemAssemblies = true)
    {
        var rootAssembly = Assembly.GetEntryAssembly();
        rootAssembly ??= Assembly.GetCallingAssembly();

        HashSet<Assembly> returnAssemblies = new(new AssemblyEquality());
        HashSet<string> loadedAssemblies = [];
        Queue<Assembly> assembliesToCheck = new();
        assembliesToCheck.Enqueue(rootAssembly);

        if (skipSystemAssemblies && IsSystemAssembly(rootAssembly))
        {
            if (IsValid(rootAssembly))
            {
                _ = returnAssemblies.Add(rootAssembly);
            }
        }

        while (assembliesToCheck.Count != 0)
        {
            var assemblyToCheck = assembliesToCheck.Dequeue();
            foreach (var reference in assemblyToCheck.GetReferencedAssemblies())
            {
                if (loadedAssemblies.Contains(reference.FullName))
                {
                    continue;
                }

                var assembly = Assembly.Load(reference);
                if (skipSystemAssemblies && IsSystemAssembly(assembly))
                {
                    continue;
                }

                assembliesToCheck.Enqueue(assembly);
                _ = loadedAssemblies.Add(reference.FullName);
                if (IsValid(assembly))
                {
                    _ = returnAssemblies.Add(assembly);
                }
            }
        }

        var asmsInBaseDir = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll", new EnumerationOptions
        {
            RecurseSubdirectories = true
        });
        foreach (var assemblyPath in asmsInBaseDir)
        {
            if (!IsManagedAssembly(assemblyPath))
            {
                continue;
            }

            var asmName = AssemblyName.GetAssemblyName(assemblyPath);

            // 如果程序集已经加载过了就不再加载
            if (returnAssemblies.Any(x => AssemblyName.ReferenceMatchesDefinition(x.GetName(), asmName)))
            {
                continue;
            }

            if (skipSystemAssemblies && IsSystemAssembly(assemblyPath))
            {
                continue;
            }

            var asm = TryLoadAssembly(assemblyPath);
            if (asm is null)
            {
                continue;
            }

            if (!IsValid(asm))
            {
                continue;
            }

            if (skipSystemAssemblies && IsSystemAssembly(asm))
            {
                continue;
            }

            _ = returnAssemblies.Add(asm);
        }

        return [.. returnAssemblies];
    }

    /// <summary>
    /// 获取符合条件名称的程序集
    /// </summary>
    /// <param name="prefix">前缀名</param>
    /// <param name="suffix">后缀名</param>
    /// <param name="contain">包含名</param>
    /// <returns></returns>
    public static IEnumerable<Assembly> GetEffectiveAssemblies(string prefix, string suffix, string contain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);
        ArgumentException.ThrowIfNullOrWhiteSpace(contain);

        return GetAllAssemblies()
            .Where(assembly => assembly.ManifestModule.Name.EndsWith(suffix, StringComparison.InvariantCultureIgnoreCase))
            .Where(assembly => assembly.ManifestModule.Name.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
            .Where(assembly => assembly.ManifestModule.Name.Contains(contain, StringComparison.InvariantCultureIgnoreCase))
            .Distinct();
    }

    /// <summary>
    /// 获取符合条件前后缀名称的程序集
    /// </summary>
    /// <param name="prefix">前缀名</param>
    /// <param name="suffix">后缀名</param>
    /// <returns></returns>
    public static IEnumerable<Assembly> GetEffectivePatchAssemblies(string prefix, string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);

        return GetAllAssemblies()
            .Where(assembly => assembly.ManifestModule.Name.EndsWith(suffix, StringComparison.InvariantCultureIgnoreCase))
            .Where(assembly => assembly.ManifestModule.Name.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
            .Distinct();
    }

    /// <summary>
    /// 获取符合条件包含名称的程序集
    /// </summary>
    /// <param name="contain">包含名</param>
    /// <returns></returns>
    public static IEnumerable<Assembly> GetEffectiveCenterAssemblies(string contain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contain);

        return GetAllAssemblies()
            .Where(assembly => assembly.ManifestModule.Name.Contains(contain, StringComparison.InvariantCultureIgnoreCase))
            .Distinct();
    }

    /// <summary>
    /// 获取曦寒程序集
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<Assembly> GetXiHanAssemblies()
    {
        return GetEffectivePatchAssemblies("XiHan", "dll");
    }

    /// <summary>
    /// 获取应用程序集
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<Assembly> GetApplicationAssemblies()
    {
        return GetEffectiveCenterAssemblies("Application");
    }

    /// <summary>
    /// 获取曦寒应用程序集
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<Assembly> GetXiHanApplicationAssemblies()
    {
        return GetEffectiveAssemblies("XiHan", "dll", "Application");
    }

    #endregion 程序集

    #region 程序集类

    /// <summary>
    /// 获取所有已加载程序集中的所有类型
    /// </summary>
    /// <returns>所有类型的集合</returns>
    public static IEnumerable<Type> GetAllTypes()
    {
        return GetAllAssemblies()
            .SelectMany(GetAllTypes)
            .Distinct();
    }

    /// <summary>
    /// 获取指定程序集中的所有类型（安全获取，处理加载异常）
    /// </summary>
    /// <param name="assembly">目标程序集</param>
    /// <returns>程序集中的类型集合</returns>
    /// <exception cref="ArgumentNullException">当程序集为 null 时</exception>
    public static IEnumerable<Type> GetAllTypes(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null)!;
        }
    }

    /// <summary>
    /// 获取曦寒框架程序集中的所有类型
    /// </summary>
    /// <returns>曦寒框架类型集合</returns>
    public static IEnumerable<Type> GetXiHanTypes()
    {
        return GetXiHanAssemblies()
            .SelectMany(GetAllTypes)
            .Distinct();
    }

    /// <summary>
    /// 获取应用程序集中的所有类型
    /// </summary>
    /// <returns>应用程序类型集合</returns>
    public static IEnumerable<Type> GetApplicationTypes()
    {
        return GetApplicationAssemblies()
            .SelectMany(GetAllTypes)
            .Distinct();
    }

    /// <summary>
    /// 获取曦寒应用程序集中的所有类型
    /// </summary>
    /// <returns>曦寒应用程序类型集合</returns>
    public static IEnumerable<Type> GetXiHanApplicationTypes()
    {
        return GetXiHanApplicationAssemblies()
            .SelectMany(GetAllTypes)
            .Distinct();
    }

    #endregion 程序集类

    #region 类型

    /// <summary>
    /// 检查给定类型是否实现或继承了指定的泛型类型
    /// </summary>
    /// <param name="givenType">要检查的类型</param>
    /// <param name="genericType">泛型类型定义</param>
    /// <returns>如果给定类型实现或继承了泛型类型则返回 true，否则返回 false</returns>
    public static bool IsAssignableToGenericType(Type givenType, Type genericType)
    {
        ArgumentNullException.ThrowIfNull(givenType);
        ArgumentNullException.ThrowIfNull(genericType);

        var givenTypeInfo = givenType.GetTypeInfo();

        if (givenTypeInfo.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
        {
            return true;
        }

        foreach (var interfaceType in givenTypeInfo.GetInterfaces())
        {
            if (interfaceType.GetTypeInfo().IsGenericType && interfaceType.GetGenericTypeDefinition() == genericType)
            {
                return true;
            }
        }

        return givenTypeInfo.BaseType != null && IsAssignableToGenericType(givenTypeInfo.BaseType, genericType);
    }

    /// <summary>
    /// 获取给定类型实现的所有泛型类型
    /// </summary>
    /// <param name="givenType">要检查的类型</param>
    /// <param name="genericType">泛型类型定义</param>
    /// <returns>实现的泛型类型列表</returns>
    public static List<Type> GetImplementedGenericTypes(Type givenType, Type genericType)
    {
        ArgumentNullException.ThrowIfNull(givenType);
        ArgumentNullException.ThrowIfNull(genericType);

        var result = new List<Type>();
        AddImplementedGenericTypes(result, givenType, genericType);
        return result;
    }

    /// <summary>
    /// 尝试获取类成员及其声明类型上定义的单个特性，包括继承的特性
    /// 如果未找到则返回默认值
    /// </summary>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    /// <param name="memberInfo">成员信息</param>
    /// <param name="defaultValue">默认值（默认为 null）</param>
    /// <param name="inherit">是否从基类继承特性</param>
    /// <returns>找到的特性实例或默认值</returns>
    public static TAttribute? GetSingleAttributeOrDefault<TAttribute>(MemberInfo memberInfo, TAttribute? defaultValue = default, bool inherit = true)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(memberInfo);

        // 使用 GetCustomAttributes 方法获取特性
        return memberInfo.IsDefined(typeof(TAttribute), inherit)
            ? memberInfo.GetCustomAttributes(typeof(TAttribute), inherit).Cast<TAttribute>().First()
            : defaultValue;
    }

    /// <summary>
    /// 尝试获取类成员或其声明类型上定义的单个特性，包括继承的特性
    /// 如果未找到则返回默认值
    /// </summary>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    /// <param name="memberInfo">成员信息</param>
    /// <param name="defaultValue">默认值（默认为 null）</param>
    /// <param name="inherit">是否从基类继承特性</param>
    /// <returns>找到的特性实例或默认值</returns>
    public static TAttribute? GetSingleAttributeOfMemberOrDeclaringTypeOrDefault<TAttribute>(MemberInfo memberInfo, TAttribute? defaultValue = default, bool inherit = true)
        where TAttribute : class
    {
        ArgumentNullException.ThrowIfNull(memberInfo);

        return memberInfo.GetCustomAttributes(inherit).OfType<TAttribute>().FirstOrDefault()
               ?? memberInfo.DeclaringType?.GetTypeInfo().GetCustomAttributes(inherit).OfType<TAttribute>().FirstOrDefault()
               ?? defaultValue;
    }

    /// <summary>
    /// 获取类成员及其声明类型上定义的所有特性，包括继承的特性
    /// </summary>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    /// <param name="memberInfo">成员信息</param>
    /// <param name="inherit">是否从基类继承特性</param>
    /// <returns>特性集合</returns>
    public static IEnumerable<TAttribute> GetAttributesOfMemberOrDeclaringType<TAttribute>(MemberInfo memberInfo, bool inherit = true)
        where TAttribute : class
    {
        ArgumentNullException.ThrowIfNull(memberInfo);

        var customAttributes = memberInfo.GetCustomAttributes(inherit).OfType<TAttribute>();
        var declaringTypeCustomAttributes =
            memberInfo.DeclaringType?.GetTypeInfo().GetCustomAttributes(inherit).OfType<TAttribute>();
        return declaringTypeCustomAttributes != null
            ? customAttributes.Concat(declaringTypeCustomAttributes).Distinct()
            : customAttributes;
    }

    /// <summary>
    /// 通过完整属性路径从给定对象获取属性值
    /// </summary>
    /// <param name="obj">目标对象</param>
    /// <param name="objectType">对象类型</param>
    /// <param name="propertyPath">属性路径（支持嵌套属性，用点分隔）</param>
    /// <returns>属性值，如果属性不存在则返回 null</returns>
    public static object? GetValueByPath(object obj, Type objectType, string propertyPath)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(objectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);

        var value = obj;
        var currentType = objectType;
        var objectPath = currentType.FullName;
        var absolutePropertyPath = propertyPath;
        if (objectPath != null && absolutePropertyPath.StartsWith(objectPath))
        {
            absolutePropertyPath = absolutePropertyPath.Replace(objectPath + ".", "");
        }

        foreach (var propertyName in absolutePropertyPath.Split('.'))
        {
            var property = currentType.GetProperty(propertyName);
            if (property != null)
            {
                if (value != null)
                {
                    value = property.GetValue(value, null);
                }
                currentType = property.PropertyType;
            }
            else
            {
                value = null;
                break;
            }
        }

        return value;
    }

    /// <summary>
    /// 递归获取指定类型中的所有公共常量值（包括基类型）
    /// </summary>
    /// <param name="type">目标类型</param>
    /// <returns>常量值数组</returns>
    public static string[] GetPublicConstantsRecursively(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        const int MaxRecursiveParameterValidationDepth = 8;
        var publicConstants = new List<string>();

        static void Recursively(List<string> constants, Type targetType, int currentDepth)
        {
            if (currentDepth > MaxRecursiveParameterValidationDepth)
            {
                return;
            }

            constants.AddRange(targetType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(x => x.IsLiteral && !x.IsInitOnly)
                .Select(x => x.GetValue(null)!.ToString()!));

            var nestedTypes = targetType.GetNestedTypes(BindingFlags.Public);

            foreach (var nestedType in nestedTypes)
            {
                Recursively(constants, nestedType, currentDepth + 1);
            }
        }

        Recursively(publicConstants, type, 1);

        return [.. publicConstants];
    }

    /// <summary>
    /// 通过完整属性路径设置给定对象的属性值
    /// </summary>
    /// <param name="obj">目标对象</param>
    /// <param name="objectType">对象类型</param>
    /// <param name="propertyPath">属性路径（支持嵌套属性，用点分隔）</param>
    /// <param name="value">要设置的值</param>
    internal static void SetValueByPath(object obj, Type objectType, string propertyPath, object value)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(objectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);

        var currentType = objectType;
        PropertyInfo property;
        var objectPath = currentType.FullName!;
        var absolutePropertyPath = propertyPath;
        if (absolutePropertyPath.StartsWith(objectPath))
        {
            absolutePropertyPath = absolutePropertyPath.Replace(objectPath + ".", "");
        }

        var properties = absolutePropertyPath.Split('.');

        if (properties.Length == 1)
        {
            property = objectType.GetProperty(properties.First())!;
            property.SetValue(obj, value);
            return;
        }

        for (var i = 0; i < properties.Length - 1; i++)
        {
            property = currentType.GetProperty(properties[i])!;
            obj = property.GetValue(obj, null)!;
            currentType = property.PropertyType;
        }

        property = currentType.GetProperty(properties.Last())!;
        property.SetValue(obj, value);
    }

    /// <summary>
    /// 添加实现的泛型类型到结果列表
    /// </summary>
    /// <param name="result">结果列表</param>
    /// <param name="givenType">给定类型</param>
    /// <param name="genericType">泛型类型</param>
    private static void AddImplementedGenericTypes(List<Type> result, Type givenType, Type genericType)
    {
        var givenTypeInfo = givenType.GetTypeInfo();

        if (givenTypeInfo.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
        {
            result.AddIfNotContains(givenType);
        }

        foreach (var interfaceType in givenTypeInfo.GetInterfaces())
        {
            if (interfaceType.GetTypeInfo().IsGenericType && interfaceType.GetGenericTypeDefinition() == genericType)
            {
                result.AddIfNotContains(interfaceType);
            }
        }

        if (givenTypeInfo.BaseType == null)
        {
            return;
        }

        AddImplementedGenericTypes(result, givenTypeInfo.BaseType, genericType);
    }

    #endregion 类型

    #region 获取包含有某特性的类

    /// <summary>
    /// 获取包含有某特性的类
    /// 第一种实现
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    /// <returns></returns>
    public static IEnumerable<Type> GetContainsAttributeTypes<TAttribute>()
        where TAttribute : Attribute
    {
        return GetAllTypes()
            .Where(e => e.CustomAttributes.Any(g => g.AttributeType == typeof(TAttribute)));
    }

    /// <summary>
    /// 获取包含有某特性的类
    /// 第二种实现
    /// </summary>
    /// <param name="attribute"></param>
    /// <returns></returns>
    public static IEnumerable<Type> GetContainsAttributeTypes(Attribute attribute)
    {
        return GetAllTypes()
            .Where(e => e.CustomAttributes.Any(g => g.AttributeType == attribute.GetType()));
    }

    #endregion 获取包含有某特性的类

    #region 获取不包含有某特性的类

    /// <summary>
    /// 获取不包含有某特性的类
    /// 第一种实现
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    /// <returns></returns>
    public static IEnumerable<Type> GetFilterAttributeTypes<TAttribute>()
        where TAttribute : Attribute
    {
        return GetAllTypes()
            .Where(e => e.CustomAttributes.All(g => g.AttributeType != typeof(TAttribute)));
    }

    /// <summary>
    /// 获取包含有某特性的类
    /// 第二种实现
    /// </summary>
    /// <param name="attribute"></param>
    /// <returns></returns>
    public static IEnumerable<Type> GetFilterAttributeTypes(Attribute attribute)
    {
        return GetAllTypes()
            .Where(e => e.CustomAttributes.All(g => g.AttributeType != attribute.GetType()));
    }

    #endregion 获取不包含有某特性的类

    #region 获取某类的子类(非抽象类)

    /// <summary>
    /// 获取某类的子类(非抽象类)
    /// 第一种实现
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IEnumerable<Type> GetSubClasses<T>()
        where T : class
    {
        return GetAllTypes()
            .Where(t => t is { IsInterface: false, IsClass: true, IsAbstract: false })
            .Where(t => typeof(T).IsAssignableFrom(t));
    }

    /// <summary>
    /// 获取某类的子类(非抽象类)
    /// 第二种实现
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static IEnumerable<Type> GetSubClasses(Type type)
    {
        return GetAllTypes()
            .Where(t => t is { IsInterface: false, IsClass: true, IsAbstract: false })
            .Where(type.IsAssignableFrom);
    }

    /// <summary>
    /// 获取某泛型接口的子类(非抽象类)
    /// </summary>
    /// <param name="interfaceType"></param>
    /// <returns></returns>
    public static IEnumerable<Type> GetSubClassesByGenericInterface(Type interfaceType)
    {
        return [.. GetAllTypes()
            .Where(type => type is { IsInterface: false, IsClass: true, IsAbstract: false }
                && type.GetInterfaces().Any(i => i.IsGenericType
                    && i.GetGenericTypeDefinition() == interfaceType))];
    }

    #endregion 获取某类的子类(非抽象类)

    #region 获取继承自某类的包含有某特性的接口、类的子类(非抽象类)

    /// <summary>
    /// 获取继承自某类的包含有某特性的接口、类的子类(非抽象类)
    /// 第一种实现
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TAttribute"></typeparam>
    /// <returns></returns>
    public static IEnumerable<Type> GetContainsAttributeSubClasses<T, TAttribute>()
        where T : class
        where TAttribute : Attribute
    {
        return GetSubClasses<T>().Intersect(GetContainsAttributeTypes<TAttribute>());
    }

    /// <summary>
    /// 获取继承自某类的包含有某特性的接口、类的子类(非抽象类)
    /// 第二种实现
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    /// <param name="type"></param>
    /// <returns></returns>
    public static IEnumerable<Type> GetContainsAttributeSubClasses<TAttribute>(Type type)
        where TAttribute : Attribute
    {
        return GetSubClasses(type).Intersect(GetContainsAttributeTypes<TAttribute>());
    }

    #endregion 获取继承自某类的包含有某特性的接口、类的子类(非抽象类)

    #region 获取继承自某类的不包含有某特性的接口、类的子类(非抽象类)

    /// <summary>
    /// 获取继承自某类的不包含有某特性的接口、类的子类(非抽象类)
    /// 第一种实现
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TAttribute"></typeparam>
    /// <returns></returns>
    public static IEnumerable<Type> GetFilterAttributeSubClass<T, TAttribute>()
        where T : class
        where TAttribute : Attribute
    {
        return GetSubClasses<T>().Intersect(GetFilterAttributeTypes<TAttribute>());
    }

    /// <summary>
    /// 获取继承自某类的不包含有某特性的接口、类的子类(非抽象类)
    /// 第二种实现
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    /// <param name="type"></param>
    /// <returns></returns>
    public static IEnumerable<Type> GetFilterAttributeSubClass<TAttribute>(Type type)
        where TAttribute : Attribute
    {
        return GetSubClasses(type).Intersect(GetFilterAttributeTypes<TAttribute>());
    }

    #endregion 获取继承自某类的不包含有某特性的接口、类的子类(非抽象类)

    #region 程序集依赖包

    /// <summary>
    /// 获取当前应用程序的 NuGet 程序包依赖项
    /// </summary>
    /// <param name="prefix">前缀名</param>
    /// <returns>NuGet 包信息列表</returns>
    public static List<NuGetPackage> GetNuGetPackages(string prefix)
    {
        var nugetPackages = new Dictionary<string, NuGetPackage>();

        // 获取当前应用所有程序集
        var assemblies = GetEffectivePatchAssemblies(prefix, "dll");

        // 查找被引用程序集中的 NuGet 库依赖项
        foreach (var assembly in assemblies)
        {
            try
            {
                var referencedAssemblies = assembly.GetReferencedAssemblies()
                    .Where(s => !s.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        !s.FullName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) &&
                        !s.FullName.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                    .Where(s => !string.IsNullOrEmpty(s.Name) && s.Version != null);

                foreach (var referencedAssembly in referencedAssemblies)
                {
                    // 检查引用的程序集是否来自 NuGet
                    if (string.IsNullOrEmpty(referencedAssembly.Name) || referencedAssembly.Version == null)
                    {
                        continue;
                    }

                    var packageName = referencedAssembly.Name;
                    var packageVersion = referencedAssembly.Version.ToString();

                    // 避免重复添加相同的 NuGet 包，保留版本更高的
                    if (!nugetPackages.TryGetValue(packageName, out var value))
                    {
                        nugetPackages[packageName] = new NuGetPackage
                        {
                            PackageName = packageName,
                            PackageVersion = packageVersion
                        };
                    }
                    else
                    {
                        var existingVersion = Version.Parse(value.PackageVersion);
                        var currentVersion = referencedAssembly.Version;

                        if (currentVersion > existingVersion)
                        {
                            nugetPackages[packageName] = new NuGetPackage
                            {
                                PackageName = packageName,
                                PackageVersion = packageVersion
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing assembly {assembly.FullName}: {ex.Message}");
            }
        }

        return [.. nugetPackages.Values.OrderBy(p => p.PackageName)];
    }

    #endregion 程序集依赖包

    #region 私有方法

    /// <summary>
    /// 判断程序集是否为系统程序集（如微软官方程序集）
    /// </summary>
    /// <param name="assembly">要检查的程序集</param>
    /// <returns>如果是系统程序集则返回 true，否则返回 false</returns>
    private static bool IsSystemAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var asmCompanyAttr = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
        if (asmCompanyAttr is null)
        {
            return false;
        }

        var companyName = asmCompanyAttr.Company;
        return companyName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 通过程序集路径判断是否为系统程序集
    /// </summary>
    /// <param name="assemblyPath">程序集文件路径</param>
    /// <returns>如果是系统程序集则返回 true，否则返回 false</returns>
    private static bool IsSystemAssembly(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            return IsSystemAssembly(assembly);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 判断程序集是否有效（能否正常加载类型）
    /// </summary>
    /// <param name="assembly">要验证的程序集</param>
    /// <returns>如果程序集有效则返回 true，否则返回 false</returns>
    private static bool IsValid(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        try
        {
            _ = assembly.GetTypes();
            _ = assembly.DefinedTypes.ToList();
            return true;
        }
        catch (ReflectionTypeLoadException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 判断指定文件是否为托管程序集
    /// </summary>
    /// <param name="file">程序集文件路径</param>
    /// <returns>如果是托管程序集则返回 true，否则返回 false</returns>
    private static bool IsManagedAssembly(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        try
        {
            using var fs = File.OpenRead(file);
            using PEReader peReader = new(fs);
            return peReader.HasMetadata && peReader.GetMetadataReader().IsAssembly;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 安全地尝试加载程序集，处理各种加载异常
    /// </summary>
    /// <param name="assemblyPath">程序集文件路径</param>
    /// <returns>成功加载的程序集，失败则返回 null</returns>
    private static Assembly? TryLoadAssembly(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            return null;
        }

        Assembly? assembly = null;

        try
        {
            var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
            assembly = Assembly.Load(assemblyName);
        }
        catch (BadImageFormatException ex)
        {
            Debug.WriteLine($"BadImageFormatException loading assembly {assemblyPath}: {ex.Message}");
        }
        catch (FileLoadException ex)
        {
            Debug.WriteLine($"FileLoadException loading assembly {assemblyPath}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception loading assembly {assemblyPath}: {ex.Message}");
        }

        if (assembly is not null)
        {
            return assembly;
        }

        try
        {
            assembly = Assembly.LoadFile(assemblyPath);
        }
        catch (BadImageFormatException ex)
        {
            Debug.WriteLine($"BadImageFormatException loading file {assemblyPath}: {ex.Message}");
        }
        catch (FileLoadException ex)
        {
            Debug.WriteLine($"FileLoadException loading file {assemblyPath}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exception loading file {assemblyPath}: {ex.Message}");
        }

        return assembly;
    }

    #endregion 私有方法
}

/// <summary>
/// 程序集相等性比较器
/// 用于比较两个程序集是否相等，基于程序集名称进行匹配
/// </summary>
internal class AssemblyEquality : EqualityComparer<Assembly>
{
    /// <summary>
    /// 比较两个程序集是否相等
    /// </summary>
    /// <param name="x">第一个程序集</param>
    /// <param name="y">第二个程序集</param>
    /// <returns>如果两个程序集相等则返回 true，否则返回 false</returns>
    public override bool Equals(Assembly? x, Assembly? y)
    {
        return (x is null && y is null) ||
               (x is not null && y is not null &&
                AssemblyName.ReferenceMatchesDefinition(x.GetName(), y.GetName()));
    }

    /// <summary>
    /// 获取程序集的哈希代码
    /// </summary>
    /// <param name="obj">程序集对象</param>
    /// <returns>程序集的哈希代码</returns>
    /// <exception cref="ArgumentNullException">当程序集为 null 时</exception>
    public override int GetHashCode(Assembly obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return obj.GetName().FullName.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// NuGet 程序包信息记录
/// 表示一个 NuGet 包的基本信息，包括包名和版本
/// </summary>
public record NuGetPackage
{
    /// <summary>
    /// NuGet 包名称
    /// </summary>
    public string PackageName { get; init; } = string.Empty;

    /// <summary>
    /// NuGet 包版本号
    /// </summary>
    public string PackageVersion { get; init; } = string.Empty;

    /// <summary>
    /// 获取包的完整标识符
    /// </summary>
    /// <returns>格式为 "PackageName@PackageVersion" 的字符串</returns>
    public override string ToString()
    {
        return $"{PackageName}@{PackageVersion}";
    }
}
