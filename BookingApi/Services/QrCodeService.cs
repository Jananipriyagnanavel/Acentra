using QRCoder;

namespace BookingApi.Services;

public interface IQrCodeService
{
    /// <summary>Generates a random, unguessable check-in token. Contains no
    /// booking data itself — it's just an opaque key looked up server-side,
    /// so scanning it never leaks booking details directly.</summary>
    string GenerateToken();

    /// <summary>Renders a PNG QR code (as bytes) encoding the check-in URL for the given token.</summary>
    byte[] GenerateQrPng(string checkInBaseUrl, string token);
}

public class QrCodeService : IQrCodeService
{
    public string GenerateToken()
    {
        // 32 bytes of randomness, URL-safe base64 — effectively unguessable
        // and safe to put in a URL path.
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public byte[] GenerateQrPng(string checkInBaseUrl, string token)
    {
        var url = $"{checkInBaseUrl.TrimEnd('/')}/checkin/{token}";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(data);
        return pngQrCode.GetGraphic(20);
    }
}
