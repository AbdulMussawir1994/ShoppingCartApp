using System.Security.Cryptography;

namespace ProductsApi.Helpers;

public static class NumberGenerator
{
    public static string GenerateSixDigitNumber()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] bytes = new byte[4];
            int number;

            do
            {
                rng.GetBytes(bytes);
                number = BitConverter.ToInt32(bytes, 0) & 0x7FFFFFFF;
                number = number % 900000 + 100000; // 100000–999999
            }
            while (number < 100000 || number > 999999);

            return number.ToString();
        }
    }

    public static long GenerateSixDigitNumberWithLong()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] bytes = new byte[4];
            int number;

            do
            {
                rng.GetBytes(bytes);
                number = BitConverter.ToInt32(bytes, 0) & 0x7FFFFFFF;
                number = number % 900000 + 100000; // 100000–999999
            }
            while (number < 100000 || number > 999999);

            return number;
        }
    }

    public static string GenerateSixDigitNumberWithGuid()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] bytes = new byte[4];
            int number;

            do
            {
                rng.GetBytes(bytes);
                number = BitConverter.ToInt32(bytes, 0) & 0x7FFFFFFF;
                number = number % 900000 + 100000;
            }
            while (number < 100000 || number > 999999);

            string guidPart = Guid.NewGuid().ToString("N"); // no dashes
            return $"{number}-{guidPart}"; // e.g., 1234569f0d8ae4e4214d2a88670b1e4c64
        }
    }
}
