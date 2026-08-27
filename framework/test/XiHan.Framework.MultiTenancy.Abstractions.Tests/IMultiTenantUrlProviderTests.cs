// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Tests.Fakes;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 多租户 URL 提供程序契约的测试
/// </summary>
/// <remarks>
/// 抽象包只声明契约，这里用 <see cref="FakeMultiTenantUrlProvider"/> 按接口 XML 文档描述的语义落地一份纯内存实现：
/// 依据当前租户替换模板占位符，并对 null / 空白模板抛出文档声明的 <see cref="ArgumentNullException"/> 与 <see cref="ArgumentException"/>。
/// 全程不发起任何网络访问。
/// </remarks>
public class IMultiTenantUrlProviderTests
{
    private const string TemplateUrl = "https://{tenant}.example.com/api/users";

    /// <summary>
    /// 当前租户有名称时用名称替换占位符
    /// </summary>
    [Fact]
    public async Task GetUrlAsync_WithTenantName_ReplacesPlaceholderWithName()
    {
        var currentTenant = CreateCurrentTenant();
        IMultiTenantUrlProvider provider = new FakeMultiTenantUrlProvider(currentTenant);

        using (currentTenant.Change(1L, "acme"))
        {
            var url = await provider.GetUrlAsync(TemplateUrl);

            Assert.Equal("https://acme.example.com/api/users", url);
        }
    }

    /// <summary>
    /// 当前租户没有名称时退化为使用唯一标识
    /// </summary>
    [Fact]
    public async Task GetUrlAsync_WithoutTenantName_FallsBackToTenantId()
    {
        var currentTenant = CreateCurrentTenant();
        IMultiTenantUrlProvider provider = new FakeMultiTenantUrlProvider(currentTenant);

        using (currentTenant.Change(1024L))
        {
            var url = await provider.GetUrlAsync(TemplateUrl);

            Assert.Equal("https://1024.example.com/api/users", url);
        }
    }

    /// <summary>
    /// 无租户时退化为宿主段
    /// </summary>
    [Fact]
    public async Task GetUrlAsync_WithoutTenant_FallsBackToHostSegment()
    {
        var currentTenant = CreateCurrentTenant();
        IMultiTenantUrlProvider provider = new FakeMultiTenantUrlProvider(currentTenant);

        var url = await provider.GetUrlAsync(TemplateUrl);

        Assert.Equal("https://host.example.com/api/users", url);
    }

    /// <summary>
    /// 模板中没有占位符时原样返回
    /// </summary>
    [Fact]
    public async Task GetUrlAsync_WithoutPlaceholder_ReturnsTemplateUnchanged()
    {
        var currentTenant = CreateCurrentTenant();
        IMultiTenantUrlProvider provider = new FakeMultiTenantUrlProvider(currentTenant);

        using (currentTenant.Change(1L, "acme"))
        {
            var url = await provider.GetUrlAsync("https://example.com/api/users");

            Assert.Equal("https://example.com/api/users", url);
        }
    }

    /// <summary>
    /// 模板为 null 时按文档抛出参数为空异常
    /// </summary>
    [Fact]
    public async Task GetUrlAsync_WithNullTemplate_ThrowsArgumentNullException()
    {
        IMultiTenantUrlProvider provider = new FakeMultiTenantUrlProvider(CreateCurrentTenant());

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => provider.GetUrlAsync(null!));

        Assert.Equal("templateUrl", exception.ParamName);
    }

    /// <summary>
    /// 模板为空白时按文档抛出参数非法异常
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetUrlAsync_WithBlankTemplate_ThrowsArgumentException(string templateUrl)
    {
        IMultiTenantUrlProvider provider = new FakeMultiTenantUrlProvider(CreateCurrentTenant());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => provider.GetUrlAsync(templateUrl));

        Assert.Equal("templateUrl", exception.ParamName);
    }

    /// <summary>
    /// 契约要求方法返回 Task&lt;string&gt; 且只接受一个模板参数
    /// </summary>
    /// <remarks>
    /// 返回类型必须是泛型 Task，同步化会让所有需要远程查询租户域名的实现无路可走。
    /// </remarks>
    [Fact]
    public void Contract_GetUrlAsync_ReturnsTaskOfString()
    {
        var method = typeof(IMultiTenantUrlProvider).GetMethod(nameof(IMultiTenantUrlProvider.GetUrlAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string>), method.ReturnType);

        var parameters = method.GetParameters();
        var parameter = Assert.Single(parameters);
        Assert.Equal(typeof(string), parameter.ParameterType);
        Assert.False(parameter.IsOptional);
    }

    /// <summary>
    /// 创建基于手写访问器的当前租户实例
    /// </summary>
    /// <returns>当前租户</returns>
    private static ICurrentTenant CreateCurrentTenant()
    {
        return new FakeCurrentTenant(new FakeCurrentTenantAccessor());
    }
}
