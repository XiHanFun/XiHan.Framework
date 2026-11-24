// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Testing;

namespace XiHan.Framework.ObjectStorage.Abstractions.Tests;

/// <summary>
/// 回放 <c>store-definitions.json</c>：命名存储实例的名字与定义的相等
/// </summary>
/// <remarks>
/// 规范 RFC-0012「命名存储实例」：实例名是 1 到 64 个小写字母、数字与连字符，字母或数字开头；两个定义实例名相同（区分大小写）、Provider 名相同（不区分大小写）、设置完全相同（键不区分大小写，值区分，与次序无关）才是同一个定义（ADR-0050、ADR-0277）。<c>providers</c> 列的内置 Provider 名分在两个 Provider 包里，由 <c>ObjectStorage.S3.Tests</c> 的 <c>BlobStoreProviderNameVectorTests</c> 对照
/// </remarks>
public sealed class BlobStoreDefinitionVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly StoreDefinitionVectors _vectors = SpecVectors.Read<StoreDefinitionVectors>("objectstorage", "store-definitions.json");

    [Fact]
    public void IsValidName_AcceptsTheValidNames()
    {
        Assert.NotEmpty(_vectors.Names.Valid);
        foreach (var name in _vectors.Names.Valid)
        {
            Assert.True(BlobStoreDefinition.IsValidName(name), name);
            Assert.Equal(name, new BlobStoreDefinition(name, _vectors.Providers[0], new Dictionary<string, string>()).Name);
        }
    }

    [Fact]
    public void IsValidName_RejectsTheInvalidNames()
    {
        Assert.NotEmpty(_vectors.Names.Invalid);
        foreach (var name in _vectors.Names.Invalid)
        {
            Assert.False(BlobStoreDefinition.IsValidName(name), name);
            Assert.Throws<ArgumentException>(() => new BlobStoreDefinition(name, _vectors.Providers[0], new Dictionary<string, string>()));
        }
    }

    [Fact]
    public void TheNameLengthLimit_IsTheOneInTheVectors()
    {
        // 实例名没有长度常量，上限写在正则里：用只差长度的名字核对两头
        Assert.True(BlobStoreDefinition.IsValidName(new string('a', _vectors.Names.MaxLength)));
        Assert.False(BlobStoreDefinition.IsValidName(new string('a', _vectors.Names.MaxLength + 1)));
    }

    [Fact]
    public void Describes_TellsWhetherTwoDefinitionsAreTheSame()
    {
        Assert.NotEmpty(_vectors.Same);
        var failures = new List<string>();
        foreach (var vector in _vectors.Same)
        {
            var left = new BlobStoreDefinition(vector.Left.Name, vector.Left.Provider, vector.Left.Settings);
            var right = new BlobStoreDefinition(vector.Right.Name, vector.Right.Provider, vector.Right.Settings);

            // 相等是对称的：两个方向都要与向量一致
            if (left.Describes(right) != vector.Same || right.Describes(left) != vector.Same)
            {
                failures.Add($"{vector.Name}: left describes right = {left.Describes(right)}, right describes left = {right.Describes(left)}, the vector has same = {vector.Same}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverBothSidesOfTheLimit_AndEveryPartOfADefinition()
    {
        // 防止向量换了写法后边界一条都没核对还照样通过：正好到上限的要收，只因超长而不合法的要拒
        Assert.Contains(_vectors.Names.Valid, name => name.Length == _vectors.Names.MaxLength);
        Assert.Contains(_vectors.Names.Invalid, name => name.Length == _vectors.Names.MaxLength + 1 && name.All(char.IsAsciiLetterOrDigit));

        // 用例里的 Provider 都是 providers 列的内置名字（不区分大小写），相等的三个部分各有只差那一处的用例
        Assert.NotEmpty(_vectors.Providers);
        Assert.All(_vectors.Same.SelectMany(static vector => new[] { vector.Left.Provider, vector.Right.Provider }), static provider => Assert.Contains(provider, _vectors.Providers, StringComparer.OrdinalIgnoreCase));
        Assert.Contains(_vectors.Same, static vector => vector.Same && vector.Left.Provider != vector.Right.Provider);
        Assert.Contains(_vectors.Same, static vector => vector.Same && SameSettings(vector, StringComparer.OrdinalIgnoreCase) && !SameSettings(vector, StringComparer.Ordinal));
        Assert.Contains(_vectors.Same, static vector => vector.Same && !vector.Left.Settings.Keys.SequenceEqual(vector.Right.Settings.Keys, StringComparer.OrdinalIgnoreCase));
        Assert.Contains(_vectors.Same, static vector => !vector.Same && vector.Left.Name != vector.Right.Name);
        Assert.Contains(_vectors.Same, static vector => !vector.Same && !string.Equals(vector.Left.Provider, vector.Right.Provider, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_vectors.Same, static vector => !vector.Same && vector.Left.Settings.Count != vector.Right.Settings.Count);
        Assert.Contains(_vectors.Same, static vector => !vector.Same && vector.Left.Settings.Any(pair => vector.Right.Settings.TryGetValue(pair.Key, out var value) && value != pair.Value && string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// 两个定义的设置按给定的键比较方式是否有同样的键、各键的值逐字相同
    /// </summary>
    private static bool SameSettings(SameCase vector, StringComparer keys)
    {
        var right = new Dictionary<string, string>(vector.Right.Settings, keys);
        return vector.Left.Settings.Count == right.Count && vector.Left.Settings.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Names">实例名的用例</param>
    /// <param name="Providers">内置的 Provider 名</param>
    /// <param name="Same">两个定义是不是同一个定义的用例</param>
    private sealed record StoreDefinitionVectors(StoreNameCases Names, string[] Providers, SameCase[] Same);

    /// <summary>
    /// 实例名的用例
    /// </summary>
    /// <param name="MaxLength">长度上限</param>
    /// <param name="Valid">合法的实例名</param>
    /// <param name="Invalid">不合法的实例名</param>
    private sealed record StoreNameCases(int MaxLength, string[] Valid, string[] Invalid);

    /// <summary>
    /// 一个用例：两个定义与它们是否相同
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Left">一个定义</param>
    /// <param name="Right">另一个定义</param>
    /// <param name="Same">是不是同一个定义</param>
    private sealed record SameCase(string Name, StoreDefinition Left, StoreDefinition Right, bool Same);

    /// <summary>
    /// 一个命名存储实例的定义
    /// </summary>
    /// <param name="Name">实例名</param>
    /// <param name="Provider">Provider 名</param>
    /// <param name="Settings">Provider 的设置</param>
    private sealed record StoreDefinition(string Name, string Provider, Dictionary<string, string> Settings);
}
