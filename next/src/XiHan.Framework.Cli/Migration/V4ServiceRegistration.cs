// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 写进 <c>&lt;项目末段&gt;V4Services.cs</c> 的一条显式注册。
/// </summary>
/// <param name="Namespace">实现类所在命名空间。</param>
/// <param name="Implementation">实现类名。</param>
/// <param name="Lifetime">存期：<c>Transient</c>、<c>Scoped</c> 或 <c>Singleton</c>。</param>
/// <param name="Services">暴露的服务接口名；为空时注册自身。</param>
/// <param name="Topics">消息处理器订阅的主题表达式（V4-08）；非空时以 <c>AddMessageHandler&lt;T&gt;(topic)</c> 登记，不再按存期注册。</param>
internal sealed record V4ServiceRegistration(string Namespace, string Implementation, string Lifetime, IReadOnlyList<string> Services, IReadOnlyList<string>? Topics = null);
