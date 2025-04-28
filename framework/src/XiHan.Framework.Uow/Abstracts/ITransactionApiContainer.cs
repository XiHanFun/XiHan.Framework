#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITransactionApiContainer
// Guid:55e9866d-12a1-4450-a827-f2ae1d967ecf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:01:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Uow.Abstracts;

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
    ITransactionApi? FindTransactionApi(string key);

    /// <summary>
    /// 添加事务API
    /// </summary>
    /// <param name="key"></param>
    /// <param name="api"></param>
    void AddTransactionApi(string key, ITransactionApi api);

    /// <summary>
    /// 获取或添加事务API
    /// </summary>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    ITransactionApi GetOrAddTransactionApi(string key, Func<ITransactionApi> factory);
}
