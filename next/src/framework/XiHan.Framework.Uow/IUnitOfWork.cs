namespace XiHan.Framework.Uow;

/// <summary>
/// 表示工作单元抽象。
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// 提交当前工作单元。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步提交操作的任务。</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);
}
