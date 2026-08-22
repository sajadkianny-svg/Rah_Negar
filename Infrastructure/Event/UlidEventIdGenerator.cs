using System.Security.Cryptography;
using Rah_Negar.Foundation.Application.Event.Policies;

namespace Rah_Negar.Infrastructure.Event;

public sealed class UlidEventIdGenerator : IEventIdGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public string NewId()
    {
        Span<byte> data = stackalloc byte[16];
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        data[0] = (byte)(timestamp >> 40);
        data[1] = (byte)(timestamp >> 32);
        data[2] = (byte)(timestamp >> 24);
        data[3] = (byte)(timestamp >> 16);
        data[4] = (byte)(timestamp >> 8);
        data[5] = (byte)timestamp;
        RandomNumberGenerator.Fill(data[6..]);

        Span<char> output = stackalloc char[26];
        UInt128 value = 0;
        foreach (byte item in data)
            value = (value << 8) | item;
        for (int index = 25; index >= 0; index--)
        {
            output[index] = Alphabet[(int)(value & 31)];
            value >>= 5;
        }
        return new string(output);
    }
}
