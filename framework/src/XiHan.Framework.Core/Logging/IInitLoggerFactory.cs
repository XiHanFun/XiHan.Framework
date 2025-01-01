#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IInitLoggerFactory
// Guid:67c97591-252c-4b74-82cc-36c15c9c7b84
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:16:53
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Logging;

/// <summary>
/// 初始化日志工厂接口
/// </summary>
public interface IInitLoggerFactory
{
    /// <summary>
    /// 创建初始化日志
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    IInitLogger<T> Create<T>();
}
