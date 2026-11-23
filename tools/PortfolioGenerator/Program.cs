using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ImageMagick;

namespace PortfolioGenerator;

internal static class Program
{
    private const int Width = 1600;
    private const int Height = 1000;
    private static readonly Brush Background = Brush("#0b1120");
    private static readonly Brush Panel = Brush("#16243d");
    private static readonly Brush Border = Brush("#34507a");
    private static readonly Brush Text = Brush("#eef4ff");
    private static readonly Brush Muted = Brush("#bdcde6");
    private static readonly Brush Accent = Brush("#72e0df");
    private static readonly Brush Gold = Brush("#ffd479");

    [STAThread]
    private static void Main()
    {
        var root = FindRoot();
        var output = Path.Combine(root, "docs");
        var pages = new[] { Cover(), Architecture(), Delivery() };
        SavePng(pages[0], Path.Combine(output, "freelancer-portfolio.png"));
        SaveGif(pages, Path.Combine(output, "freelancer-portfolio.gif"));
        SavePdf(pages, Path.Combine(output, "freelancer-portfolio.pdf"));
        Verify(output, pages.Length);
        Console.WriteLine("Created portfolio PNG, animated GIF, and PDF in " + output);
    }

    private static void Verify(string output, int pageCount)
    {
        using var png = new MagickImage(Path.Combine(output, "freelancer-portfolio.png"));
        if (png.Width != Width || png.Height != Height) throw new InvalidDataException("Incorrect PNG dimensions.");
        using var gif = new MagickImageCollection(Path.Combine(output, "freelancer-portfolio.gif"));
        if (gif.Count != pageCount || gif.Any(frame => frame.AnimationDelay != 350 || frame.AnimationIterations != 0))
            throw new InvalidDataException("Incorrect GIF frame count, timing or loop count.");
        using var pdf = PdfReader.Open(Path.Combine(output, "freelancer-portfolio.pdf"), PdfDocumentOpenMode.Import);
        if (pdf.PageCount != pageCount) throw new InvalidDataException("Incorrect PDF page count.");
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "docs", "architecture.md"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run the portfolio generator from the repository root or a subdirectory.");
    }

