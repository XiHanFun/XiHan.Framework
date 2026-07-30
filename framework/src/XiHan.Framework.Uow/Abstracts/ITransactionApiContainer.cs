// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
