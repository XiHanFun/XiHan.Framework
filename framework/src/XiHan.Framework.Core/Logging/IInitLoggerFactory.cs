// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
