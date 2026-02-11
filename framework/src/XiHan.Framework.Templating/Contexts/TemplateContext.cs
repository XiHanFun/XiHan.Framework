#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateContext
// Guid:55efcb58-b6d4-411b-9aff-d043301f6b8b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 03:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Templating.Contexts;

/// <summary>
/// 模板上下文实现
/// </summary>
public class TemplateContext : ITemplateContext
{
    private readonly Stack<ITemplateScope> _scopeStack = new();
    private readonly ConcurrentDictionary<string, object?> _globalVariables = new();
    private readonly ConcurrentDictionary<string, Delegate> _functions = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public TemplateContext()
    {
        _scopeStack.Push(new TemplateScope(Guid.NewGuid().ToString(), null));
    }

    /// <summary>
    /// 获取变量值
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>变量值</returns>
    public object? GetVariable(string name)
    {
        // 从当前作用域开始向上查找
        foreach (var scope in _scopeStack)
        {
            if (scope.Variables.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        // 在全局变量中查找
        return _globalVariables.TryGetValue(name, out var globalValue) ? globalValue : null;
    }

    /// <summary>
    /// 设置变量值
    /// </summary>
    /// <param name="name">变量名</param>
    /// <param name="value">变量值</param>
    public void SetVariable(string name, object? value)
    {
        var currentScope = _scopeStack.Peek();
        currentScope.Variables[name] = value;
    }

    /// <summary>
    /// 是否包含变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>是否包含</returns>
    public bool HasVariable(string name)
    {
        // 检查所有作用域
        foreach (var scope in _scopeStack)
        {
            if (scope.Variables.ContainsKey(name))
            {
                return true;
            }
        }

        // 检查全局变量
        return _globalVariables.ContainsKey(name);
    }

    /// <summary>
    /// 移除变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>是否成功移除</returns>
    public bool RemoveVariable(string name)
    {
        var currentScope = _scopeStack.Peek();
        return currentScope.Variables.Remove(name);
    }

    /// <summary>
    /// 获取所有变量名
    /// </summary>
    /// <returns>变量名集合</returns>
    public IEnumerable<string> GetVariableNames()
    {
        var names = new HashSet<string>();

        // 收集所有作用域的变量名
        foreach (var scope in _scopeStack)
        {
            foreach (var name in scope.Variables.Keys)
            {
                names.Add(name);
            }
        }

        // 收集全局变量名
        foreach (var name in _globalVariables.Keys)
        {
            names.Add(name);
        }

        return names;
    }

    /// <summary>
    /// 推入作用域
    /// </summary>
    /// <returns>作用域标识</returns>
    public IDisposable PushScope()
    {
        var newScope = new TemplateScope(Guid.NewGuid().ToString(), _scopeStack.Peek());
        _scopeStack.Push(newScope);
        return new ScopeDisposer(_scopeStack);
    }

    /// <summary>
    /// 获取函数
    /// </summary>
    /// <param name="name">函数名</param>
    /// <returns>函数委托</returns>
    public Delegate? GetFunction(string name)
    {
        return _functions.TryGetValue(name, out var function) ? function : null;
    }

    /// <summary>
    /// 设置函数
    /// </summary>
    /// <param name="name">函数名</param>
    /// <param name="function">函数委托</param>
    public void SetFunction(string name, Delegate function)
    {
        _functions[name] = function;
    }

    /// <summary>
    /// 克隆上下文
    /// </summary>
    /// <returns>新的上下文实例</returns>
    public ITemplateContext Clone()
    {
        var cloned = new TemplateContext();

        // 复制全局变量
        foreach (var (key, value) in _globalVariables)
        {
            cloned._globalVariables[key] = value;
        }

        // 复制函数
        foreach (var (key, value) in _functions)
        {
            cloned._functions[key] = value;
        }

        // 复制当前作用域的变量
        var currentScope = _scopeStack.Peek();
        foreach (var (key, value) in currentScope.Variables)
        {
            cloned.SetVariable(key, value);
        }

        return cloned;
    }

    /// <summary>
    /// 设置全局变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <param name="value">变量值</param>
    internal void SetGlobalVariable(string name, object? value)
    {
        _globalVariables[name] = value;
    }
}
