#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WorkflowVariables
// Guid:0a58e3f7-c412-4d96-8b03-67f2d1a94ce5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:19:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Workflow.Abstractions.Runtime;

/// <summary>
/// 流程变量容器（包装实例变量字典，提供类型安全读写）
/// </summary>
public class WorkflowVariables
{
    private readonly Dictionary<string, object?> _variables;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="variables">底层变量字典（直接持有引用，写入即生效）</param>
    public WorkflowVariables(Dictionary<string, object?> variables)
    {
        _variables = variables;
    }

    /// <summary>
    /// 变量名集合
    /// </summary>
    public IReadOnlyCollection<string> Names => _variables.Keys;

    /// <summary>
    /// 底层变量字典（表达式求值等场景直接读取）
    /// </summary>
    public IReadOnlyDictionary<string, object?> AsReadOnly => _variables;

    /// <summary>
    /// 是否包含变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>包含返回 true</returns>
    public bool Contains(string name)
    {
        return _variables.ContainsKey(name);
    }

    /// <summary>
    /// 获取变量原始值（不存在返回 null）
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>归一化后的变量值</returns>
    public object? Get(string name)
    {
        return _variables.TryGetValue(name, out var value) ? WorkflowValueConverter.Normalize(value) : null;
    }

    /// <summary>
    /// 获取变量并转换为目标类型（不存在返回默认值）
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="name">变量名</param>
    /// <returns>转换后的变量值</returns>
    public T? Get<T>(string name)
    {
        return _variables.TryGetValue(name, out var value) ? WorkflowValueConverter.ConvertTo<T>(value) : default;
    }

    /// <summary>
    /// 尝试获取变量
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="name">变量名</param>
    /// <param name="value">转换后的变量值</param>
    /// <returns>变量存在且转换成功返回 true</returns>
    public bool TryGet<T>(string name, out T? value)
    {
        if (_variables.TryGetValue(name, out var raw))
        {
            value = WorkflowValueConverter.ConvertTo<T>(raw);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// 设置变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <param name="value">变量值</param>
    public void Set(string name, object? value)
    {
        _variables[name] = value;
    }

    /// <summary>
    /// 批量合并变量
    /// </summary>
    /// <param name="values">待合并的键值集合</param>
    public void Merge(IEnumerable<KeyValuePair<string, object?>> values)
    {
        foreach (var pair in values)
        {
            _variables[pair.Key] = pair.Value;
        }
    }

    /// <summary>
    /// 移除变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>存在并移除返回 true</returns>
    public bool Remove(string name)
    {
        return _variables.Remove(name);
    }
}
