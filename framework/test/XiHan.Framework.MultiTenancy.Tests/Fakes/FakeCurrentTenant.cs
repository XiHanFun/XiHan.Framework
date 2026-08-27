// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy.Tests.Fakes;

/// <summary>
/// 当前租户的手写替身
/// </summary>
/// <remarks>
/// 供 <see cref="TenantSettingValueProvider"/>、<see cref="Features.TenantFeatureChecker"/> 等消费方使用。
/// 这些被测类型只读取 <see cref="Id"/> 与 <see cref="Name"/>，因此替身把两者做成可直接赋值的属性，
/// 便于构造「只有唯一标识」「名称为空白」等边界组合，而不必绕道作用域切换。
/// </remarks>
internal sealed class FakeCurrentTenant : ICurrentTenant
{
    /// <summary>
    /// 当前租户唯一标识
    /// </summary>
    public long? Id { get; set; }

    /// <summary>
    /// 当前租户名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 当前租户是否可用
    /// </summary>
    public bool IsAvailable => Id.HasValue;

    /// <summary>
    /// 临时切换当前租户
    /// </summary>
    /// <param name="id">租户唯一标识</param>
    /// <param name="name">租户名称</param>
    /// <returns>用于还原上一层租户上下文的释放器</returns>
    public IDisposable Change(long? id, string? name = null)
    {
        var previousId = Id;
        var previousName = Name;

        Id = id;
        Name = name;

        return new RestoreScope(this, previousId, previousName);
    }

    /// <summary>
    /// 租户切换作用域
    /// </summary>
    private sealed class RestoreScope : IDisposable
    {
        private readonly FakeCurrentTenant _owner;
        private readonly long? _previousId;
        private readonly string? _previousName;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="owner">所属的当前租户替身</param>
        /// <param name="previousId">进入作用域之前的租户唯一标识</param>
        /// <param name="previousName">进入作用域之前的租户名称</param>
        public RestoreScope(FakeCurrentTenant owner, long? previousId, string? previousName)
        {
            _owner = owner;
            _previousId = previousId;
            _previousName = previousName;
        }

        /// <summary>
        /// 还原上一层租户上下文
        /// </summary>
        public void Dispose()
        {
            _owner.Id = _previousId;
            _owner.Name = _previousName;
        }
    }
}
