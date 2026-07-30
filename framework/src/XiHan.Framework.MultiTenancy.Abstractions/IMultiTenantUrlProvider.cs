// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 多租户 URL 提供程序接口
/// 定义为多租户系统生成租户特定 URL 的契约
/// 支持根据当前租户上下文动态生成和解析 URL
/// </summary>
public interface IMultiTenantUrlProvider
{
    /// <summary>
    /// 异步获取基于模板的租户特定 URL
    /// 根据当前租户上下文和提供的 URL 模板生成最终的访问地址
    /// </summary>
    /// <param name="templateUrl">URL 模板字符串，可能包含租户相关的占位符</param>
    /// <returns>
    /// 表示异步操作的任务，任务结果为解析后的完整 URL 字符串
    /// </returns>
    /// <exception cref="ArgumentNullException">当 templateUrl 为 null 时</exception>
    /// <exception cref="ArgumentException">当 templateUrl 为空字符串或格式无效时</exception>
    /// <remarks>
    /// 此方法通常用于以下场景：
    /// <list type="bullet">
    /// <item>为不同租户生成独立的访问域名（如：tenant1.example.com）</item>
    /// <item>在 URL 路径中嵌入租户标识（如：/tenant/{tenantId}/api）</item>
    /// <item>根据租户配置选择不同的服务端点</item>
    /// <item>生成租户特定的静态资源 URL</item>
    /// </list>
    ///
    /// 示例用法：
    /// <code>
    /// // 模板：https://{tenant}.example.com/api/users
    /// var url = await urlProvider.GetUrlAsync("https://{tenant}.example.com/api/users");
    /// // 结果：https://acme.example.com/api/users
    /// </code>
    /// </remarks>
    Task<string> GetUrlAsync(string templateUrl);
}
