using System.Security.Cryptography;

namespace CineBook.Application.Common;

public static class CodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string New(string prefix, int length = 8)
    {
        Span<char> buf = stackalloc char[length];
        for (var i = 0; i < length; i++)
            buf[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return prefix + new string(buf);
    }
}
