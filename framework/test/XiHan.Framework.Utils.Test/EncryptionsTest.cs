using XiHan.Framework.Utils.Encryptions;

namespace XiHan.Framework.Utils.Test;

internal static class EncryptionsTest
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
    /// EcdsaHelper Test
    /// </summary>
    public static void EcdsaHelperTest()
    {
        Console.WriteLine("EcdsaHelper Test");

        var data = "Hello, Test!";
        var (publicKey, privateKey) = EcdsaHelper.GenerateKeys();
        Console.WriteLine($"publicKey:{publicKey}");
        Console.WriteLine($"privateKey:{privateKey}");

        var signature = EcdsaHelper.SignData(data, privateKey);
        Console.WriteLine($"signature:{signature}");

        var verify = EcdsaHelper.VerifyData(data, signature, publicKey);
        Console.WriteLine($"verify:{verify}");
    }
}