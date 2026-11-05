// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.EntityFrameworkCore;

namespace XiHan.Framework.Persistence.Generators.Tests;

/// <summary>
/// 持久化生成器：仓储登记、EF Core 实体登记、过滤规约与诊断
/// </summary>
public sealed class EntityGeneratorTests
{
    /// <summary>
    /// 四个实体
    /// </summary>
    /// <remarks>
    /// 聚合根（软删除 + 多租户）、内部实体（int 键）、只软删除的实体、被跳过的抽象类
    /// </remarks>
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using XiHan.Framework.Domain.Entities;
        using XiHan.Framework.Domain.Events;

        namespace Demo.App;

        public sealed class Order : IAggregateRoot<Guid>, IMultiTenant, ISoftDeletable
        {
            public Guid Id { get; init; }
            public string TenantId { get; set; } = string.Empty;
            public bool IsDeleted { get; set; }
            public DateTimeOffset? DeletedAt { get; set; }
            public IReadOnlyList<IDomainEvent> DomainEvents => Array.Empty<IDomainEvent>();
            public void ClearDomainEvents() { }
        }

        internal sealed partial class Note : IEntity<int>
        {
            public int Id { get; set; }
        }

        internal sealed partial class Note
        {
        }

        public sealed class Tag : IEntity<string>, ISoftDeletable
        {
            public string Id { get; set; } = string.Empty;
            public bool IsDeleted { get; set; }
            public DateTimeOffset? DeletedAt { get; set; }
        }

