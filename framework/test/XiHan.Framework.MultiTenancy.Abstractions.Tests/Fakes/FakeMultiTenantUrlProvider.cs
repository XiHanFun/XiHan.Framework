// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests.Fakes;

/// <summary>
/// 多租户 URL 提供程序的手写替身
/// </summary>
/// <remarks>
/// 按 <see cref="IMultiTenantUrlProvider"/> 的 XML 文档实现：
/// 把模板中的 <see cref="TenantPlaceholder"/> 占位符替换为当前租户名称（无名称时退化为租户唯一标识，无租户时退化为 <see cref="HostSegment"/>），
/// 并按文档声明的异常契约对 null / 空白模板抛出对应的参数异常。不做任何网络访问。
/// </remarks>
internal sealed class FakeMultiTenantUrlProvider : IMultiTenantUrlProvider
{
    /// <summary>
    /// 租户占位符
    /// </summary>
    public const string TenantPlaceholder = "{tenant}";

    /// <summary>
    /// 无租户（宿主）时使用的替换段
    /// </summary>
    public const string HostSegment = "host";

    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="currentTenant">当前租户</param>
    public FakeMultiTenantUrlProvider(ICurrentTenant currentTenant)
    {
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 获取基于模板的租户特定 URL
    /// </summary>
    /// <param name="templateUrl">URL 模板</param>
    /// <returns>解析后的完整 URL</returns>
    public Task<string> GetUrlAsync(string templateUrl)
    {
        ArgumentNullException.ThrowIfNull(templateUrl);

        if (string.IsNullOrWhiteSpace(templateUrl))
        {
            throw new ArgumentException("URL 模板不能为空。", nameof(templateUrl));
        }

        var replacement = _currentTenant.Name
            ?? _currentTenant.Id?.ToString(CultureInfo.InvariantCulture)
            ?? HostSegment;

        return Task.FromResult(templateUrl.Replace(TenantPlaceholder, replacement, StringComparison.Ordinal));
    }
}
