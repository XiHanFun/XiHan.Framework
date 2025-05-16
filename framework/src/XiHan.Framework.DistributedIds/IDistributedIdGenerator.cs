#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDistributedIdGenerator
// Guid:fbe9aae0-e567-49db-8140-931f1aa617ac
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/28 19:33:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DistributedIds;

/// <summary>
/// 分布式ID生成器接口
/// </summary>
public interface IDistributedIdGenerator
{
    /// <summary>
    /// 获取下一个ID
    /// </summary>
    /// <returns>生成的ID</returns>
    long NextId();

    /// <summary>
    /// 获取下一个ID（字符串形式）
    /// </summary>
    /// <returns>生成的ID字符串</returns>
    string NextIdString();

    /// <summary>
    /// 从ID中提取时间戳
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>时间戳</returns>
    DateTime ExtractTime(long id);

    /// <summary>
    /// 从ID中提取工作机器ID
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>工作机器ID</returns>
    int ExtractWorkerId(long id);

    /// <summary>
    /// 从ID中提取序列号
    /// </summary>
    /// <param name="id">ID</param>
    /// <returns>序列号</returns>
    int ExtractSequence(long id);
}
