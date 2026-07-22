// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Threading;

/// <summary>
/// 环境数据上下文
/// </summary>
public interface IAmbientDataContext
{
    /// <summary>
    /// 设置数据
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    void SetData(string key, object? value);

    /// <summary>
    /// 获取数据
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    object? GetData(string key);
}
