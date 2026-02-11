#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanAIMemoryService
// Guid:206fd881-16bc-4edc-bf2f-8cb8f1dd5753
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Memory;

/// <summary>
/// AI记忆服务接口
/// </summary>
public interface IXiHanAIMemoryService
{
    /// <summary>
    /// 添加记忆
    /// </summary>
    /// <param name="collection">集合名称</param>
    /// <param name="text">文本内容</param>
    /// <param name="metadata">元数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>记忆唯一标识</returns>
    Task<string> AddAsync(string collection, string text, IDictionary<string, object>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检索记忆
    /// </summary>
    /// <param name="collection">集合名称</param>
    /// <param name="query">查询文本</param>
    /// <param name="limit">返回数量限制</param>
    /// <param name="minRelevance">最小相关度</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>记忆结果集</returns>
    Task<IEnumerable<XiHanMemoryResult>> SearchAsync(string collection, string query, int limit = 5, double minRelevance = 0.7, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除记忆
    /// </summary>
    /// <param name="collection">集合名称</param>
    /// <param name="id">记忆唯一标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    Task<bool> DeleteAsync(string collection, string id, CancellationToken cancellationToken = default);
}
