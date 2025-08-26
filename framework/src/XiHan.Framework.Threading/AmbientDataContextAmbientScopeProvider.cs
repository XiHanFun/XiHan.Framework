#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AmbientDataContextAmbientScopeProvider
// Guid:118c12ac-962a-4564-83a6-fdf1eab411db
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 6:03:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.Threading;

/// <summary>
/// 环境数据上下文环境作用域提供者
/// </summary>
public class AmbientDataContextAmbientScopeProvider<T> : IAmbientScopeProvider<T>
{
    private static readonly ConcurrentDictionary<string, ScopeItem> ScopeDictionary = new();

    private readonly IAmbientDataContext _dataContext;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dataContext"></param>
    public AmbientDataContextAmbientScopeProvider(IAmbientDataContext dataContext)
    {
        _ = Guard.NotNull(dataContext, nameof(dataContext));

        _dataContext = dataContext;

        Logger = NullLogger<AmbientDataContextAmbientScopeProvider<T>>.Instance;
    }

    /// <summary>
    /// 日志
    /// </summary>
    public ILogger<AmbientDataContextAmbientScopeProvider<T>> Logger { get; set; }

    /// <summary>
    /// 获取值
    /// </summary>
    /// <param name="contextKey"></param>
    /// <returns></returns>
    public T? GetValue(string contextKey)
    {
        var item = GetCurrentItem(contextKey);
        return item is null ? default : item.Value;
    }

    /// <summary>
    /// 开始作用域
    /// </summary>
    /// <param name="contextKey"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    public IDisposable BeginScope(string contextKey, T value)
    {
        var item = new ScopeItem(value, GetCurrentItem(contextKey));

        if (!ScopeDictionary.TryAdd(item.Id, item))
        {
            throw new XiHanException("无法添加项目！ScopeDictionary.TryAdd 返回 false！");
        }

        _dataContext.SetData(contextKey, item.Id);

        return new DisposeAction<ValueTuple<ConcurrentDictionary<string, ScopeItem>, ScopeItem, IAmbientDataContext, string>>(static state =>
        {
            var (scopeDictionary, item, dataContext, contextKey) = state;

            _ = scopeDictionary.TryRemove(item.Id, out item);

            if (item is null)
            {
                return;
            }

            if (item.Outer is null)
            {
                dataContext.SetData(contextKey, null);
                return;
            }

            dataContext.SetData(contextKey, item.Outer.Id);
        }, (ScopeDictionary, item, _dataContext, contextKey));
    }

    /// <summary>
    /// 获取当前项
    /// </summary>
    /// <param name="contextKey"></param>
    /// <returns></returns>
    private ScopeItem? GetCurrentItem(string contextKey)
    {
        return _dataContext.GetData(contextKey) is string objKey ? ScopeDictionary.GetOrDefault(objKey) : null;
    }

    /// <summary>
    /// 作用域项
    /// </summary>
    private class ScopeItem
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="value"></param>
        /// <param name="outer"></param>
        public ScopeItem(T value, ScopeItem? outer = null)
        {
            Id = Guid.NewGuid().ToString();

            Value = value;
            Outer = outer;
        }

        /// <summary>
        /// 主键
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 作用域项
        /// </summary>
        public ScopeItem? Outer { get; }

        /// <summary>
        /// 值
        /// </summary>
        public T Value { get; }
    }
}
