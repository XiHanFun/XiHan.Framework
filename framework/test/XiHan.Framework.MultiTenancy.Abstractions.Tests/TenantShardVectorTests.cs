// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 回放 <c>tenant-shards.json</c>：从租户属性推出分片
/// </summary>
/// <remarks>
/// <c>multitenancy/tenant-shards.json</c>（RFC-0007「分片」的默认解析器，ADR-0018、ADR-0277）。经 <see cref="TenantStoreShardResolver.FromProperties"/> 回放，三个属性名与 <see cref="TenantShardKeys"/> 的常量比较
/// </remarks>
public sealed class TenantShardVectorTests
{
    /// <summary>
    /// 用例里的租户
    /// </summary>
    private const string TenantId = "acme";

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ShardVectors _vectors = SpecVectors.Read<ShardVectors>("multitenancy", "tenant-shards.json");

    [Fact]
    public void PropertyNames_AreTheOnesInTheVectors()
    {
        Assert.Equal(TenantShardKeys.ConnectionString, _vectors.ConnectionStringProperty);
        Assert.Equal(TenantShardKeys.DatabaseProvider, _vectors.ProviderProperty);
        Assert.Equal(TenantShardKeys.PropertyPrefix, _vectors.PassThroughPrefix);
    }

    [Fact]
    public void FromProperties_DerivesTheShardOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var derived = TenantStoreShardResolver.FromProperties(new TenantInfo { Id = TenantId, Properties = vector.Properties });
            var actual = derived.TryGetValue(out var shard) ? Describe(shard.ConnectionString, shard.Provider, shard.Properties) : "no shard";
            var expected = vector.Shard is { } expectedShard ? Describe(expectedShard.ConnectionString, expectedShard.Provider, expectedShard.Properties) : "no shard";
            if (actual != expected)
            {
                failures.Add($"{vector.Name}: derived {actual}, the vector has {expected}");
            }

            // 分片的租户 ID 就是这个租户的 ID
            if (derived.TryGetValue(out var owned) && owned.TenantId != TenantId)
            {
                failures.Add($"{vector.Name}: the shard belongs to tenant '{owned.TenantId}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverSharedTenants_TheProviderCasing_AndThePassThroughPrefix()
    {
        // 防止向量换了写法后某条规则一条都没核对还照样通过：共库（连接串缺失与为空各一条）、提供者名改写成小写、透传属性去前缀、别的属性不透传
        Assert.Contains(_vectors.Cases, vector => vector.Shard is null && !vector.Properties.ContainsKey(_vectors.ConnectionStringProperty));
        Assert.Contains(_vectors.Cases, vector => vector.Shard is null && vector.Properties.TryGetValue(_vectors.ConnectionStringProperty, out var connectionString) && connectionString.Length == 0);
        Assert.Contains(_vectors.Cases, vector => vector.Shard is { Provider: { } provider } && vector.Properties[_vectors.ProviderProperty] != provider);
        Assert.Contains(_vectors.Cases, vector => vector.Shard is { Properties.Count: > 0 } && vector.Properties.Keys.Any(key => key.StartsWith(_vectors.PassThroughPrefix, StringComparison.Ordinal)));
        Assert.Contains(_vectors.Cases, vector => vector.Shard is { } shard && vector.Properties.Keys.Any(key => !key.StartsWith(_vectors.PassThroughPrefix, StringComparison.Ordinal) && key != _vectors.ConnectionStringProperty && key != _vectors.ProviderProperty && !shard.Properties.ContainsKey(key)));
    }

    /// <summary>
    /// 一个分片的描述：连接串、提供者名与按键排序的透传属性
    /// </summary>
    /// <param name="connectionString">连接串</param>
    /// <param name="provider">提供者名；<see langword="null"/> 表示没有</param>
    /// <param name="properties">透传的属性</param>
    private static string Describe(string connectionString, string? provider, IReadOnlyDictionary<string, string> properties)
        => $"connection string '{connectionString}', provider {(provider is null ? "none" : $"'{provider}'")}, properties [{string.Join(", ", properties.OrderBy(static property => property.Key, StringComparer.Ordinal).Select(static property => $"{property.Key}={property.Value}"))}]";

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="ConnectionStringProperty">承载分片连接串的属性名</param>
    /// <param name="ProviderProperty">承载提供者名的属性名</param>
    /// <param name="PassThroughPrefix">透传属性的键前缀</param>
    /// <param name="Cases">用例</param>
    private sealed record ShardVectors(string ConnectionStringProperty, string ProviderProperty, string PassThroughPrefix, ShardCase[] Cases);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">名字</param>
    /// <param name="Properties">租户的属性</param>
    /// <param name="Shard">推出的分片；<see langword="null"/> 表示共库</param>
    private sealed record ShardCase(string Name, Dictionary<string, string> Properties, ShardExpectation? Shard);

    /// <summary>
    /// 期望推出的分片
    /// </summary>
    /// <param name="ConnectionString">连接串</param>
    /// <param name="Provider">提供者名；<see langword="null"/> 表示没有</param>
    /// <param name="Properties">透传的属性</param>
    private sealed record ShardExpectation(string ConnectionString, string? Provider, Dictionary<string, string> Properties);
}
