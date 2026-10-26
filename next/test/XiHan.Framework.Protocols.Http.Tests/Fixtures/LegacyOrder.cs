// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Http.Tests.Fixtures;

/// <summary>
/// 没有登记为任何操作请求 / 响应的 JSON 类型：普通端点用它时只能显式声明同哪个操作的 Schema，否则生成文档失败。
/// </summary>
/// <param name="Name">名称。</param>
internal sealed record LegacyOrder(string Name);
