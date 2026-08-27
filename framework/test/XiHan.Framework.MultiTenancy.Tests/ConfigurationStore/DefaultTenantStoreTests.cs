// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Data;
using XiHan.Framework.MultiTenancy.ConfigurationStore;

namespace XiHan.Framework.MultiTenancy.Tests.ConfigurationStore;

/// <summary>
/// 基于配置的默认租户存储的测试
/// </summary>
/// <remarks>
/// 这个存储做了三件容易被忽略的事，用例逐条覆盖：
/// 一是每次查询都从选项监视器重新取快照，配置热更新后不会读到陈旧列表；
/// 二是对外返回的永远是克隆体，调用方改了返回对象也污染不到配置里的原始实例；
/// 三是克隆过程中兜底推导名称与规范化名称（名称为空取唯一标识、规范化名称为空取名称大写），
/// 这份兜底口径决定了按名称查租户能不能命中，改动会直接影响线上路由。
/// 选项监视器不手写替身，直接用真实的选项管道构造，顺带验证它能被正常装配。
/// </remarks>
public class DefaultTenantStoreTests
{
    /// <summary>
    /// 实现了租户存储契约
    /// </summary>
    [Fact]
    public void Type_ImplementsTenantStore()
    {
        var (store, _) = CreateStore();

        Assert.IsAssignableFrom<ITenantStore>(store);
    }

    /// <summary>
    /// 按唯一标识能查到对应租户
    /// </summary>
    [Fact]
    public async Task FindAsync_ById_ReturnsMatchingTenant()
    {
        var (store, _) = CreateStore(
            new TenantConfiguration(1L, "acme", "ACME"),
            new TenantConfiguration(2L, "globex", "GLOBEX"));

        var tenant = await store.FindAsync(2L, TestContext.Current.CancellationToken);

        Assert.NotNull(tenant);
        Assert.Equal(2L, tenant.Id);
        Assert.Equal("globex", tenant.Name);
        Assert.Equal("GLOBEX", tenant.NormalizedName);
    }

