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
/// 分布式唯一标识生成器接口
/// </summary>
public interface IDistributedIdGenerator<TKey>
{
    /// <summary>
    /// 获取下一个唯一标识
    /// </summary>
    /// <returns>生成的唯一标识</returns>
    TKey NextId();

    /// <summary>
    /// 获取下一个唯一标识(字符串形式)
    /// </summary>
    /// <returns>生成的唯一标识字符串</returns>
    string NextIdString();

    /// <summary>
    /// 批量获取唯一标识
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>ID数组</returns>
    TKey[] NextIds(int count);

    /// <summary>
    /// 批量获取唯一标识(字符串形式)
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>ID字符串数组</returns>
    string[] NextIdStrings(int count);

    /// <summary>
    /// 异步获取下一个唯一标识
    /// </summary>
    /// <returns>生成的唯一标识</returns>
    Task<TKey> NextIdAsync();

    /// <summary>
    /// 异步获取下一个唯一标识(字符串形式)
    /// </summary>
    /// <returns>生成的唯一标识字符串</returns>
    Task<string> NextIdStringAsync();

    /// <summary>
    /// 异步批量获取唯一标识
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>ID数组</returns>
    Task<TKey[]> NextIdsAsync(int count);

    /// <summary>
    /// 异步批量获取唯一标识(字符串形式)
    /// </summary>
    /// <param name="count">需要获取的唯一标识数量</param>
    /// <returns>ID字符串数组</returns>
    Task<string[]> NextIdStringsAsync(int count);

    /// <summary>
    /// 从唯一标识中提取时间戳
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>时间戳</returns>
    DateTime ExtractTime(TKey id);

    /// <summary>
    /// 从唯一标识中提取工作机器唯一标识
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>工作机器唯一标识</returns>
    int ExtractWorkerId(TKey id);

    /// <summary>
    /// 从唯一标识中提取序列号
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>序列号</returns>
    int ExtractSequence(TKey id);

    /// <summary>
    /// 从唯一标识中提取数据中心唯一标识
    /// </summary>
    /// <param name="id">Id</param>
    /// <returns>数据中心唯一标识</returns>
    int ExtractDataCenterId(TKey id);

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
