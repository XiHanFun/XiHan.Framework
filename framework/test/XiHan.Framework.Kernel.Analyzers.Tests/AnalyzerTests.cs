// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace XiHan.Framework.Kernel.Analyzers.Tests;

/// <summary>
/// 八条规则的端到端验证
/// </summary>
/// <remarks>
/// 用内联源码与 <c>.globalconfig</c> 模拟框架包、环层与跨程序集引用
/// </remarks>
public sealed class AnalyzerTests
{
    /// <summary>
    /// 原语层里 <c>ApiLevelAttribute</c> 与 <c>Stability</c> 的最小替身
    /// </summary>
    /// <remarks>
    /// 分析器按元数据名查找，不依赖真实程序集
    /// </remarks>
    private const string ApiLevelSource = """
        namespace XiHan.Framework.Primitives
        {
            [ApiLevel(Stability.Stable, "5.0")]
            public enum Stability { Stable = 0, Preview = 1, Experimental = 2, Internal = 3, Deprecated = 4 }

            [System.AttributeUsage(System.AttributeTargets.All)]
            [ApiLevel(Stability.Stable, "5.0")]
            public sealed class ApiLevelAttribute : System.Attribute
            {
                public ApiLevelAttribute(Stability stability, string since) { }
            }
        }
        """;

    /// <summary>
    /// 以指定的框架包与环层设置运行单个分析器，并核对期望的诊断
    /// </summary>
    private static Task RunAsync<TAnalyzer>(string source, bool frameworkPackage, int ring = -1, params DiagnosticResult[] expected)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(ApiLevelSource);

        // 分析器从 build_property.* 读取范围，这里用全局 .globalconfig 模拟 MSBuild 的 CompilerVisibleProperty
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", $"""
            is_global = true
            build_property.XiHanFrameworkPackage = {(frameworkPackage ? "true" : "false")}
            build_property.XiHanRing = {ring}
            """));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    [Fact]
    public Task XH0001_PublicTypeWithoutApiLevel_InFrameworkPackage_IsReported() => RunAsync<PublicTypeAnalyzer>(
        """
        namespace Demo
        {
            public sealed class {|#0:Widget|} { }

            [XiHan.Framework.Primitives.ApiLevel(XiHan.Framework.Primitives.Stability.Stable, "5.0")]
            public sealed class Annotated { }

            internal sealed class Hidden { }
        }
        """,
        frameworkPackage: true,
        expected: DiagnosticResult.CompilerError("XH0001").WithLocation(0).WithArguments("Widget"));

    [Fact]
    public Task XH0001_NotReported_OutsideFrameworkPackages() => RunAsync<PublicTypeAnalyzer>(
        """
        namespace Demo
        {
            public sealed class Widget { }
        }
        """,
        frameworkPackage: false);

    [Fact]
    public Task XH0002_And_XH0004_NamingRules() => RunAsync<PublicTypeAnalyzer>(
        """
        using XiHan.Framework.Primitives;

        namespace Demo
        {
            [ApiLevel(Stability.Stable, "5.0")]
            public sealed class {|#0:XiHanWidget|} { }

            [ApiLevel(Stability.Stable, "5.0")]
            public sealed class {|#1:WidgetManager|} { }

            [ApiLevel(Stability.Stable, "5.0")]
            public sealed class {|#2:DefaultWidget|} { }

            [ApiLevel(Stability.Stable, "5.0")]
            public sealed class Widget { }

            [ApiLevel(Stability.Stable, "5.0")]
            public static class Default { }
        }
        """,
        frameworkPackage: true,
        expected:
        [
            DiagnosticResult.CompilerError("XH0002").WithLocation(0).WithArguments("XiHanWidget"),
            DiagnosticResult.CompilerError("XH0004").WithLocation(1).WithArguments("WidgetManager", "Manager"),
            DiagnosticResult.CompilerError("XH0004").WithLocation(2).WithArguments("DefaultWidget", "Default"),
        ]);

    [Fact]
    public async Task XH0003_InternalApi_FromAnotherAssembly_IsReported()
    {
        var test = new CSharpAnalyzerTest<InternalApiUsageAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                public static class Consumer
                {
                    public static void Use()
                    {
                        var hidden = new Library.{|#0:Hidden|}();
                        Library.{|#1:Hidden|}.Run();
                        Library.Visible.Run();
                        {|#2:Library.Visible.Secret()|};
                    }
                }
                """,
        };

