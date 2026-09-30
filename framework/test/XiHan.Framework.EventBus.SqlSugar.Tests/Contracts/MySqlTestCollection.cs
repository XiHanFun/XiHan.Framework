// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.SqlSugar.Tests.Contracts;

/// <summary>
/// 访问同一个 MySQL 库的测试类串行执行
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MySqlTestCollection
{
    /// <summary>
    /// 集合名
    /// </summary>
    public const string Name = "XiHan.EventBus.SqlSugar.MySql";
}
