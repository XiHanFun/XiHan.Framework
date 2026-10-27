// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Grpc.Tests.Fixtures;

/// <summary>
/// 生成器对封闭泛型与字符串枚举的产出形态（与 <c>Kernel.Generators.Tests</c> 断言的文本同形）：
/// 根 <c>title</c> 带实参名，字符串枚举在 <c>$defs</c> 里是 <c>type: string</c> + <c>enum</c>，属性以 <c>$ref</c> 引用，可空为 <c>anyOf</c> 带 <c>null</c>。
/// </summary>
internal static class PagedSchemas
{
    /// <summary>
    /// 分页请求：<c>page</c>、可空枚举 <c>status</c>。
    /// </summary>
    public const string PageQuery = """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"PageQuery","type":"object","properties":{"page":{"type":"integer","format":"int32"},"status":{"anyOf":[{"$ref":"#/$defs/UserStatus"},{"type":"null"}]}},"$defs":{"UserStatus":{"type":"string","enum":["Active","Disabled"]}}}""";

    /// <summary>
    /// <c>PageResult&lt;UserResponse&gt;</c>：用户带枚举、可空枚举、枚举数组与枚举映射。
    /// </summary>
    public const string UsersPage = """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"PageResultOfUserResponse","type":"object","properties":{"items":{"type":"array","items":{"$ref":"#/$defs/UserResponse"}},"total":{"type":"integer","format":"int64"}},"required":["items","total"],"$defs":{"UserStatus":{"type":"string","enum":["Active","Disabled"]},"UserResponse":{"type":"object","properties":{"id":{"type":"string","format":"uuid"},"status":{"$ref":"#/$defs/UserStatus"},"previousStatus":{"anyOf":[{"$ref":"#/$defs/UserStatus"},{"type":"null"}]},"history":{"type":"array","items":{"$ref":"#/$defs/UserStatus"}},"byRole":{"type":"object","additionalProperties":{"$ref":"#/$defs/UserStatus"}}},"required":["id","status","history","byRole"]}}}""";

    /// <summary>
    /// <c>PageResult&lt;RoleResponse&gt;</c>：同一外层类型、不同实参。
    /// </summary>
    public const string RolesPage = """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"PageResultOfRoleResponse","type":"object","properties":{"items":{"type":"array","items":{"$ref":"#/$defs/RoleResponse"}},"total":{"type":"integer","format":"int64"}},"required":["items","total"],"$defs":{"RoleResponse":{"type":"object","properties":{"id":{"type":"string","format":"uuid"},"name":{"type":"string"}},"required":["id","name"]}}}""";
}
