// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DevTools.CommandLine.Validators;

namespace XiHan.Framework.DevTools.Tests.CommandLine;

/// <summary>
/// 内置验证器行为测试
/// </summary>
public class ValidatorTests
{
    /// <summary>
    /// 目录存在时 DirectoryExistsValidator 应通过
    /// </summary>
    [Fact]
    public void DirectoryExistsValidator_ExistingDirectory_ReturnsSuccess()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "xihan-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var validator = new DirectoryExistsValidator();

            var result = validator.Validate(tempDir);

            Assert.True(result.IsValid);
            Assert.Null(result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// 目录不存在时 DirectoryExistsValidator 应拒绝
    /// </summary>
    [Fact]
    public void DirectoryExistsValidator_NonExistentDirectory_ReturnsError()
    {
        var validator = new DirectoryExistsValidator();

        var result = validator.Validate(Path.Combine(Path.GetTempPath(), "no-such-dir-" + Guid.NewGuid().ToString("N")));

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    /// <summary>
    /// 目录验证器对 null 放行，对空字符串拒绝
    /// </summary>
    [Fact]
    public void DirectoryExistsValidator_NullOrEmpty_ReturnsExpected()
    {
        var validator = new DirectoryExistsValidator();

        Assert.True(validator.Validate(null).IsValid);
        Assert.False(validator.Validate("").IsValid);
        Assert.False(validator.Validate("   ").IsValid);
    }

    /// <summary>
    /// 文件存在时 FileExistsValidator 应通过
    /// </summary>
    [Fact]
    public void FileExistsValidator_ExistingFile_ReturnsSuccess()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "xihan-test-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(tempFile, "content");
        try
        {
            var validator = new FileExistsValidator();

            var result = validator.Validate(tempFile);

            Assert.True(result.IsValid);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// 文件不存在时 FileExistsValidator 应拒绝
    /// </summary>
    [Fact]
    public void FileExistsValidator_NonExistentFile_ReturnsError()
    {
        var validator = new FileExistsValidator();

        var result = validator.Validate(Path.Combine(Path.GetTempPath(), "no-such-file-" + Guid.NewGuid().ToString("N") + ".txt"));

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    /// <summary>
    /// 文件验证器对 null 放行，对空字符串拒绝
    /// </summary>
    [Fact]
    public void FileExistsValidator_NullOrEmpty_ReturnsExpected()
    {
        var validator = new FileExistsValidator();

        Assert.True(validator.Validate(null).IsValid);
        Assert.False(validator.Validate("").IsValid);
    }

    /// <summary>
    /// 范围内的值应通过 RangeValidator
    /// </summary>
    /// <param name="value">待验证值</param>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void RangeValidator_ValueInRange_ReturnsSuccess(int value)
    {
        var validator = new RangeValidator();

        var result = validator.Validate(value, [1, 10]);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// 超出范围的值应被 RangeValidator 拒绝
    /// </summary>
    /// <param name="value">待验证值</param>
    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void RangeValidator_ValueOutOfRange_ReturnsError(int value)
    {
        var validator = new RangeValidator();

        var result = validator.Validate(value, [1, 10]);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    /// <summary>
    /// RangeValidator 对 null 放行，对缺少参数或不可比较的值拒绝
    /// </summary>
    [Fact]
    public void RangeValidator_NullOrMissingParameters_ReturnsExpected()
    {
        var validator = new RangeValidator();

        Assert.True(validator.Validate(null, [1, 10]).IsValid);
        Assert.False(validator.Validate(5, null).IsValid);
        Assert.False(validator.Validate(5, [1]).IsValid);
        Assert.False(validator.Validate(new object(), [1, 10]).IsValid);
    }
}