        public abstract class Draft : IEntity<Guid>
        {
            public Guid Id { get; set; }
        }
        """;

    /// <summary>
    /// 运行生成器并把生成的代码编回编译
    /// </summary>
    /// <remarks>
    /// 返回编译错误、生成器诊断与生成的源文本；<paramref name="rootNamespace"/> 是项目的 <c>RootNamespace</c>
    /// </remarks>
    private static (ImmutableArray<Diagnostic> Compile, ImmutableArray<Diagnostic> Generator, string Generated) Run(string source, bool withEntityFrameworkCore, string rootNamespace = "Demo.App")
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var framework = new[] { typeof(IEntity<>).Assembly, typeof(IRepository<,>).Assembly, typeof(Primitives.Error).Assembly, typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly };
        var entityFramework = withEntityFrameworkCore ? new[] { typeof(ModelBuilder).Assembly, typeof(ModelBuilderExtensions).Assembly } : [];
        // EF Core 的公开签名牵涉不少 Microsoft.Extensions 包：把进程里已加载的程序集都给编译，缺引用不会误报成生成器的错
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(static a => a.Location);
        // 受信程序集列表含测试输出目录里的全部 dll（包括 EF Core），"未引用 EF Core"的场景要按文件名整体过滤
        var references = trustedAssemblies
            .Concat(framework.Concat(entityFramework).Select(static a => a.Location))
            .Concat(loaded)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => withEntityFrameworkCore || !Path.GetFileName(path).Contains("EntityFrameworkCore", StringComparison.Ordinal))
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Demo.App",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create([new EntityGenerator().AsSourceGenerator()], optionsProvider: new TestAnalyzerConfigOptionsProvider(rootNamespace));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);
        var generated = output.SyntaxTrees.FirstOrDefault(static t => t.FilePath.EndsWith("GeneratedEntities.g.cs", StringComparison.Ordinal))?.ToString() ?? string.Empty;
        return (output.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray(), generatorDiagnostics, generated);
    }

    [Fact]
    public void Generates_RepositoryRegistrations_AndFilters_ThatCompile()
    {
        var (compile, generator, generated) = Run(Source, withEntityFrameworkCore: false);

        Assert.Empty(generator);
        Assert.Empty(compile);
        Assert.Contains("namespace Demo.App", generated, StringComparison.Ordinal);
        Assert.Contains("public static class GeneratedEntities", generated, StringComparison.Ordinal);
        Assert.Contains("AddGeneratedRepositories(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)", generated, StringComparison.Ordinal);
        Assert.Contains("RepositoryServiceCollectionExtensions.AddRepository<global::Demo.App.Order, global::System.Guid>(services);", generated, StringComparison.Ordinal);
        Assert.Contains("RepositoryServiceCollectionExtensions.AddRepository<global::Demo.App.Note, int>(services);", generated, StringComparison.Ordinal);
        Assert.Contains("RepositoryServiceCollectionExtensions.AddRepository<global::Demo.App.Tag, string>(services);", generated, StringComparison.Ordinal);
        // 抽象类跳过；partial 类只登记一次；没有引用 EF Core 就没有模型登记
        Assert.DoesNotContain("Draft", generated, StringComparison.Ordinal);
        Assert.Equal(1, generated.Split("AddRepository<global::Demo.App.Note, int>").Length - 1);
        Assert.DoesNotContain("ApplyGeneratedEntities", generated, StringComparison.Ordinal);

        // 软删除 + 多租户：三个规约；只软删除：一个；无协议：不生成
        Assert.Contains("public static class OrderFilters", generated, StringComparison.Ordinal);
        Assert.Contains("NotDeleted { get; } = new global::XiHan.Framework.Domain.Specifications.Specification<global::Demo.App.Order>(static entity => !entity.IsDeleted);", generated, StringComparison.Ordinal);
        Assert.Contains("ForTenant(string tenantId) => new global::XiHan.Framework.Domain.Specifications.Specification<global::Demo.App.Order>(entity => entity.TenantId == tenantId);", generated, StringComparison.Ordinal);
        Assert.Contains("ActiveFor(string tenantId) => NotDeleted.And(ForTenant(tenantId));", generated, StringComparison.Ordinal);
        Assert.Contains("public static class TagFilters", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("TagFilters.ForTenant", generated, StringComparison.Ordinal);
        Assert.Contains("Specification<global::Demo.App.Tag> NotDeleted", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("NoteFilters", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Generates_EntityFrameworkCoreModelRegistration_WhenReferenced()
    {
        var (compile, generator, generated) = Run(Source, withEntityFrameworkCore: true);

        Assert.Empty(generator);
        Assert.Empty(compile);
        Assert.Contains("ApplyGeneratedEntities(this global::Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder, global::Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade database)", generated, StringComparison.Ordinal);
        Assert.Contains("modelBuilder.Entity<global::Demo.App.Order>();", generated, StringComparison.Ordinal);
        Assert.Contains("modelBuilder.Entity<global::Demo.App.Note>();", generated, StringComparison.Ordinal);
        Assert.Contains("return global::XiHan.Framework.Persistence.EntityFrameworkCore.ModelBuilderExtensions.ApplyPersistenceConventions(modelBuilder, database);", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_AmbiguousKey_NestedEntity_AndFilterConflict()
    {
        var (_, generator, generated) = Run("""
            using System;
            using XiHan.Framework.Domain.Entities;
            using XiHan.Framework.Domain.Events;
            namespace Demo.App;
            public sealed class Weird : IEntity<int>, IEntity<Guid> { int IEntity<int>.Id => 0; Guid IEntity<Guid>.Id => Guid.Empty; }
            public static class Outer { public sealed class Inner : IEntity<int> { public int Id { get; set; } } }
            public static class TagFilters { }
            public sealed class Tag : IEntity<string>, ISoftDeletable { public string Id { get; set; } = ""; public bool IsDeleted { get; set; } public DateTimeOffset? DeletedAt { get; set; } }
            """, withEntityFrameworkCore: false);

        Assert.Equal(["XHP001", "XHP002", "XHP003"], generator.Select(static d => d.Id).OrderBy(static id => id, StringComparer.Ordinal));
        Assert.Equal(DiagnosticSeverity.Warning, generator.Single(static d => d.Id == "XHP003").Severity);
        // 冲突的实体仍登记仓储，只是不生成过滤规约
        Assert.Contains("AddRepository<global::Demo.App.Tag, string>(services);", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("static class TagFilters", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Weird", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Inner", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Records_AreRegistered_AndValueTypeEntities_AreReported()
    {
        var (compile, generator, generated) = Run("""
            using System;
            using XiHan.Framework.Domain.Entities;
            namespace Demo.App;
            public sealed record Invoice(Guid Id, string Number) : IEntity<Guid>;
            public sealed record class Payment : IEntity<long>, ISoftDeletable
            {
                public long Id { get; init; }
                public bool IsDeleted { get; set; }
                public DateTimeOffset? DeletedAt { get; set; }
            }
            internal sealed partial record Receipt : IEntity<int> { public int Id { get; init; } }
            internal sealed partial record Receipt;
            public abstract record DraftRecord : IEntity<Guid> { public Guid Id { get; init; } }
            public readonly record struct Point(int Id) : IEntity<int>;
            public struct Cell : IEntity<int> { public int Id { get; set; } }
            """, withEntityFrameworkCore: true);

        Assert.Empty(compile);
        // record class 与带 record 修饰的类一样登记仓储、登记进 EF Core 模型、生成过滤规约；partial 只登记一次，抽象的跳过
        Assert.Contains("AddRepository<global::Demo.App.Invoice, global::System.Guid>(services);", generated, StringComparison.Ordinal);
        Assert.Contains("AddRepository<global::Demo.App.Payment, long>(services);", generated, StringComparison.Ordinal);
        Assert.Equal(1, generated.Split("AddRepository<global::Demo.App.Receipt, int>").Length - 1);
        Assert.Contains("modelBuilder.Entity<global::Demo.App.Invoice>();", generated, StringComparison.Ordinal);
        Assert.Contains("public static class PaymentFilters", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("DraftRecord", generated, StringComparison.Ordinal);
        // 值类型不能作仓储的实体：逐个报 XHP004 错误，不登记
        Assert.Equal(["Cell", "Point"], generator.Where(static d => d.Id == "XHP004").Select(static d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Split('\'')[1]).OrderBy(static name => name, StringComparer.Ordinal));
        Assert.All(generator, static d => Assert.Equal(("XHP004", DiagnosticSeverity.Error), (d.Id, d.Severity)));
        Assert.DoesNotContain("Point", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Cell", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void NoEntities_GeneratesNothing()
    {
        var (compile, generator, generated) = Run("namespace Demo.App; public sealed class Plain { }", withEntityFrameworkCore: false);
        Assert.Empty(generator);
        Assert.Empty(compile);
        Assert.Empty(generated);
    }

    [Fact]
    public void KeywordNamespaces_AreEscapedInGeneratedCode()
    {
        var (compile, generator, generated) = Run("""
            using System;
            using XiHan.Framework.Domain.Entities;

            namespace Demo.@event;

            public sealed class @class : IEntity<Guid>, ISoftDeletable
            {
                public Guid Id { get; init; }
                public bool IsDeleted { get; set; }
                public DateTimeOffset? DeletedAt { get; set; }
            }
            """, withEntityFrameworkCore: false, rootNamespace: "Demo.event");

        // 根命名空间、实体所在命名空间与类型名是 C# 关键字时加 @，登记类与过滤规约类都能编译
        Assert.Empty(generator);
        Assert.Empty(compile);
        Assert.Contains("namespace Demo.@event", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace Demo.event", generated, StringComparison.Ordinal);
        Assert.Contains("AddRepository<global::Demo.@event.@class, global::System.Guid>(services);", generated, StringComparison.Ordinal);
        Assert.Contains("public static class classFilters", generated, StringComparison.Ordinal);
    }
}
