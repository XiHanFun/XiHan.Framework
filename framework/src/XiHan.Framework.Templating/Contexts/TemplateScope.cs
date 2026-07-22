// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace XiHan.Framework.Templating.Contexts;

/// <summary>
/// 模板作用域实现
/// </summary>
internal class TemplateScope : ITemplateScope
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">作用域标识</param>
    /// <param name="parent">父作用域</param>
    public TemplateScope(string id, ITemplateScope? parent)
    {
        Id = id;
        Parent = parent;
        Variables = new ConcurrentDictionary<string, object?>();
    }

    /// <summary>
    /// 作用域标识
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 父作用域
    /// </summary>
    public ITemplateScope? Parent { get; }

    /// <summary>
    /// 是否为根作用域
    /// </summary>
    public bool IsRoot => Parent == null;

    /// <summary>
    /// 作用域变量
    /// </summary>
    public IDictionary<string, object?> Variables { get; }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Variables.Clear();
    }
}