        // XH0003 只报跨程序集的使用，所以被引用的类型放在单独的 Library 项目里
        var library = new ProjectState("Library", LanguageNames.CSharp, "Library", "cs");
        library.Sources.Add(ApiLevelSource);
        library.Sources.Add("""
            using XiHan.Framework.Primitives;

            namespace Library
            {
                [ApiLevel(Stability.Internal, "5.0")]
                public sealed class Hidden
                {
                    public static void Run() { }
                }

                [ApiLevel(Stability.Stable, "5.0")]
                public static class Visible
                {
                    public static void Run() { }

                    [ApiLevel(Stability.Internal, "5.0")]
                    public static void Secret() { }
                }
            }
            """);
        test.TestState.AdditionalProjects.Add("Library", library);
        test.TestState.AdditionalProjectReferences.Add("Library");
        test.ExpectedDiagnostics.AddRange(
        [
            DiagnosticResult.CompilerError("XH0003").WithLocation(0).WithArguments("Hidden"),
            DiagnosticResult.CompilerError("XH0003").WithLocation(1).WithArguments("Hidden"),
            DiagnosticResult.CompilerError("XH0003").WithLocation(2).WithArguments("Visible.Secret()"),
        ]);

        await test.RunAsync();
    }

    [Fact]
    public Task XH0005_ReflectionInRingOne_IsReported() => RunAsync<ReflectionUsageAnalyzer>(
        """
        public static class Kernel
        {
            public static void Run(object value)
            {
                var type = value.GetType();
                var name = type.Name;
                _ = {|#0:type.GetMethod("Run")|};
                _ = {|#1:System.Activator.CreateInstance(type)|};
                _ = {|#2:System.Type.GetType("Kernel")|};
                _ = {|#3:typeof(Kernel).Assembly|};
            }
        }
        """,
        frameworkPackage: true,
        ring: 1,
        expected:
        [
            DiagnosticResult.CompilerError("XH0005").WithLocation(0).WithArguments("Type.GetMethod(string)"),
            DiagnosticResult.CompilerError("XH0005").WithLocation(1).WithArguments("Activator.CreateInstance(Type)"),
            DiagnosticResult.CompilerError("XH0005").WithLocation(2).WithArguments("Type.GetType(string)"),
            DiagnosticResult.CompilerError("XH0005").WithLocation(3).WithArguments("Type.Assembly"),
        ]);

    [Fact]
    public Task XH0005_NotReported_OutsideRingZeroAndOne() => RunAsync<ReflectionUsageAnalyzer>(
        """
        public static class Host
        {
            public static object Run(System.Type type) => System.Activator.CreateInstance(type)!;
        }
        """,
        frameworkPackage: true,
        ring: 5);

    [Fact]
    public Task XH0006_StableSignatureReferencingUnstableTypes_IsReported() => RunAsync<StabilityDependencyAnalyzer>(
        """
        using System.Collections.Generic;
        using XiHan.Framework.Primitives;

        namespace Demo
        {
            [ApiLevel(Stability.Preview, "5.0")]
            public sealed class Draft { }

            [ApiLevel(Stability.Experimental, "5.0")]
            public interface ITrial { }

            [ApiLevel(Stability.Preview, "5.0")]
            public abstract class DraftBase { }

            [ApiLevel(Stability.Stable, "5.0")]
            public sealed class {|#0:Settled|} : DraftBase, ITrial
            {
                private readonly Draft _field = new Draft();

                public Draft {|#1:Current|} { get; } = new Draft();

                public IReadOnlyList<Draft> {|#2:All|}() => new List<Draft>();

                public void {|#3:Take|}(Draft[] drafts, Draft single) { }

                internal Draft Hidden() => _field;

                [ApiLevel(Stability.Preview, "5.0")]
                public Draft Experimenting() => _field;
            }

            [ApiLevel(Stability.Preview, "5.0")]
            public sealed class Loose
            {
                public Draft Current { get; } = new Draft();
            }
        }
        """,
        frameworkPackage: true,
        expected:
        [
            DiagnosticResult.CompilerError("XH0006").WithLocation(0).WithArguments("Settled", "DraftBase", "Preview"),
            DiagnosticResult.CompilerError("XH0006").WithLocation(0).WithArguments("Settled", "ITrial", "Experimental"),
            DiagnosticResult.CompilerError("XH0006").WithLocation(1).WithArguments("Settled.Current", "Draft", "Preview"),
            DiagnosticResult.CompilerError("XH0006").WithLocation(2).WithArguments("Settled.All()", "Draft", "Preview"),
            DiagnosticResult.CompilerError("XH0006").WithLocation(3).WithArguments("Settled.Take(Draft[], Draft)", "Draft", "Preview"),
        ]);

    [Fact]
    public Task XH0006_NotReported_OutsideFrameworkPackages() => RunAsync<StabilityDependencyAnalyzer>(
        """
        using XiHan.Framework.Primitives;

        namespace Demo
        {
            [ApiLevel(Stability.Preview, "5.0")]
            public sealed class Draft { }

            [ApiLevel(Stability.Stable, "5.0")]
            public static class Settled
            {
                public static Draft Create() => new Draft();
            }
        }
        """,
        frameworkPackage: false);

