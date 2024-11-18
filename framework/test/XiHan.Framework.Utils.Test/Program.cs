using XiHan.Framework.Utils.Security.Cryptography;

Console.WriteLine("Hello, Test!");

string ss = "{\"amount\":\"500\",\"merchantOrderNo\":\"20241118181906524010001\",\"customerName\":\"\",\"customerEmail\":\"\",\"customerPhone\":\"\",\"notifyUrl\":\"https://api.tkusdtapi.com/api/greenorder\"}";
string aesKey = "NjD/j53SSqnnNd3v";
string aesIv = "W7QzK1tQK9in04QY";
string sign = AesHelper.Encrypt(ss, aesKey, aesIv);
Console.WriteLine(sign);

Console.ReadKey();
