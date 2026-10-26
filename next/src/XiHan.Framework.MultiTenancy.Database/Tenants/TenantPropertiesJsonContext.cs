// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.MultiTenancy.Database.Tenants;

/// <summary>
/// 租户属性列（<see cref="TenantRecord.PropertiesJson"/>）的源生成序列化上下文：键区分大小写的字符串字典，不用反射，保持 AOT 兼容。
/// </summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class TenantPropertiesJsonContext : JsonSerializerContext;
