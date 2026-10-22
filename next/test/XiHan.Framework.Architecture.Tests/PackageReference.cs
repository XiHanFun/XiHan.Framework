// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 项目文件中的一条 <c>PackageReference</c>。
/// </summary>
/// <param name="Id">包 ID。</param>
/// <param name="IsPrivate">是否 <c>PrivateAssets="all"</c>；私有引用不进入包依赖。</param>
internal sealed record PackageReference(string Id, bool IsPrivate);
