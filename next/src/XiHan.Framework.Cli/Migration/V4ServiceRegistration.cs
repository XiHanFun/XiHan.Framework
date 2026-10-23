// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-03 从注入标记接口推出的一条显式注册。
/// </summary>
/// <param name="Namespace">实现类所在命名空间。</param>
/// <param name="Implementation">实现类名。</param>
/// <param name="Lifetime">存期：<c>Transient</c>、<c>Scoped</c> 或 <c>Singleton</c>。</param>
/// <param name="Services">暴露的服务接口名；为空时注册自身。</param>
internal sealed record V4ServiceRegistration(string Namespace, string Implementation, string Lifetime, IReadOnlyList<string> Services);
