#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EncryptionsTest
// Guid:ca687409-18d9-4f92-96f5-2583e34603d7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/16 4:38:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Encryptions;

namespace XiHan.Framework.Utils.Test.Encryptions;

public class EncryptionsTest
{
    /// <summary>
    /// AesHelper Test
    /// </summary>
    public static void AesHelperTest()
    {
        Console.WriteLine("AesHelper Test");

        var data = "AesHelperTest!";
        var password = "adsfdasfmdksafdsafdsaf";
        var encryptData = AesHelper.Encrypt(data, password);
        Console.WriteLine($"encryptData:{encryptData}");

        var decryptData = AesHelper.Decrypt(encryptData, password);
        Console.WriteLine($"decryptData:{decryptData}");
    }

    /// <summary>
    /// DesHelper Test
    /// </summary>
    public static void DesHelperTest()
    {
        Console.WriteLine("DesHelper Test");

        var data = "DesHelperTest";
        var encryptData = DesHelper.Encrypt(data);
        Console.WriteLine($"encryptData:{encryptData}");

        var decryptData = DesHelper.Decrypt(encryptData);
        Console.WriteLine($"decryptData:{decryptData}");
    }

    /// <summary>
    /// RsaHelper Test
    /// </summary>
    public static void RsaHelperTest()
    {
        Console.WriteLine("RsaHelper Test");

        var data = "RsaHelperTest";
        var (publicKey, privateKey) = RsaHelper.GenerateKeys();
        Console.WriteLine($"publicKey:{publicKey}");
        Console.WriteLine($"privateKey:{privateKey}");

        var encryptData = RsaHelper.Encrypt(data, publicKey);
        Console.WriteLine($"encryptData:{encryptData}");

        var decryptData = RsaHelper.Decrypt(encryptData, privateKey);
        Console.WriteLine($"decryptData:{decryptData}");

        var signData = RsaHelper.SignData(data, privateKey);
        Console.WriteLine($"signData:{signData}");

        var verify = RsaHelper.VerifyData(data, signData, publicKey);
        Console.WriteLine($"verify:{verify}");
    }

    /// <summary>
    /// EcdsaHelper Test
    /// </summary>
    public static void EcdsaHelperTest()
    {
        Console.WriteLine("EcdsaHelper Test");

        var data = "Hello, Test!";
        var (publicKey, privateKey) = EcdsaHelper.GenerateKeys();
        Console.WriteLine($"publicKey:{publicKey}");
        Console.WriteLine($"privateKey:{privateKey}");

        var signData = EcdsaHelper.SignData(data, privateKey);
        Console.WriteLine($"signData:{signData}");

        var verify = EcdsaHelper.VerifyData(data, signData, publicKey);
        Console.WriteLine($"verify:{verify}");
    }
}