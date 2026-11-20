// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 按数据库类型分开的 SqlSugar 反射信息缓存
/// </summary>
/// <remarks>
/// SqlSugar 在反射信息缓存里按实体类型与 <c>ConfigId</c> 取实体映射（数据读取器的物化器同样不分库），缺省的缓存进程内共用；<see cref="SqlSugarConventions"/> 的列约定却随数据库类型而变（SQL Server 的时间参数按 <c>datetime2</c> 传、字符串建成 <c>nvarchar</c> 等）。<c>ConfigId</c> 相同、数据库类型不同的连接（租户分片连接都是 <see cref="SqlSugarConnections.Shard"/>，同一进程里的几个宿主都用 <see cref="SqlSugarConnections.Default"/>）因此会用到先建好的那份映射。本类给每个键加上连接此刻的数据库类型再交给原来的缓存：同类型的连接照旧共用一份，不同类型的各建各的。数据库类型在每次取用时读取，应用可以在 <see cref="SqlSugarConventions.Apply"/> 之后才设置它；键仍存在原来的缓存里，SqlSugar 的 <c>RemoveCacheAll</c> 照样清得掉
/// </remarks>
/// <param name="inner">原来的缓存（缺省是 SqlSugar 进程内共用的那份）</param>
/// <param name="config">读取数据库类型的连接配置</param>
internal sealed class DbTypeScopedReflectionCache(ICacheService inner, ConnectionConfig config) : ICacheService
{
    /// <inheritdoc />
    public void Add<TValue>(string key, TValue value) => inner.Add(Scoped(key), value);

    /// <inheritdoc />
    public void Add<TValue>(string key, TValue value, int cacheDurationInSeconds) => inner.Add(Scoped(key), value, cacheDurationInSeconds);

    /// <inheritdoc />
    public bool ContainsKey<TValue>(string key) => inner.ContainsKey<TValue>(Scoped(key));

    /// <inheritdoc />
    public TValue Get<TValue>(string key) => inner.Get<TValue>(Scoped(key));

    /// <inheritdoc />
    public IEnumerable<string> GetAllKey<TValue>()
    {
        var prefix = Prefix();
        return inner.GetAllKey<TValue>()
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(key => key[prefix.Length..])
            .ToList();
    }

    /// <inheritdoc />
    public void Remove<TValue>(string key) => inner.Remove<TValue>(Scoped(key));

    /// <inheritdoc />
    public TValue GetOrCreate<TValue>(string cacheKey, Func<TValue> create, int cacheDurationInSeconds = int.MaxValue)
        => inner.GetOrCreate(Scoped(cacheKey), create, cacheDurationInSeconds);

    /// <summary>
    /// 本连接此刻的键前缀
    /// </summary>
    private string Prefix() => string.Concat("xihan.", config.DbType.ToString(), ":");

    /// <summary>
    /// 加上数据库类型的键
    /// </summary>
    private string Scoped(string key) => string.Concat(Prefix(), key);
}
