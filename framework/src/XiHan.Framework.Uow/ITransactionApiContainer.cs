#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITransactionApiContainer
// Guid:55e9866d-12a1-4450-a827-f2ae1d967ecf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:01:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Uow;

/// <summary>
/// 事务API容器
/// </summary>
public interface ITransactionApiContainer
{
    /// <summary>
    /// 查找事务API
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    ITransactionApi? FindTransactionApi([NotNull] string key);

    /// <summary>
    /// 添加事务API
    /// </summary>
    /// <param name="key"></param>
    /// <param name="api"></param>
    void AddTransactionApi([NotNull] string key, [NotNull] ITransactionApi api);

    /// <summary>
    /// 获取或添加事务API
    /// </summary>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    ITransactionApi GetOrAddTransactionApi([NotNull] string key, [NotNull] Func<ITransactionApi> factory);
}
