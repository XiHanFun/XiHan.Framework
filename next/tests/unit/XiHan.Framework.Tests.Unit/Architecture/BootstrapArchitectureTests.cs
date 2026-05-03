using NetArchTest.Rules;
using Xunit;
using XiHan.Framework.Bootstrap.Application;
using XiHan.Framework.Bootstrap.Abstractions.Application;

namespace XiHan.Framework.Tests.Unit.Architecture;

/// <summary>
/// 提供 Bootstrap 边界守护测试。
/// </summary>
public sealed class BootstrapArchitectureTests
{
    /// <summary>
    /// Bootstrap 抽象层不应依赖宿主或 ASP.NET Core。
    /// </summary>
    [Fact]
    public void BootstrapAbstractions_Should_Not_Depend_On_Hosts_Or_AspNetCore()
    {
        var result = Types.InAssembly(typeof(IXiHanApplication).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "XiHan.Framework.Hosts.Api",
                "XiHan.Framework.Web",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, "Bootstrap.Abstractions 不应依赖宿主、Web 或 ASP.NET Core。");
    }

    /// <summary>
    /// Bootstrap 实现层不应反向依赖宿主项目。
    /// </summary>
    [Fact]
    public void Bootstrap_Should_Not_Depend_On_Host_Project()
    {
        var result = Types.InAssembly(typeof(XiHanApplicationFactory).Assembly)
            .ShouldNot()
            .HaveDependencyOn("XiHan.Framework.Hosts.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, "Bootstrap 不应反向依赖具体宿主项目。");
    }

    /// <summary>
    /// Bootstrap 中不应再定义 Kernel 命名空间下的类型。
    /// </summary>
    [Fact]
    public void Bootstrap_Should_Not_Leak_Back_Into_Kernel_Namespace()
    {
        var invalidTypes = typeof(XiHanApplicationFactory).Assembly
            .GetTypes()
            .Where(static type =>
                type.Namespace is not null &&
                type.Namespace.StartsWith("XiHan.Framework.Kernel", StringComparison.Ordinal))
            .ToArray();

        Assert.True(invalidTypes.Length == 0, "Bootstrap 不应回流到 Kernel 命名空间。");
    }
}
