// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Core.Logging;

/// <summary>
/// 初始化日志接口
/// </summary>
public interface IInitLogger<out T> : ILogger<T>
{
    /// <summary>
    /// 日志入口
    /// </summary>
    public List<XiHanInitLogEntry> Entries { get; }
}
