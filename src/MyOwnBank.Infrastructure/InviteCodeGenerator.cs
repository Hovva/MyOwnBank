using System.Security.Cryptography;
using MyOwnBank.Application.Abstractions;

namespace MyOwnBank.Infrastructure;

public sealed class InviteCodeGenerator : IInviteCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string CreateCode()
    {
        Span<char> chars = stackalloc char[8];

        for (var index = 0; index < chars.Length; index++)
        {
            chars[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
