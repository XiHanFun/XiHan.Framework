#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScopeDisposer
// Guid:c22ba6c8-3486-42db-8eab-46472fcfee6e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:04:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
