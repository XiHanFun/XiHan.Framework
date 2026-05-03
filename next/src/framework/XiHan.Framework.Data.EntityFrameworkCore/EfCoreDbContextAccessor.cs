using Microsoft.EntityFrameworkCore;
using XiHan.Framework.Data.Abstractions;

namespace XiHan.Framework.Data.EntityFrameworkCore;

/// <summary>
/// 表示 `EF Core` 数据上下文访问器。
/// </summary>
/// <typeparam name="TDbContext">数据库上下文类型。</typeparam>
public sealed class EfCoreDbContextAccessor<TDbContext> : IDbContextAccessor<TDbContext>
    where TDbContext : DbContext
{
    /// <summary>
    /// 初始化访问器实例。
    /// </summary>
    /// <param name="context">数据库上下文实例。</param>
    public EfCoreDbContextAccessor(TDbContext context)
    {
        Context = context;
    }

    /// <inheritdoc />
    public TDbContext Context { get; }
}
