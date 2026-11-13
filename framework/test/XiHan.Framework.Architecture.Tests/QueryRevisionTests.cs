// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using XiHan.Framework.Architecture.Tests.Model;
using XiHan.Framework.Kernel;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 查询修订号门禁（RFC-0001「Provider 契约的演进」，ADR-0228）
/// </summary>
/// <remarks>
/// 交给 Provider 的查询带修订号常量，Provider 在入口以编译时取到的常量核对。查询加了新条件、旧 Provider 却不核对，就会静默忽略新条件，给出放宽了的结果
/// </remarks>
public sealed class QueryRevisionTests
{
    /// <summary>
    /// 修订号常量的名字
    /// </summary>
    private const string RevisionField = "Revision";

    /// <summary>
    /// 核对方法的名字
    /// </summary>
    private const string ValidateMethod = "Validate";

    /// <summary>
    /// 5.0 交给 Provider 的查询类型数
    /// </summary>
    /// <remarks>
    /// 少了说明有查询丢了修订号；新增查询类型时同步调大
    /// </remarks>
    private const int ExpectedQueryTypes = 12;

    [Fact]
    public void QueryTypes_RejectProvidersCompiledAgainstAnOlderRevision()
    {
        var failures = new List<string>();
        var queries = QueryTypes().ToList();
        Assert.Equal(ExpectedQueryTypes, queries.Count);

        foreach (var query in queries)
        {
            var closed = query.IsGenericTypeDefinition ? query.MakeGenericType(typeof(object)) : query;
            var field = closed.GetField(RevisionField, BindingFlags.Public | BindingFlags.Static);
            var validate = closed.GetMethod(ValidateMethod, [typeof(int)]);
            Assert.NotNull(field);
            Assert.NotNull(validate);
            var revision = Assert.IsType<int>(field.GetRawConstantValue());

            // 修订号先于其余校验核对，未初始化的实例走不到读其余字段那一步
            var instance = RuntimeHelpers.GetUninitializedObject(closed);
            var older = Assert.IsType<Result>(validate.Invoke(instance, [revision - 1]));
            if (older.Error?.Code != KernelErrors.QueryRevisionUnsupported)
            {
                failures.Add($"'{query.FullName}.Validate({revision - 1})' must fail with '{KernelErrors.QueryRevisionUnsupported}', got '{older.Error?.Code ?? "success"}'.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Providers_CheckTheQueryRevisionAtEveryEntry()
    {
        var queries = QueryTypes().ToHashSet();
        var failures = new List<string>();

        // 按声明实现方法的类型汇总：它实现了几个接收某查询的接口方法，源文件里就至少要引用几次那个查询的修订号常量
        var assemblies = NamingTests.LoadSourceAssemblies().ToList();
        var frameworkAssemblies = assemblies.ToHashSet();
        var entries = new Dictionary<(Type Declaring, Type Query), int>();
        foreach (var assembly in assemblies)
        {
            // 工具链包（一致性套件、测试支持）里是测试替身与转发的装饰器，随框架一起编译（ADR-0157 的同一理由），不会按旧修订号编译
            var name = assembly.GetName().Name ?? string.Empty;
            if (name.EndsWith(".Conformance", StringComparison.Ordinal) || name.EndsWith(".Testing", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var type in assembly.GetTypes().Where(static type => !type.IsInterface && !(type.IsAbstract && type.IsSealed)))
            {
                // 只看框架自己声明的接口：记录自带的 IEquatable<T> 也收查询类型，但不是 Provider 契约
                foreach (var contract in type.GetInterfaces().Where(contract => frameworkAssemblies.Contains(contract.Assembly)))
                {
                    var map = type.GetInterfaceMap(contract);
                    for (var i = 0; i < map.InterfaceMethods.Length; i++)
                    {
                        if (QueryParameter(map.InterfaceMethods[i], queries) is not { } query || map.TargetMethods[i].DeclaringType is not { } declaring || declaring.IsInterface)
                        {
                            continue;
                        }

                        // 与查询类型同一程序集的实现总是和查询一起编译，不可能按旧修订号编译；它们多是转给存储的服务，由存储核对
                        if (declaring.Assembly == query.Assembly)
                        {
                            continue;
                        }

                        var key = (Definition(declaring), query);
                        entries[key] = entries.GetValueOrDefault(key) + 1;
                    }
                }
            }
        }

        Assert.NotEmpty(entries);
        foreach (var ((declaring, query), count) in entries)
        {
            if (SourceFile(declaring) is not { } file)
            {
                failures.Add($"'{declaring.FullName}': source file not found.");
                continue;
            }

            var reference = new Regex($@"\b{Regex.Escape(Name(query))}(<\w+>)?\.{RevisionField}\b");
            var found = reference.Count(File.ReadAllText(file));
            if (found < count)
            {
                failures.Add($"'{Workspace.Relative(file)}': implements {count} method(s) taking '{Name(query)}' but references '{Name(query)}.{RevisionField}' {found} time(s); call 'query.{ValidateMethod}({Name(query)}.{RevisionField})' at each entry.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 带修订号常量与 <c>Validate(int)</c> 的公开查询类型
    /// </summary>
    private static IEnumerable<Type> QueryTypes() =>
        NamingTests.LoadSourceAssemblies()
            .SelectMany(static assembly => assembly.GetExportedTypes())
            .Where(static type => type.GetField(RevisionField, BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true } field && field.FieldType == typeof(int)
                && type.GetMethod(ValidateMethod, [typeof(int)]) is { ReturnType: var returns } && returns == typeof(Result));

    /// <summary>
    /// 接口方法参数里的查询类型（泛型查询按定义比较）
    /// </summary>
    private static Type? QueryParameter(MethodInfo method, HashSet<Type> queries)
    {
        foreach (var parameter in method.GetParameters())
        {
            var type = Definition(parameter.ParameterType);
            if (queries.Contains(type))
            {
                return type;
            }
        }

        return null;
    }

    /// <summary>
    /// 泛型类型取定义，其余原样
    /// </summary>
    private static Type Definition(Type type) => type.IsGenericType && !type.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type;

    /// <summary>
    /// 去掉泛型元数后的类型名
    /// </summary>
    private static string Name(Type type) => type.IsGenericType ? type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)] : type.Name;

    /// <summary>
    /// 类型所在的源文件
    /// </summary>
    /// <remarks>
    /// 一个类型一个文件：嵌套类型在最外层类型的文件里，泛型类型的文件名加 <c>OfT</c>
    /// </remarks>
    private static string? SourceFile(Type type)
    {
        var outer = type;
        while (outer.DeclaringType is { } parent)
        {
            outer = parent;
        }

        var project = Workspace.SourceProjects.FirstOrDefault(p => p.PackageId == type.Assembly.GetName().Name);
        if (project is null)
        {
            return null;
        }

        var fileName = Name(outer) + (outer.IsGenericType ? "OfT" : string.Empty) + ".cs";
        return Workspace.SourceFiles(project.Directory).FirstOrDefault(f => Path.GetFileName(f) == fileName);
    }
}
