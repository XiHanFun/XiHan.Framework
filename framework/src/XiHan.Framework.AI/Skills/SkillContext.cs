#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SkillContext
// Guid:b94d875f-92c2-49ee-b2c8-2cabc1d4b83c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/25 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Skills;

/// <summary>
/// 技能上下文
/// </summary>
public class SkillContext
{
    /// <summary>
    /// 会话唯一标识
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 关联的Agent Id
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// 上下文变量
    /// </summary>
    public Dictionary<string, object> Variables { get; } = [];

    /// <summary>
    /// 对话历史
    /// </summary>
    public List<(string Role, string Content)> ChatHistory { get; } = [];

    /// <summary>
    /// 添加上下文变量
    /// </summary>
    /// <param name="key">变量名</param>
    /// <param name="value">变量值</param>
    public void AddVariable(string key, object value)
    {
        Variables[key] = value;
    }

    /// <summary>
    /// 获取上下文变量
    /// </summary>
    /// <typeparam name="T">变量类型</typeparam>
    /// <param name="key">变量名</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>变量值</returns>
    public T GetVariable<T>(string key, T? defaultValue = default)
    {
        return Variables.TryGetValue(key, out var value) && value is T typedValue
            ? typedValue
            : defaultValue!;
    }

    /// <summary>
    /// 添加聊天消息
    /// </summary>
    /// <param name="role">角色</param>
    /// <param name="content">内容</param>
    public void AddChatMessage(string role, string content)
    {
        ChatHistory.Add((role, content));
    }

    /// <summary>
    /// 设置聊天历史
    /// </summary>
    /// <param name="history">聊天历史</param>
    public void SetChatHistory(IEnumerable<(string Role, string Content)> history)
    {
        ChatHistory.Clear();
        foreach (var item in history)
        {
            ChatHistory.Add(item);
        }
    }
}
