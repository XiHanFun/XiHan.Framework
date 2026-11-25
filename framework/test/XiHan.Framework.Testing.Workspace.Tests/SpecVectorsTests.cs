// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using XiHan.Framework.Testing.Workspace.Tests.Fixtures;

namespace XiHan.Framework.Testing.Workspace.Tests;

/// <summary>
/// <see cref="SpecVectors"/> 的定位与严格读取
/// </summary>
/// <remarks>
/// 严格读取的各条分别经反射与源生成两条路走一遍：不加约束时两条路遇到缺了的成员都悄悄给缺省值（源生成下连 <see langword="init"/> 成员也传进空值），都得把缺了必需成员的文件拦下。读法的细节用临时目录里的文件核对，定位与公开的两个重载用规范里真实的向量核对
/// </remarks>
public sealed class SpecVectorsTests : IDisposable
{
    /// <summary>
    /// 放临时向量文件的目录
    /// </summary>
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("xihan-spec-vectors-");

    /// <inheritdoc />
    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void Directory_IsTheVectorsDirectoryOfTheWorkspace()
    {
        Assert.Equal(Path.Combine(FrameworkWorkspace.Root, "spec", "conformance", "vectors"), SpecVectors.Directory);
        Assert.True(Directory.Exists(SpecVectors.Directory), $"'{SpecVectors.Directory}' does not exist; is the spec submodule checked out?");
        Assert.Equal(Path.Combine(SpecVectors.Directory, "primitives", "error-codes.json"), SpecVectors.PathOf("primitives", "error-codes.json"));
    }

