namespace XiHan.Framework.Data.Abstractions;

/// <summary>
/// 表示数据上下文访问器抽象。
/// </summary>
/// <typeparam name="TContext">上下文类型。</typeparam>
public interface IDbContextAccessor<out TContext>
{
    /// <summary>
    /// 获取当前上下文实例。
    /// </summary>
    TContext Context { get; }
}
