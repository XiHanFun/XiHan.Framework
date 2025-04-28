#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDatabaseApiContainer
// Guid:8f7833d1-55da-4c59-acca-d996c519ade0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:59:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Uow.Abstracts;

/// <summary>
/// 数据库API容器接口
/// </summary>
public interface IDatabaseApiContainer : IServiceProviderAccessor
{
    /// <summary>
    /// 查找数据库API
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    IDatabaseApi? FindDatabaseApi(string key);

    /// <summary>
    /// 添加数据库API
    /// </summary>
    /// <param name="key"></param>
    /// <param name="api"></param>
    void AddDatabaseApi(string key, IDatabaseApi api);

    /// <summary>
    /// 获取或添加数据库API
    /// </summary>
    /// <param name="key"></param>
    /// <param name="factory"></param>
    /// <returns></returns>
    IDatabaseApi GetOrAddDatabaseApi(string key, Func<IDatabaseApi> factory);
}
