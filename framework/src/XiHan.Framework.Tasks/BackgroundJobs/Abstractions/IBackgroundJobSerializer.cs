// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

/// <summary>
/// 后台作业参数序列化器
/// </summary>
public interface IBackgroundJobSerializer
{
    /// <summary>
    /// 序列化
    /// </summary>
    /// <param name="obj">对象</param>
    /// <returns>序列化字符串</returns>
    string Serialize(object obj);

    /// <summary>
    /// 反序列化
    /// </summary>
    /// <param name="value">序列化字符串</param>
    /// <param name="type">目标类型</param>
    /// <returns>对象</returns>
    object Deserialize(string value, Type type);
}
