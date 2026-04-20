namespace XiHan.Framework.Security.Abstractions.CurrentUser;

/// <summary>
/// 表示当前用户上下文抽象。
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// 获取当前用户标识。
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// 获取当前用户名。
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// 获取当前用户是否已认证。
    /// </summary>
    bool IsAuthenticated { get; }
}
