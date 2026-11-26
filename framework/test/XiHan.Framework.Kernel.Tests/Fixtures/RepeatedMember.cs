// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 由转换器把同一个成员写两次的对象
/// </summary>
/// <param name="Name">写两次的成员名</param>
[JsonConverter(typeof(RepeatedMemberConverter))]
internal sealed record RepeatedMember(string Name);
