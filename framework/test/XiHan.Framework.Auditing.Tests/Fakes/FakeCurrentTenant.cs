// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Auditing.Tests.Fakes;

/// <summary>
/// 当前租户替身
/// </summary>
public sealed class FakeCurrentTenant : ICurrentTenant
{
    /// <summary>
    /// 当前租户是否可用
    /// </summary>
    public bool IsAvailable => Id.HasValue;

    /// <summary>
    /// 租户标识
    /// </summary>
    public long? Id { get; set; }

    /// <summary>
    /// 租户名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 临时切换租户，释放时还原
    /// </summary>
    /// <param name="id">租户标识</param>
    /// <param name="name">租户名称</param>
    /// <returns>还原器</returns>
    public IDisposable Change(long? id, string? name = null)
    {
        var restore = new RestoreScope(this, Id, Name);
        Id = id;
        Name = name;
        return restore;
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly FakeCurrentTenant _owner;
        private readonly long? _previousId;
        private readonly string? _previousName;

        public RestoreScope(FakeCurrentTenant owner, long? previousId, string? previousName)
        {
            _owner = owner;
            _previousId = previousId;
            _previousName = previousName;
        }

        public void Dispose()
        {
            _owner.Id = _previousId;
            _owner.Name = _previousName;
        }
    }
}