    [Fact]
    public void Read_ReplaysASpecVector_ByReflectionAndBySourceGeneration()
    {
        var reflected = SpecVectors.Read<ErrorCodeVectors>("primitives", "error-codes.json");
        var generated = SpecVectors.Read<ErrorCodeVectors>("primitives", "error-codes.json", VectorJsonContext.Default);

        Assert.NotEmpty(reflected.Valid);
        Assert.NotEmpty(reflected.Invalid);
        Assert.Equal(reflected.Valid, generated.Valid);
        Assert.Equal(reflected.Invalid, generated.Invalid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_TakesAMatchingDocument_AndSkipsTheTopLevelComment(bool sourceGenerated)
    {
        Assert.Equal(new ConstructorVector("orders", 2), Read<ConstructorVector>("""{"$comment":"For people.","name":"orders","count":2}""", sourceGenerated));
        // 成员名按 Web 缺省：不区分大小写
        Assert.Equal(new ConstructorVector("orders", 2), Read<ConstructorVector>("""{"Name":"orders","COUNT":2}""", sourceGenerated));
        Assert.Equal(new RequiredMemberVector { Name = "orders" }, Read<RequiredMemberVector>("""{"name":"orders"}""", sourceGenerated));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_RejectsAMemberTheModelDoesNotKnow(bool sourceGenerated)
    {
        var error = Assert.Throws<JsonException>(() => Read<ConstructorVector>("""{"name":"orders","count":2,"extra":true}""", sourceGenerated));

        Assert.Contains("'extra'", error.Message, StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => Read<RequiredMemberVector>("""{"name":"orders","extra":true}""", sourceGenerated));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_SkipsOnlyTheTopLevelComment(bool sourceGenerated)
    {
        // 别处的 $comment 是向量的内容，模型不认得就失败
        Assert.Throws<JsonException>(() => Read<OuterVector>("""{"inner":{"$comment":"Not for people.","name":"orders","count":2}}""", sourceGenerated));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_RejectsAMissingConstructorParameter(bool sourceGenerated)
    {
        var error = Assert.Throws<JsonException>(() => Read<ConstructorVector>("""{"name":"orders"}""", sourceGenerated));

        Assert.Contains("count", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<JsonException>(() => Read<OuterVector>("""{"inner":{"count":2}}""", sourceGenerated));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_RejectsAMissingRequiredMember(bool sourceGenerated)
    {
        var error = Assert.Throws<JsonException>(() => Read<RequiredMemberVector>("""{"count":2}""", sourceGenerated));

        Assert.Contains("name", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_RejectsRepeatedMembers(bool sourceGenerated)
    {
        Assert.Throws<JsonException>(() => Read<ConstructorVector>("""{"name":"orders","name":"billing","count":2}""", sourceGenerated));
        Assert.Throws<JsonException>(() => Read<OuterVector>("""{"inner":{"name":"orders","count":1,"count":2}}""", sourceGenerated));
    }

    [Fact]
    public void Read_ReportsAMissingFile_AnEmptyFile_AndADocumentThatIsNotAnObject()
    {
        var missing = Path.Combine(_directory.FullName, "missing.json");
        var notFound = Assert.Throws<FileNotFoundException>(() => SpecVectors.ReadFile(missing, TypeInfo<ConstructorVector>(sourceGenerated: false)));
        Assert.Equal(missing, notFound.FileName);
        Assert.Contains(missing, notFound.Message, StringComparison.Ordinal);

        Assert.Contains("is empty", Assert.Throws<InvalidOperationException>(() => Read<ConstructorVector>(string.Empty, sourceGenerated: false)).Message, StringComparison.Ordinal);
        Assert.Contains("is empty", Assert.Throws<InvalidOperationException>(() => Read<ConstructorVector>(" \n\t", sourceGenerated: false)).Message, StringComparison.Ordinal);
        Assert.Contains("only 'null'", Assert.Throws<InvalidOperationException>(() => Read<ConstructorVector>("null", sourceGenerated: false)).Message, StringComparison.Ordinal);
        Assert.Contains("is not a JSON object", Assert.Throws<InvalidOperationException>(() => Read<ConstructorVector>("""[{"name":"orders","count":2}]""", sourceGenerated: false)).Message, StringComparison.Ordinal);

        // 不合法的 JSON 照样是 JsonException，消息里带着文件
        var malformed = Assert.Throws<JsonException>(() => Read<ConstructorVector>("""{"name":""", sourceGenerated: false));
        Assert.Contains(_directory.FullName, malformed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_WithAResolverThatDoesNotKnowTheModel_Throws()
        => Assert.Throws<NotSupportedException>(() => SpecVectors.Read<UnlistedVector>("primitives", "error-codes.json", VectorJsonContext.Default));

    [Theory]
    [InlineData("", "error-codes.json")]
    [InlineData(" ", "error-codes.json")]
    [InlineData(".", "error-codes.json")]
    [InlineData("..", "error-codes.json")]
    [InlineData("primitives/nested", "error-codes.json")]
    [InlineData("primitives\\nested", "error-codes.json")]
    [InlineData("primitives", "")]
    [InlineData("primitives", "../error-codes.json")]
    [InlineData("primitives", "..")]
    public void PathOf_TakesOneDirectoryNameAndOneFileName(string area, string fileName)
    {
        Assert.ThrowsAny<ArgumentException>(() => SpecVectors.PathOf(area, fileName));
        Assert.ThrowsAny<ArgumentException>(() => SpecVectors.Read<ErrorCodeVectors>(area, fileName));
    }

    /// <summary>
    /// 把一段 JSON 写进临时文件再严格读取
    /// </summary>
    /// <param name="json">文件的内容</param>
    /// <param name="sourceGenerated">经源生成的类型信息读，否则经反射</param>
    private T Read<T>(string json, bool sourceGenerated)
    {
        var path = Path.Combine(_directory.FullName, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return SpecVectors.ReadFile(path, TypeInfo<T>(sourceGenerated));
    }

    /// <summary>
    /// 严格读取的选项给出的类型信息
    /// </summary>
    /// <param name="sourceGenerated">取源生成的类型信息，否则取反射的</param>
    private static JsonTypeInfo<T> TypeInfo<T>(bool sourceGenerated)
    {
        IJsonTypeInfoResolver resolver = sourceGenerated ? VectorJsonContext.Default : new DefaultJsonTypeInfoResolver();
        return (JsonTypeInfo<T>)SpecVectors.CreateOptions(resolver).GetTypeInfo(typeof(T));
    }
}