    /// <summary>
    /// 按唯一标识查不到时返回 null 而不是抛异常
    /// </summary>
    [Fact]
    public async Task FindAsync_ById_WhenMissing_ReturnsNull()
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "acme", "ACME"));

        Assert.Null(await store.FindAsync(999L, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 未配置任何租户时按唯一标识查返回 null
    /// </summary>
    [Fact]
    public async Task FindAsync_ById_WhenNoTenantConfigured_ReturnsNull()
    {
        var (store, _) = CreateStore();

        Assert.Null(await store.FindAsync(1L, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 租户集合为 null 时按唯一标识查返回 null
    /// </summary>
    /// <remarks>
    /// 配置绑定在极端情况下可能把集合整体置空，存储必须能空转而不是空引用。
    /// </remarks>
    [Fact]
    public async Task FindAsync_ById_WhenTenantArrayNull_ReturnsNull()
    {
        var (store, options) = CreateStore(new TenantConfiguration(1L, "acme", "ACME"));
        options.Tenants = null!;

        Assert.Null(await store.FindAsync(1L, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 按名称查忽略大小写
    /// </summary>
    [Theory]
    [InlineData("acme")]
    [InlineData("ACME")]
    [InlineData("AcMe")]
    public async Task FindAsync_ByName_IgnoresCase(string name)
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "acme", "ACME_NORMALIZED"));

        var tenant = await store.FindAsync(name, TestContext.Current.CancellationToken);

        Assert.NotNull(tenant);
        Assert.Equal(1L, tenant.Id);
    }

    /// <summary>
    /// 规范化名称同样可以作为查询键
    /// </summary>
    [Fact]
    public async Task FindAsync_ByNormalizedName_ReturnsMatchingTenant()
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "曦寒租户", "XIHAN"));

        var tenant = await store.FindAsync("xihan", TestContext.Current.CancellationToken);

        Assert.NotNull(tenant);
        Assert.Equal(1L, tenant.Id);
    }

    /// <summary>
    /// 按名称查会先裁掉首尾空白
    /// </summary>
    [Fact]
    public async Task FindAsync_ByName_TrimsInput()
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "acme", "ACME"));

        var tenant = await store.FindAsync("  acme  ", TestContext.Current.CancellationToken);

        Assert.NotNull(tenant);
        Assert.Equal(1L, tenant.Id);
    }

    /// <summary>
    /// 传入纯数字字符串时按唯一标识查
    /// </summary>
    /// <remarks>
    /// 请求头里的租户标识拿到的永远是字符串，这条转发让「Header 里写唯一标识」和「Header 里写名称」共用一个入口。
    /// </remarks>
    [Fact]
    public async Task FindAsync_ByNumericString_RoutesToIdLookup()
    {
        var (store, _) = CreateStore(new TenantConfiguration(7L, "acme", "ACME"));

        var tenant = await store.FindAsync("7", TestContext.Current.CancellationToken);

        Assert.NotNull(tenant);
        Assert.Equal(7L, tenant.Id);
        Assert.Equal("acme", tenant.Name);
    }

    /// <summary>
    /// 数字字符串超出唯一标识范围时退回按名称匹配
    /// </summary>
    [Fact]
    public async Task FindAsync_ByOverflowingNumericString_FallsBackToNameMatch()
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "99999999999999999999", "OVERFLOW"));

        var tenant = await store.FindAsync("99999999999999999999", TestContext.Current.CancellationToken);

        Assert.NotNull(tenant);
        Assert.Equal(1L, tenant.Id);
    }

    /// <summary>
    /// 名称为空或空白时直接返回 null，不做任何匹配
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FindAsync_ByBlankName_ReturnsNull(string name)
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "acme", "ACME"));

        Assert.Null(await store.FindAsync(name, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 名称为 null 时同样返回 null
    /// </summary>
    [Fact]
    public async Task FindAsync_ByNullName_ReturnsNull()
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "acme", "ACME"));
        string? name = null;

        Assert.Null(await store.FindAsync(name!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 名称不存在时返回 null
    /// </summary>
    [Fact]
    public async Task FindAsync_ByUnknownName_ReturnsNull()
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "acme", "ACME"));

        Assert.Null(await store.FindAsync("unknown", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 名称为空白的租户克隆时用唯一标识兜底
    /// </summary>
    /// <remarks>
    /// 只配了 Id 的租户在配置里很常见，兜底失效会让这类租户既查不到名字也无法按名称路由。
    /// </remarks>
    [Fact]
    public async Task FindAsync_WhenTenantNameBlank_ClonesWithIdAsName()
    {
        var (store, _) = CreateStore(new TenantConfiguration { Id = 5L });

        var tenant = await store.FindAsync(5L, TestContext.Current.CancellationToken);

        Assert.NotNull(tenant);
        Assert.Equal("5", tenant.Name);
        Assert.Equal("5", tenant.NormalizedName);
    }

    /// <summary>
    /// 规范化名称为空白时取名称的大写形式
    /// </summary>
    [Fact]
    public async Task FindAsync_WhenNormalizedNameBlank_ClonesWithUpperCasedName()
    {
        var (store, _) = CreateStore(new TenantConfiguration { Id = 6L, Name = "acme" });

        var tenant = await store.FindAsync(6L, TestContext.Current.CancellationToken);

        Assert.NotNull(tenant);
        Assert.Equal("acme", tenant.Name);
        Assert.Equal("ACME", tenant.NormalizedName);
    }

    /// <summary>
    /// 克隆时裁掉名称与规范化名称的首尾空白
    /// </summary>
    [Fact]
    public async Task FindAsync_TrimsClonedNames()
    {
        var (store, _) = CreateStore(new TenantConfiguration { Id = 8L, Name = "  acme  ", NormalizedName = "  ACME  " });

        var tenant = await store.FindAsync(8L, TestContext.Current.CancellationToken);

        Assert.NotNull(tenant);
        Assert.Equal("acme", tenant.Name);
        Assert.Equal("ACME", tenant.NormalizedName);
    }

    /// <summary>
    /// 激活状态与版本唯一标识原样带到克隆体上
    /// </summary>
    [Fact]
    public async Task FindAsync_CarriesActiveFlagAndEditionId()
    {
        var editionId = Guid.NewGuid();
        var (store, _) = CreateStore(new TenantConfiguration(1L, "acme", "ACME", editionId) { IsActive = false });

        var tenant = await store.FindAsync(1L, TestContext.Current.CancellationToken);

        Assert.NotNull(tenant);
        Assert.False(tenant.IsActive);
        Assert.Equal<Guid?>(editionId, tenant.EditionId);
    }

    /// <summary>
    /// 每次查询返回互不相同的克隆体，改动不会回写到配置
    /// </summary>
    /// <remarks>
    /// 这是这个存储最重要的隔离契约：租户配置是单例选项里的共享对象，
    /// 一旦把原始实例直接交出去，某个调用方随手改个名字就会污染所有后续请求。
    /// </remarks>
    [Fact]
    public async Task FindAsync_ReturnsIndependentClone()
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "acme", "ACME"));

        var first = await store.FindAsync(1L, TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        first.Name = "被调用方改坏了";
        first.IsActive = false;

        var second = await store.FindAsync(1L, TestContext.Current.CancellationToken);

        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Equal("acme", second.Name);
        Assert.True(second.IsActive);
    }

    /// <summary>
    /// 连接字符串集合也被克隆成新的实例
    /// </summary>
    [Fact]
    public async Task FindAsync_ClonesConnectionStringsIntoNewInstance()
    {
        var original = new TenantConfiguration(1L, "acme", "ACME")
        {
            ConnectionStrings = new ConnectionStrings
            {
                Default = "Server=.;Database=Acme",
                ["Report"] = "Server=.;Database=AcmeReport"
            }
        };
        var (store, _) = CreateStore(original);

        var tenant = await store.FindAsync(1L, TestContext.Current.CancellationToken);

        Assert.NotNull(tenant);
        Assert.NotNull(tenant.ConnectionStrings);
        Assert.NotSame(original.ConnectionStrings, tenant.ConnectionStrings);
        Assert.Equal(2, tenant.ConnectionStrings.Count);
        Assert.Equal("Server=.;Database=Acme", tenant.ConnectionStrings.Default);
        Assert.Equal("Server=.;Database=AcmeReport", tenant.ConnectionStrings["Report"]);
    }

    /// <summary>
    /// 连接字符串为空集合时克隆成 null
    /// </summary>
    /// <remarks>
    /// 空集合与 null 在这里被统一成 null，调用方只需判空一次；
    /// 若把空集合原样带出去，「有没有配置独立库」的判断就得同时判 null 和 Count。
    /// </remarks>
    [Fact]
    public async Task FindAsync_WhenConnectionStringsEmpty_ClonesToNull()
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "acme", "ACME"));

        var tenant = await store.FindAsync(1L, TestContext.Current.CancellationToken);

        Assert.NotNull(tenant);
        Assert.Null(tenant.ConnectionStrings);
    }

    /// <summary>
    /// 默认返回全部租户，包含未激活的
    /// </summary>
    [Fact]
    public async Task GetListAsync_ByDefault_IncludesInactiveTenants()
    {
        var (store, _) = CreateStore(
            new TenantConfiguration(1L, "acme", "ACME"),
            new TenantConfiguration(2L, "globex", "GLOBEX") { IsActive = false });

        var tenants = await store.GetListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, tenants.Count);
    }

    /// <summary>
    /// 显式排除未激活租户时只返回激活的
    /// </summary>
    [Fact]
    public async Task GetListAsync_ExcludingInactive_ReturnsOnlyActiveTenants()
    {
        var (store, _) = CreateStore(
            new TenantConfiguration(1L, "acme", "ACME"),
            new TenantConfiguration(2L, "globex", "GLOBEX") { IsActive = false });

        var tenants = await store.GetListAsync(false, TestContext.Current.CancellationToken);

        var tenant = Assert.Single(tenants);
        Assert.Equal(1L, tenant.Id);
    }

    /// <summary>
    /// 没有租户时返回空列表而不是 null
    /// </summary>
    [Fact]
    public async Task GetListAsync_WhenNoTenantConfigured_ReturnsEmptyList()
    {
        var (store, _) = CreateStore();

        var tenants = await store.GetListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(tenants);
        Assert.Empty(tenants);
    }

    /// <summary>
    /// 列表里的元素同样是克隆体
    /// </summary>
    [Fact]
    public async Task GetListAsync_ReturnsClones()
    {
        var original = new TenantConfiguration(1L, "acme", "ACME");
        var (store, _) = CreateStore(original);

        var tenants = await store.GetListAsync(cancellationToken: TestContext.Current.CancellationToken);

        var tenant = Assert.Single(tenants);
        Assert.NotSame(original, tenant);
        Assert.Equal(original.Id, tenant.Id);
    }

    /// <summary>
    /// 每次查询都重新读取选项快照
    /// </summary>
    /// <remarks>
    /// 存储持有的是选项监视器而不是选项值，配置热更新后必须立刻生效；
    /// 若在构造函数里把列表缓存下来，新增租户要重启进程才认，这里用改写选项值来反证没有缓存。
    /// </remarks>
    [Fact]
    public async Task FindAsync_ReadsLatestOptionsSnapshotOnEveryCall()
    {
        var (store, options) = CreateStore(new TenantConfiguration(1L, "acme", "ACME"));

        Assert.NotNull(await store.FindAsync(1L, TestContext.Current.CancellationToken));

        options.Tenants = [new TenantConfiguration(2L, "globex", "GLOBEX")];

        Assert.Null(await store.FindAsync(1L, TestContext.Current.CancellationToken));
        Assert.NotNull(await store.FindAsync(2L, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 传入已取消的令牌时按唯一标识查直接抛取消异常
    /// </summary>
    [Fact]
    public async Task FindAsync_ById_WhenTokenAlreadyCancelled_Throws()
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "acme", "ACME"));
        var cancelled = new CancellationToken(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await store.FindAsync(1L, cancelled));
    }

    /// <summary>
    /// 传入已取消的令牌时按名称查直接抛取消异常
    /// </summary>
    [Fact]
    public async Task FindAsync_ByName_WhenTokenAlreadyCancelled_Throws()
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "acme", "ACME"));
        var cancelled = new CancellationToken(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await store.FindAsync("acme", cancelled));
    }

    /// <summary>
    /// 传入已取消的令牌时取列表直接抛取消异常
    /// </summary>
    [Fact]
    public async Task GetListAsync_WhenTokenAlreadyCancelled_Throws()
    {
        var (store, _) = CreateStore(new TenantConfiguration(1L, "acme", "ACME"));
        var cancelled = new CancellationToken(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await store.GetListAsync(true, cancelled));
    }

    /// <summary>
    /// 用真实选项管道构造默认租户存储
    /// </summary>
    /// <param name="tenants">预置的租户配置</param>
    /// <returns>租户存储与其背后的选项实例（可直接改写以模拟配置热更新）</returns>
    private static (DefaultTenantStore Store, XiHanDefaultTenantStoreOptions Options) CreateStore(params TenantConfiguration[] tenants)
    {
        var services = new ServiceCollection();
        services.Configure<XiHanDefaultTenantStoreOptions>(options => options.Tenants = tenants);

        var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<XiHanDefaultTenantStoreOptions>>();

        return (new DefaultTenantStore(monitor), monitor.CurrentValue);
    }
}
