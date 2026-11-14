// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 查找命名门禁：名字决定「没有」怎么表示
/// </summary>
/// <remarks>
/// 规则见 RFC-0001「查找的命名」：<c>Find…</c> 返回 <see cref="Maybe{T}"/>，<c>TryGet…</c> 返回 <see langword="bool"/> 加 <c>out</c>，<c>Get…</c> 必定交出值。缓存的键值读取与实现外部接口的成员除外
/// </remarks>
public sealed class LookupNamingTests
{
    [Fact]
    public void TryGetMethods_ReturnBoolWithAnOutParameter()
    {
        var failures = new List<string>();
        foreach (var method in PublicMethods().Where(static m => m.Name.StartsWith("TryGet", StringComparison.Ordinal)))
        {
            if (method.ReturnType != typeof(bool) || !method.GetParameters().Any(static p => p.IsOut))
            {
                failures.Add($"'{method.DeclaringType!.FullName}.{method.Name}' must return 'bool' with an 'out' parameter; a lookup returning 'Maybe' is named 'Find…'.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void GetMethods_AlwaysProduceAValue()
    {
        var nullability = new NullabilityInfoContext();
        var failures = new List<string>();
        foreach (var method in PublicMethods().Where(static m => m.Name.StartsWith("Get", StringComparison.Ordinal)))
        {
            // 缓存条目的读沿用键值存储的词汇（批量优先的命名表），缓存接口的实现（如测试支持里的缓存包装）同样照接口命名；外部接口的成员照原接口命名
            if (method.DeclaringType!.Namespace?.StartsWith("XiHan.Framework.Caching", StringComparison.Ordinal) == true || ImplementsCachingInterface(method) || ImplementsForeignInterface(method))
            {
                continue;
            }

            if (ContainsMaybe(method.ReturnType) || (!method.ReturnType.IsValueType && nullability.Create(method.ReturnParameter).ReadState == NullabilityState.Nullable))
            {
                failures.Add($"'{method.DeclaringType.FullName}.{method.Name}' may return no value; name it 'Find…' and return 'Maybe', or 'TryGet…' with an 'out' parameter.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 全部正式包公开类型上声明的公开方法
    /// </summary>
    /// <remarks>
    /// 不含属性访问器、运算符与编译器生成的成员
    /// </remarks>
    private static IEnumerable<MethodInfo> PublicMethods()
        => NamingTests.LoadSourceAssemblies()
            .SelectMany(static assembly => assembly.GetExportedTypes())
            .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(static method => !method.IsSpecialName && method.Name != nameof(GetHashCode) && method.Name != nameof(GetType));

    /// <summary>
    /// 类型里是否含 <see cref="Maybe{T}"/>（含任务与结果的包装）
    /// </summary>
    private static bool ContainsMaybe(Type type)
        => type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Maybe<>) || type.GetGenericArguments().Any(ContainsMaybe));

    /// <summary>
    /// 是否实现了缓存子系统声明的接口成员
    /// </summary>
    private static bool ImplementsCachingInterface(MethodInfo method)
    {
        var type = method.DeclaringType!;
        if (type.IsInterface || method.IsStatic)
        {
            return false;
        }

        return type.GetInterfaces().Any(contract => contract.Namespace?.StartsWith("XiHan.Framework.Caching", StringComparison.Ordinal) == true && type.GetInterfaceMap(contract).TargetMethods.Contains(method));
    }

    /// <summary>
    /// 是否实现了框架之外声明的接口成员
    /// </summary>
    private static bool ImplementsForeignInterface(MethodInfo method)
    {
        var type = method.DeclaringType!;
        if (type.IsInterface || method.IsStatic)
        {
            return false;
        }

        foreach (var contract in type.GetInterfaces())
        {
            if (contract.Assembly.GetName().Name?.StartsWith("XiHan.Framework", StringComparison.Ordinal) == true)
            {
                continue;
            }

            if (type.GetInterfaceMap(contract).TargetMethods.Contains(method))
            {
                return true;
            }
        }

        return false;
    }
}