    private static RenderTargetBitmap Page(string title, int number, Action<DrawingContext> content)
    {
        var visual = new DrawingVisual();
        using (var d = visual.RenderOpen())
        {
            d.DrawRectangle(Background, null, new Rect(0, 0, Width, Height));
            d.DrawRectangle(Accent, null, new Rect(0, 0, Width, 10));
            Label(d, "SOFTWARE ENGINEERING  /  PORTFOLIO", 80, 52, 22, Accent, true);
            Label(d, title, 80, 105, 58, Text, true);
            d.DrawLine(new Pen(Border, 2), new Point(80, 203), new Point(1520, 203));
            Label(d, "PAUANESH KUMAR  |  GITHUB.COM/PAUANESH-KUMAR", 80, 949, 18, Muted, true);
            Label(d, $"{number} / 3", 1440, 949, 18, Muted, true);
            content(d);
        }
        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static RenderTargetBitmap Cover() => Page("E-Commerce Microservices", 1, d =>
    {
        Paragraph(d, "A .NET backend exploring service boundaries, event-driven commerce, resilient integrations and data ownership.", 80, 232, 1360, 31);
        Label(d, ".NET 10   /   KAFKA   /   POSTGRESQL   /   MONGODB   /   REDIS", 80, 350, 23, Gold, true);
        Card(d, 80, 425, 700, 175, "Commerce services", "User, product, cart, order and payment APIs divide responsibilities across independent projects.");
        Card(d, 820, 425, 700, 175, "Event-driven integration", "Kafka consumers and transactional outbox code connect the order and payment workflows.");
        Card(d, 80, 625, 700, 175, "Data & delivery", "PostgreSQL, MongoDB and Redis; Docker Compose, YARP and observability tooling.");
        Card(d, 820, 625, 700, 175, "Recovery design", "Transactional inventory and payment reconciliation tested in isolated scenarios.");
        Label(d, "SERVICE-ORIENTED DESIGN  |  ISOLATED INTEGRATION TESTING", 80, 849, 24, Gold, true);
    });

    private static RenderTargetBitmap Architecture() => Page("Architecture & workflows", 2, d =>
    {
        Paragraph(d, "A layered, service-oriented codebase with synchronous checkout and asynchronous event handling.", 80, 235, 1380, 30);
        var nodes = new[] { ("CLIENT", "HTTP client"), ("COMMERCE", "Cart / Order"), ("MESSAGING", "Kafka / Outbox"), ("CONSUMERS", "Payment / Analytics") };
        for (var i = 0; i < nodes.Length; i++)
        {
            var x = 80 + i * 365;
            d.DrawRoundedRectangle(Panel, new Pen(Accent, 2), new Rect(x, 355, 320, 150), 20, 20);
            Label(d, nodes[i].Item1, x + 22, 380, 19, Accent, true);
            Label(d, nodes[i].Item2, x + 22, 424, 25, Text, true);
            if (i < nodes.Length - 1) d.DrawLine(new Pen(Muted, 4), new Point(x + 325, 430), new Point(x + 357, 430));
        }
        Card(d, 80, 570, 700, 190, "Inventory consistency", "MongoDB transactions combine reservation, inbox and outbox changes; delivery is at least once.");
        Card(d, 820, 570, 700, 190, "Payment recovery", "Durable payment intent, signed webhooks and provider reconciliation handle uncertain outcomes.");
        Label(d, "KAFKA EVENTS  /  TRANSACTIONAL OUTBOX  /  DEDUPLICATION", 80, 835, 23, Gold, true);
    });

    private static RenderTargetBitmap Delivery() => Page("Delivery & verification", 3, d =>
    {
        Paragraph(d, "A practical engineering portfolio focused on system design and implementation evidence.", 80, 235, 1360, 31);
        Card(d, 80, 350, 700, 210, "Current implementation", "Layered ASP.NET Core services, YARP routing, Kafka consumers, outbox publishers and a Razorpay integration.");
        Card(d, 820, 350, 700, 210, "Infrastructure", "Compose describes the services and supporting databases, cache, messaging and monitoring tools.");
        Card(d, 80, 595, 700, 210, "Verification", "xUnit and isolated checkout, compensation, retry and duplicate-delivery tests use a payment-provider stub.", Gold);
        Card(d, 820, 595, 700, 210, "Technology", "ASP.NET Core on Kestrel, Kafka, PostgreSQL, MongoDB, Redis and Docker Compose.", Gold);
        Label(d, "PROJECT: GITHUB.COM/PAUANESH-KUMAR/ECOMMERCEMICROSERVICESAPP", 80, 849, 22, Accent, true);
    });

    private static void Card(DrawingContext d, double x, double y, double width, double height, string title, string body, Brush? color = null)
    {
        d.DrawRoundedRectangle(Panel, new Pen(Border, 2), new Rect(x, y, width, height), 20, 20);
        Label(d, title, x + 28, y + 22, 29, color ?? Accent, true);
        var text = Format(body, 24, Muted, false, width - 56);
        if (text.Height > height - 90) throw new InvalidOperationException($"Card content exceeds its bounds: {title}");
        d.DrawText(text, new Point(x + 28, y + 77));
    }

    private static void Paragraph(DrawingContext d, string text, double x, double y, double width, double size)
        => d.DrawText(Format(text, size, Muted, false, width), new Point(x, y));

    private static void Label(DrawingContext d, string text, double x, double y, double size, Brush color, bool bold)
        => d.DrawText(Format(text, size, color, bold, Width - x), new Point(x, y));

    private static FormattedText Format(string text, double size, Brush color, bool bold, double maxWidth)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), size, color, 1)
        { MaxTextWidth = maxWidth };

    private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;

    private static void SavePng(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(path);
        encoder.Save(output);
    }

    private static void SaveGif(IEnumerable<BitmapSource> images, string path)
    {
        using var animation = new MagickImageCollection();
        foreach (var image in images)
        {
            using var stream = new MemoryStream();
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(image));
            png.Save(stream);
            stream.Position = 0;
            var frame = new MagickImage(stream) { AnimationDelay = 350, AnimationIterations = 0 };
            animation.Add(frame);
        }
        if (animation.Count == 0) throw new InvalidOperationException("No portfolio frames were provided.");
        animation.Write(path, MagickFormat.Gif);
    }

    private static void SavePdf(IEnumerable<BitmapSource> images, string path)
    {
        using var document = new PdfDocument();
        foreach (var image in images)
        {
            using var stream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
            stream.Position = 0;
            using var pdfImage = XImage.FromStream(stream);
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(Width * 0.6);
            page.Height = XUnit.FromPoint(Height * 0.6);
            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawImage(pdfImage, 0, 0, page.Width.Point, page.Height.Point);
        }
        document.Save(path);
    }
}
