using QRCoder;

namespace CineBook.Web.Common;

public static class QrSvg
{
    public static string Render(string payload, int size)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);

        var stronger = new[] { QRCodeGenerator.ECCLevel.Q, QRCodeGenerator.ECCLevel.H };
        foreach (var level in stronger)
        {
            var candidate = generator.CreateQrCode(payload, level);
            if (candidate.Version == data.Version)
                data = candidate;
        }

        using var svg = new SvgQRCode(data);
        return svg.GetGraphic(new System.Drawing.Size(size, size), "#000000", "#FFFFFF", false,
            SvgQRCode.SizingMode.WidthHeightAttribute);
    }
}
