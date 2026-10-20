// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace XiHan.Framework.Kernel.Analyzers.Tests;

public sealed class AnalyzerTests
{
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

    private static Task RunAsync<TAnalyzer>(string source, bool frameworkPackage, int ring = -1, params DiagnosticResult[] expected)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.TestState.Sources.Add(ApiLevelSource);
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
}
