#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TenantWriteGuard
// Guid:3f8c2a1e-7d94-4b6f-a2c8-9e5d1f0b7a34
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Repositories;

/// <summary>
/// 写路径租户边界守卫的显式逃逸作用域
/// </summary>
/// <remarks>
/// <para>
/// 仓储写路径在租户上下文内默认拒绝改写非本租户行（含 <c>TenantId=0</c> 的平台全局行）——
/// 这是「读共享 ≠ 写共享」的安全边界。但存在一类<b>用户主体数据</b>（用户账号、安全信息、
/// 个人设置、站内信收件行等）：行的归属键是 UserId，<c>TenantId</c> 只是「注册地」元数据，
/// 平台归属用户（TenantId=0）切换进租户后写自己的这些行是完全合法的。
/// </para>
/// <para>
/// 此类写入用 <see cref="Suppress"/> 显式包裹：
/// <code>
/// using (TenantWriteGuard.Suppress())
/// {
///     await _userRepository.UpdateAsync(user, cancellationToken);
/// }
/// </code>
/// 设计取舍：
/// <list type="bullet">
///   <item>按调用点显式声明（而非按实体类型静默豁免）——每处逃逸都可 grep 审计，
///         且不会让「租户管理员经读共享改写平台用户账号」这类管理面写入绕过守卫；</item>
///   <item>调用方必须自行保证被包裹的写只作用于当前用户自有行（UserId == 当前用户），
///         这是本逃逸口的使用契约；</item>
///   <item>不同于 <c>ICurrentTenant.Change(null)</c>：不改变租户上下文与连接解析（库隔离部署下
///         Change(null) 会把写重定向到平台库），只豁免写边界校验。</item>
/// </list>
/// AsyncLocal 语义：仅当前异步执行流生效，作用域结束自动还原，并行流互不影响。
/// </para>
/// </remarks>
public static class TenantWriteGuard
{
    private static readonly AsyncLocal<bool> SuppressedFlag = new();

    /// <summary>
    /// 当前执行流是否处于守卫豁免作用域内
    /// </summary>
    public static bool IsSuppressed => SuppressedFlag.Value;

    /// <summary>
    /// 开启守卫豁免作用域（用户主体数据的自有行写入专用，见类型注释的使用契约）
    /// </summary>
    /// <returns>作用域句柄，Dispose 时还原</returns>
    public static IDisposable Suppress()
    {
        var previous = SuppressedFlag.Value;
        SuppressedFlag.Value = true;
        return new SuppressScope(previous);
    }

    private sealed class SuppressScope(bool previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            SuppressedFlag.Value = previous;
        }
    }
}
