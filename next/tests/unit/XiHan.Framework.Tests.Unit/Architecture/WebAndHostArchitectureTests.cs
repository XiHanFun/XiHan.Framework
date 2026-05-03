using NetArchTest.Rules;
using Xunit;

namespace XiHan.Framework.Tests.Unit.Architecture;

/// <summary>
/// 提供 Web 与宿主层归属守护测试。
/// </summary>
public sealed class WebAndHostArchitectureTests
{
    /// <summary>
    /// Web 层不应反向依赖具体宿主。
    /// </summary>
    [Fact]
    public void Web_Should_Not_Depend_On_Host_Project()
    {
        var webAssembly = typeof(XiHan.Framework.Web.XiHanFrameworkWebMarker).Assembly;

        var result = Types.InAssembly(webAssembly)
            .ShouldNot()
            .HaveDependencyOn("XiHan.Framework.Hosts.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, "Web 层不应反向依赖具体宿主项目。");
    }

    /// <summary>
    /// 宿主层不应直接依赖具体 Provider 实现。
    /// </summary>
    [Fact]
    public void Host_Should_Not_Depend_On_Provider_Implementations_Directly()
    {
        var hostAssembly = typeof(XiHan.Framework.Hosts.Api.Program).Assembly;

        var result = Types.InAssembly(hostAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "XiHan.Framework.Data.EntityFrameworkCore",
                "XiHan.Framework.Data.SqlSugar",
                "XiHan.Framework.Logging.Console",
                "XiHan.Framework.Logging.Serilog")
            .GetResult();

        Assert.True(result.IsSuccessful, "宿主层不应直接依赖具体 Provider 实现，应通过框架装配层或模块接入。");
    }

    /// <summary>
    /// 宿主层应只依赖允许的框架装配层与通用层。
    /// </summary>
    [Fact]
    public void Host_Should_Be_Allowed_To_Depend_On_Framework_Composition_Layers()
    {
        var hostAssembly = typeof(XiHan.Framework.Hosts.Api.Program).Assembly;
        var referencedAssemblyNames = hostAssembly
            .GetReferencedAssemblies()
            .Select(static assemblyName => assemblyName.Name)
            .Where(static name => name is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("XiHan.Framework.Bootstrap.Abstractions", referencedAssemblyNames);
        Assert.Contains("XiHan.Framework.Bootstrap", referencedAssemblyNames);
        Assert.Contains("XiHan.Framework.Kernel", referencedAssemblyNames);
    }
}
