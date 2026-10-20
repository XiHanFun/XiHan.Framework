// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace XiHan.Framework.Kernel.Analyzers;

/// <summary>
/// XH0005：Ring 0/1（<c>XiHanRing</c> 为 0 或 1）不得使用反射 API。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReflectionUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> ReflectiveTypeMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "GetMethod", "GetMethods", "GetProperty", "GetProperties", "GetField", "GetFields", "GetConstructor", "GetConstructors",
        "GetMember", "GetMembers", "GetEvent", "GetEvents", "GetNestedType", "GetNestedTypes", "InvokeMember", "MakeGenericType", "GetType",
        "GetInterface", "GetInterfaces", "GetCustomAttributes", "GetCustomAttributesData", "Assembly", "Module");

    /// <summary>
    /// <c>typeof(T).Name</c> 这类只读元数据不是反射：MemberInfo 上的这些成员放行。
    /// </summary>
    private static readonly ImmutableHashSet<string> HarmlessMemberInfoMembers = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Name", "DeclaringType", "ReflectedType", "MemberType", "MetadataToken", "IsCollectible");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rules.ReflectionInKernel);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var ring = Rules.Ring(start.Options);
            if (ring is not (0 or 1))
            {
                return;
            }

            start.RegisterOperationAction(
                Analyze,
                OperationKind.Invocation,
                OperationKind.ObjectCreation,
                OperationKind.PropertyReference,
                OperationKind.FieldReference,
                OperationKind.MethodReference);
        });
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        ISymbol? target = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IObjectCreationOperation creation => creation.Constructor,
            IMemberReferenceOperation reference => reference.Member,
            _ => null,
        };

        if (target?.ContainingType is null || !IsReflective(target))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rules.ReflectionInKernel,
            context.Operation.Syntax.GetLocation(),
            target.OriginalDefinition.ToDisplayString(Rules.DisplayFormat)));
    }

    private static bool IsReflective(ISymbol member)
    {
        var containingType = member.ContainingType;
        var ns = containingType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ns.StartsWith("System.Reflection", StringComparison.Ordinal))
        {
            return !(string.Equals(containingType.Name, "MemberInfo", StringComparison.Ordinal) && HarmlessMemberInfoMembers.Contains(member.Name));
        }

        if (string.Equals(ns, "System", StringComparison.Ordinal))
        {
            if (string.Equals(containingType.Name, "Activator", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(containingType.Name, "Type", StringComparison.Ordinal) && ReflectiveTypeMembers.Contains(member.Name))
            {
                // object.GetType() 不是反射；只拦 Type.GetType(string) 这类按名查找
                return !string.Equals(member.Name, "GetType", StringComparison.Ordinal) || member is IMethodSymbol { Parameters.Length: > 0 };
            }
        }

        return false;
    }
}
