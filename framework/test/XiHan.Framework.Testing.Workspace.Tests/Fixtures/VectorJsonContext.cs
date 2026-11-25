// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Testing.Workspace.Tests.Fixtures;

/// <summary>
/// 向量模型的源生成序列化上下文
/// </summary>
/// <remarks>
/// 自己的选项是缺省的（不忽略大小写、不拒绝多出的成员）：<see cref="SpecVectors"/> 只取它的类型信息，读法照旧严格
/// </remarks>
[JsonSerializable(typeof(ConstructorVector))]
[JsonSerializable(typeof(RequiredMemberVector))]
[JsonSerializable(typeof(OuterVector))]
[JsonSerializable(typeof(ErrorCodeVectors))]
internal sealed partial class VectorJsonContext : JsonSerializerContext;
