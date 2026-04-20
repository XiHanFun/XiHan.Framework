using XiHan.Framework.Data.Abstractions;

namespace XiHan.Framework.Data.SqlSugar;

/// <summary>
/// 表示 `SqlSugar` 上下文访问器。
/// </summary>
public sealed class SqlSugarContextAccessor : IDbContextAccessor<object>
{
    /// <summary>
    /// 初始化访问器实例。
    /// </summary>
    /// <param name="context">上下文实例。</param>
    public SqlSugarContextAccessor(object context)
    {
        Context = context;
    }

    /// <inheritdoc />
    public object Context { get; }
}
