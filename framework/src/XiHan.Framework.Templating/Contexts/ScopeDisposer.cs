// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Contexts;

/// <summary>
/// 作用域释放器
/// </summary>
internal class ScopeDisposer : IDisposable
{
    private readonly Stack<ITemplateScope> _scopeStack;
    private bool _disposed = false;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scopeStack">作用域栈</param>
    public ScopeDisposer(Stack<ITemplateScope> scopeStack)
    {
        _scopeStack = scopeStack;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed && _scopeStack.Count > 1)
        {
            var scope = _scopeStack.Pop();
            scope.Dispose();
            _disposed = true;
        }
    }
}
