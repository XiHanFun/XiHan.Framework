// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 本包的公开面不出现 AWS SDK 的类型（ADR-0266）
/// </summary>
/// <remarks>
/// 公开签名里有 SDK 的类型，SDK 升大版本就是本包的破坏性变更。核对导出类型的基类、接口与公开或受保护成员的签名；客户端只在包内与容器里出现
/// </remarks>
public sealed class S3PublicSurfaceTests
{
    /// <summary>
    /// AWS SDK 程序集名的前缀
    /// </summary>
    private const string SdkAssemblyPrefix = "AWSSDK.";

    [Fact]
    public void PublicSurface_DoesNotExposeAwsSdkTypes()
    {
        var failures = new List<string>();
        foreach (var type in typeof(S3BlobStore).Assembly.GetExportedTypes())
        {
            foreach (var inherited in Ancestors(type).Where(IsSdkType))
            {
                failures.Add($"'{type.FullName}' derives from or implements SDK type '{inherited.FullName}'.");
            }

            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(IsVisible))
            {
                foreach (var referenced in Signature(member).SelectMany(Flatten).Where(IsSdkType))
                {
                    failures.Add($"The signature of '{type.FullName}.{member.Name}' exposes SDK type '{referenced.FullName}'.");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Distinct(StringComparer.Ordinal)));
    }

    [Fact]
    public void TheCheck_RecognisesSdkTypes()
    {
        // 判定本身要认得出 SDK 的类型，否则上面的用例永远是绿的
        Assert.True(IsSdkType(typeof(Amazon.S3.IAmazonS3)));
        Assert.True(IsSdkType(typeof(Amazon.Runtime.AmazonServiceException)));
        Assert.Contains(typeof(Amazon.S3.IAmazonS3), Flatten(typeof(Func<S3ObjectStorageOptions, Amazon.S3.IAmazonS3[]>)));
        Assert.False(IsSdkType(typeof(S3BlobStore)));
    }

    /// <summary>
    /// 类型是否来自 AWS SDK
    /// </summary>
    private static bool IsSdkType(Type type)
        => (type.Assembly.GetName().Name ?? string.Empty).StartsWith(SdkAssemblyPrefix, StringComparison.Ordinal);

    /// <summary>
    /// 成员在包外是否可见（公开或受保护）
    /// </summary>
    private static bool IsVisible(MemberInfo member) => member switch
    {
        MethodBase method => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly,
        FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
        PropertyInfo property => property.GetAccessors(nonPublic: true).Any(IsVisible),
        EventInfo @event => (@event.AddMethod is { } add && IsVisible(add)) || (@event.RemoveMethod is { } remove && IsVisible(remove)),
        _ => false,
    };

    /// <summary>
    /// 基类链与实现的接口
    /// </summary>
    private static IEnumerable<Type> Ancestors(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            yield return current;
        }

        foreach (var contract in type.GetInterfaces())
        {
            yield return contract;
        }
    }

    /// <summary>
    /// 成员签名里直接出现的类型：返回类型、参数类型、属性、字段与事件处理器的类型
    /// </summary>
    private static IEnumerable<Type> Signature(MemberInfo member) => member switch
    {
        MethodInfo method => method.GetParameters().Select(static p => p.ParameterType).Prepend(method.ReturnType),
        ConstructorInfo constructor => constructor.GetParameters().Select(static p => p.ParameterType),
        PropertyInfo property => [property.PropertyType],
        FieldInfo field => [field.FieldType],
        EventInfo { EventHandlerType: { } handler } => [handler],
        _ => [],
    };

    /// <summary>
    /// 展开数组、引用与泛型实参，得到签名里实际涉及的各个类型
    /// </summary>
    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.GetElementType() is { } element)
        {
            return Flatten(element);
        }

        return type.IsGenericType ? type.GetGenericArguments().SelectMany(Flatten).Prepend(type) : [type];
    }
}
