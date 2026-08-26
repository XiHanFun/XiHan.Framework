// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 初始化对象的选取模式
/// </summary>
public enum DbInitializationMode
{
    /// <summary>
    /// 全量：扫描到的实体/种子全部参与，显式标注 <c>Enabled = false</c> 的除外
    /// </summary>
    All = 0,

    /// <summary>
    /// 按需：仅显式标注了 <see cref="TableInitializationAttribute"/> / <see cref="DataSeedingAttribute"/> 的参与
    /// </summary>
    OptIn = 1
}
