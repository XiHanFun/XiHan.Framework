// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 回放 <c>tenant-ids.json</c>：租户 ID 的文法与长度上限
/// </summary>
/// <remarks>
/// <c>multitenancy/tenant-ids.json</c>（RFC-0007「租户 ID」，ADR-0196、ADR-0277）。经 <see cref="TenantIdentifier"/> 回放，再核对用同一条文法的三处入口：租户信息的 ID、默认租户与分片的租户 ID
/// </remarks>
public sealed class TenantIdVectorTests
{
    /// <summary>
    /// 建分片用的连接串
    /// </summary>
    private const string ConnectionString = "Host=db-7.internal;Database=acme";

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly TenantIdVectors _vectors = ConformanceVectors.Load<TenantIdVectors>("tenant-ids.json");

    [Fact]
    public void MaxLength_IsTheOneInTheVectors()
        => Assert.Equal(TenantIdentifier.MaxLength, _vectors.MaxLength);

    [Fact]
    public void IsValid_AcceptsTheValidIds()
    {
        Assert.NotEmpty(_vectors.Valid);
        foreach (var id in _vectors.Valid)
        {
            Assert.True(TenantIdentifier.IsValid(id), id);
            Assert.Equal(id, TenantIdentifier.Validate(id));
            Assert.Equal(id, new TenantInfo { Id = id }.Id);
            Assert.Equal(id, new MultiTenancyOptions { DefaultTenant = id }.DefaultTenant);
            Assert.Equal(id, new TenantShard(id, ConnectionString).TenantId);
        }
    }

    [Fact]
    public void IsValid_RejectsTheInvalidIds()
    {
        Assert.NotEmpty(_vectors.Invalid);
        foreach (var id in _vectors.Invalid)
        {
            Assert.False(TenantIdentifier.IsValid(id), id);
            Assert.Throws<ArgumentException>(() => TenantIdentifier.Validate(id));
            Assert.Throws<ArgumentException>(() => new TenantInfo { Id = id });
            Assert.Throws<ArgumentException>(() => new MultiTenancyOptions { DefaultTenant = id });
            Assert.Throws<ArgumentException>(() => new TenantShard(id, ConnectionString));
        }
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimit_AndTheUppercaseIds()
    {
        // 防止向量换了写法后上限一条都没核对还照样通过：正好到上限的要收，只因超长而不合法的要拒
        Assert.Contains(_vectors.Valid, id => id.Length == _vectors.MaxLength);
        Assert.Contains(_vectors.Invalid, id => id.Length == _vectors.MaxLength + 1 && id.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9')));

        // 只用小写：只因含大写而不合法的也要有（ADR-0196）
        Assert.Contains(_vectors.Invalid, static id => id.Any(char.IsAsciiLetterUpper) && TenantIdentifier.IsValid(id.ToLowerInvariant()));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="MaxLength">租户 ID 的字符数上限</param>
    /// <param name="Valid">合文法的租户 ID</param>
    /// <param name="Invalid">不合文法的租户 ID</param>
    private sealed record TenantIdVectors(int MaxLength, string[] Valid, string[] Invalid);
}
