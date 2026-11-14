// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Caching.Abstractions.Tests;

/// <summary>
/// 应用自己的源生成上下文：只认识应用的类型，不认识框架组件的缓存类型
/// </summary>
[JsonSerializable(typeof(int))]
internal sealed partial class ApplicationJsonContext : JsonSerializerContext;
