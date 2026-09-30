// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.SqlSugar.Outbox;

/// <summary>
/// 基于异步本地存储的发件箱入箱连接范围
/// </summary>
public sealed class AsyncLocalSqlSugarOutboxConnectionScope : ISqlSugarOutboxConnectionScope
{
    private readonly AsyncLocal<string?> _configId = new();

    /// <summary>
    /// 当前范围指定的连接配置标识，未指定时为 null
    /// </summary>
    public string? ConfigId
    {
        get { return _configId.Value; }
    }

    /// <summary>
    /// 指定入箱写入的连接
    /// </summary>
    /// <param name="configId">承载业务数据的连接配置标识，须与连接配置标识原文一致（区分大小写）</param>
    /// <returns>释放时恢复外层指定连接的对象</returns>
    /// <exception cref="ArgumentException"><paramref name="configId"/> 为 null、空字符串或空白</exception>
    public IDisposable Use(string configId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configId);

        var previous = _configId.Value;
        _configId.Value = configId.Trim();

        return new ScopeRestorer(this, previous);
    }

    /// <summary>
    /// 释放时恢复外层指定的连接
    /// </summary>
    private sealed class ScopeRestorer : IDisposable
    {
        private readonly AsyncLocalSqlSugarOutboxConnectionScope _owner;
        private readonly string? _previous;
        private bool _disposed;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="owner">所属范围</param>
        /// <param name="previous">外层指定的连接配置标识</param>
        public ScopeRestorer(AsyncLocalSqlSugarOutboxConnectionScope owner, string? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        /// <summary>
        /// 恢复外层指定的连接
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner._configId.Value = _previous;
        }
    }
}