    [Fact]
    public async Task XH0006_UnstableTypeFromAnotherAssembly_IsReported()
    {
        var test = new CSharpAnalyzerTest<StabilityDependencyAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                using XiHan.Framework.Primitives;

                namespace Demo
                {
                    [ApiLevel(Stability.Stable, "5.0")]
                    public static class Consumer
                    {
                        public static Library.Draft {|#0:Create|}() => new Library.Draft();

                        public static Library.Settled Settle() => new Library.Settled();
                    }
                }
                """,
        };

        // 被引用的类型来自元数据，级别经元数据上的特性读出
        var library = new ProjectState("Library", LanguageNames.CSharp, "Library", "cs");
        library.Sources.Add(ApiLevelSource);
        library.Sources.Add("""
            using XiHan.Framework.Primitives;

            namespace Library
            {
                [ApiLevel(Stability.Preview, "5.0")]
                public sealed class Draft { }

                [ApiLevel(Stability.Stable, "5.0")]
                public sealed class Settled { }
            }
            """);
        test.TestState.AdditionalProjects.Add("Library", library);
        test.TestState.AdditionalProjectReferences.Add("Library");
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", """
            is_global = true
            build_property.XiHanFrameworkPackage = true
            """));
        test.ExpectedDiagnostics.Add(DiagnosticResult.CompilerError("XH0006").WithLocation(0).WithArguments("Consumer.Create()", "Draft", "Preview"));

        await test.RunAsync();
    }

    [Fact]
    public Task XH0007_MemberAnnotatedAboveItsType_IsReported() => RunAsync<StabilityDependencyAnalyzer>(
        """
        using XiHan.Framework.Primitives;

        namespace Demo
        {
            [ApiLevel(Stability.Preview, "5.0")]
            public sealed class Draft
            {
                [ApiLevel(Stability.Stable, "5.0")]
                public void {|#0:Promise|}() { }

                [ApiLevel(Stability.Experimental, "5.0")]
                public void Try() { }

                [ApiLevel(Stability.Stable, "5.0")]
                public sealed class {|#1:Inner|} { }
            }
        }
        """,
        frameworkPackage: true,
        expected:
        [
            DiagnosticResult.CompilerError("XH0007").WithLocation(0).WithArguments("Draft.Promise()", "Stable", "Preview", "Draft"),
            DiagnosticResult.CompilerError("XH0007").WithLocation(1).WithArguments("Draft.Inner", "Stable", "Preview", "Draft"),
        ]);

    [Fact]
    public Task XH0008_StableOptionalParameters_AreReported() => RunAsync<OptionalParameterAnalyzer>(
        """
        using System.Runtime.CompilerServices;
        using XiHan.Framework.Primitives;

        namespace Demo
        {
            [ApiLevel(Stability.Stable, "5.0")]
            public interface IRunner
            {
                void Run(int {|#0:times|} = 1);
            }

            [ApiLevel(Stability.Stable, "5.0")]
            public sealed class Runner : IRunner
            {
                public Runner(string name, int {|#1:capacity|} = 10) { }

                public void Run(int times = 1) { }

                public static string Check(string value, [CallerArgumentExpression(nameof(value))] string? name = null) => value;

                public void Stop(bool {|#2:force|} = false, string? {|#3:reason|} = null) { }

                internal void Hidden(int count = 0) { }

                [ApiLevel(Stability.Preview, "5.0")]
                public void Trial(int count = 0) { }
            }

            [ApiLevel(Stability.Preview, "5.0")]
            public static class Draft
            {
                public static void Run(int count = 0) { }
            }
        }
        """,
        frameworkPackage: true,
        expected:
        [
            DiagnosticResult.CompilerError("XH0008").WithLocation(0).WithArguments("IRunner.Run(int)", "times"),
            DiagnosticResult.CompilerError("XH0008").WithLocation(1).WithArguments("Runner.Runner(string, int)", "capacity"),
            DiagnosticResult.CompilerError("XH0008").WithLocation(2).WithArguments("Runner.Stop(bool, string)", "force"),
            DiagnosticResult.CompilerError("XH0008").WithLocation(3).WithArguments("Runner.Stop(bool, string)", "reason"),
        ]);

    [Fact]
    public async Task XH0008_NotReported_InToolingPackages()
    {
        var test = new CSharpAnalyzerTest<OptionalParameterAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = """
                using XiHan.Framework.Primitives;

                namespace Demo
                {
                    [ApiLevel(Stability.Stable, "5.0")]
                    public static class Host
                    {
                        public static void Start(int port = 0) { }
                    }
                }
                """,
        };
        test.TestState.Sources.Add(ApiLevelSource);
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", """
            is_global = true
            build_property.XiHanFrameworkPackage = true
            """));

        // 面向源码使用的工具链包按程序集名识别
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectAssemblyName(projectId, "Demo.Testing"));

        await test.RunAsync();
    }
}
