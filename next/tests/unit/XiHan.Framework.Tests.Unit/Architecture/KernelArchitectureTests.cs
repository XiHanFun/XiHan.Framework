using NetArchTest.Rules;
using Xunit;
using XiHan.Framework.Kernel.Modularity;

namespace XiHan.Framework.Tests.Unit.Architecture;

/// <summary>
/// 提供 Kernel 边界守护测试。
/// </summary>
public sealed class KernelArchitectureTests
{
    /// <summary>
    /// Kernel 不应再包含引导层命名空间。
    /// </summary>
    [Fact]
    public void Kernel_Should_Not_Contain_Bootstrap_Or_Application_Namespaces()
    {
        var invalidTypes = typeof(IXiHanModule).Assembly
            .GetTypes()
            .Where(static type =>
                type.Namespace is not null &&
                (type.Namespace.StartsWith("XiHan.Framework.Kernel.Application", StringComparison.Ordinal) ||
                 type.Namespace.StartsWith("XiHan.Framework.Kernel.Reflections", StringComparison.Ordinal)))
            .ToArray();

        Assert.True(invalidTypes.Length == 0, "Kernel 中不应再出现 Application 或 Reflections 命名空间。");
    }

    /// <summary>
    /// Kernel 不应依赖宿主或 Web 层实现。
    /// </summary>
    [Fact]
    public void Kernel_Should_Not_Have_Dependency_On_Hosts_Or_Web()
    {
        var result = Types.InAssembly(typeof(IXiHanModule).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "XiHan.Framework.Hosts.Api",
                "XiHan.Framework.Web",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, "Kernel 不应依赖宿主项目、Web 项目或 ASP.NET Core。");
    }
}
