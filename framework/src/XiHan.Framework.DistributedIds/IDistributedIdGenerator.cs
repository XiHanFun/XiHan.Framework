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
    /// 获取下一个Id
    /// </summary>
    /// <returns>生成的Id</returns>
    long NextId();

    /// <summary>
    /// 获取下一个Id(字符串形式)
    /// </summary>
    /// <returns>生成的ID字符串</returns>
    string NextIdString();

    /// <summary>
    /// 批量获取Id
    /// </summary>
    /// <param name="count">需要获取的ID数量</param>
    /// <returns>ID数组</returns>
    long[] NextIds(int count);

    /// <summary>
    /// 批量获取Id(字符串形式)
    /// </summary>
    /// <param name="count">需要获取的ID数量</param>
    /// <returns>ID字符串数组</returns>
    string[] NextIdStrings(int count);

    /// <summary>
    /// 异步获取下一个Id
    /// </summary>
    /// <returns>生成的Id</returns>
    Task<long> NextIdAsync();

    /// <summary>
    /// 异步获取下一个Id(字符串形式)
    /// </summary>
    /// <returns>生成的ID字符串</returns>
    Task<string> NextIdStringAsync();

    /// <summary>
    /// 异步批量获取Id
    /// </summary>
    /// <param name="count">需要获取的ID数量</param>
    /// <returns>ID数组</returns>
    Task<long[]> NextIdsAsync(int count);

    /// <summary>
    /// 异步批量获取Id(字符串形式)
    /// </summary>
    /// <param name="count">需要获取的ID数量</param>
    /// <returns>ID字符串数组</returns>
    Task<string[]> NextIdStringsAsync(int count);

    /// <summary>
    /// 从ID中提取时间戳
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>时间戳</returns>
    DateTime ExtractTime(long id);

    /// <summary>
    /// 从ID中提取工作机器Id
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>工作机器Id</returns>
    int ExtractWorkerId(long id);

    /// <summary>
    /// 从ID中提取序列号
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>序列号</returns>
    int ExtractSequence(long id);

    /// <summary>
    /// 从ID中提取数据中心Id
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>数据中心Id</returns>
    int ExtractDataCenterId(long id);

    /// <summary>
    /// 获取生成器类型
    /// </summary>
    /// <returns>生成器类型</returns>
    string GetGeneratorType();

    /// <summary>
    /// 获取生成器状态信息
    /// </summary>
    /// <returns>状态信息字典</returns>
    Dictionary<string, object> GetStats();
}
