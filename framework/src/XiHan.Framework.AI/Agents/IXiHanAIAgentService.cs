#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanAIAgentService
// Guid:577e7780-5c43-40b9-8306-4d4e75fefb26
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AI.Options;
using XiHan.Framework.AI.Results;

namespace XiHan.Framework.AI.Agents;

/// <summary>
/// Agent服务接口
/// </summary>
public interface IXiHanAIAgentService
{
    /// <summary>
    /// 创建新Agent
    /// </summary>
    /// <param name="agentId">Agent唯一标识</param>
    /// <param name="options">Agent配置选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新创建的Agent</returns>
    Task<IXiHanAIAgent> CreateAgentAsync(string agentId, AgentOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取已存在Agent
    /// </summary>
    /// <param name="agentId">Agent唯一标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>存在的Agent或null</returns>
    Task<IXiHanAIAgent?> GetAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除Agent
    /// </summary>
    /// <param name="agentId">Agent唯一标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    Task<bool> RemoveAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向Agent发送消息
    /// </summary>
    /// <param name="agentId">Agent唯一标识</param>
    /// <param name="message">消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>聊天结果</returns>
    Task<XiHanChatResult> SendMessageAsync(string agentId, string message, CancellationToken cancellationToken = default);
}
