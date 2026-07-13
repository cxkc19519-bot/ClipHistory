using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipHistory.Core.Models;
using ClipHistory.Core.Services;
using ClipHistory.Infrastructure.Storage;

namespace ClipHistory.App.Clipboard;

public sealed class ClipboardHistoryService
{
    private readonly SqliteHistoryRepository repository;
    private readonly ImageFileStore imageFileStore;
    private string? suppressedFingerprint;

    public ClipboardHistoryService(
        SqliteHistoryRepository repository,
        ImageFileStore imageFileStore)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.imageFileStore = imageFileStore ?? throw new ArgumentNullException(nameof(imageFileStore));
    }

    public bool TryCaptureCurrentClipboard(DateTimeOffset capturedAtUtc)
    {
        EnsureUtc(capturedAtUtc);

        if (System.Windows.Clipboard.ContainsFileDropList())
        {
            string[] filePaths = System.Windows.Clipboard.GetFileDropList().Cast<string>().ToArray();
            if (filePaths.Length == 0)
            {
                return false;
            }

            string fingerprint = ContentFingerprint.ForFiles(filePaths);
            if (ConsumeSuppression(fingerprint))
            {
                return false;
            }

            return AddOrRefresh(
                ClipboardContentType.Files,
                fingerprint,
                capturedAtUtc,
                filePaths: filePaths);
        }

        if (System.Windows.Clipboard.ContainsImage())
        {
            BitmapSource? source = System.Windows.Clipboard.GetImage();
            if (source is null || source.PixelWidth <= 0 || source.PixelHeight <= 0)
            {
                return false;
            }

            BitmapSource normalized = NormalizeToBgra32(source);
            byte[] pixels = CopyPixels(normalized);
            string fingerprint = ContentFingerprint.ForBgra32Image(
                pixels,
                normalized.PixelWidth,
                normalized.PixelHeight);
            if (ConsumeSuppression(fingerprint))
            {
                return false;
            }

            HistoryItem? existing = repository.GetByContentFingerprint(
                ClipboardContentType.Image,
                fingerprint);
            if (existing is not null)
            {
                existing.MarkCopied(capturedAtUtc);
                repository.UpdateState(existing);
                return true;
            }

            Guid id = Guid.NewGuid();
            string imageRelativePath = imageFileStore.SavePng(id, EncodePng(normalized));
            try
            {
                repository.Add(new HistoryItem(
                    id,
                    ClipboardContentType.Image,
                    fingerprint,
                    capturedAtUtc,
                    capturedAtUtc,
                    capturedAtUtc,
                    imageRelativePath: imageRelativePath));
            }
            catch
            {
                imageFileStore.Delete(imageRelativePath);
                throw;
            }

            return true;
        }

        if (System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText))
        {
            string text = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText);
            string fingerprint = ContentFingerprint.ForText(text);
            if (ConsumeSuppression(fingerprint))
            {
                return false;
            }

            return AddOrRefresh(
                ClipboardContentType.Text,
                fingerprint,
                capturedAtUtc,
                textContent: text);
        }

        return false;
    }

    public void CopyToClipboard(HistoryItem historyItem)
    {
        ArgumentNullException.ThrowIfNull(historyItem);
        suppressedFingerprint = historyItem.ContentHash;

        try
        {
            switch (historyItem.ContentType)
            {
                case ClipboardContentType.Text:
                    System.Windows.Clipboard.SetText(
                        historyItem.TextContent!,
                        System.Windows.TextDataFormat.UnicodeText);
                    break;

                case ClipboardContentType.Image:
                    System.Windows.Clipboard.SetImage(LoadImage(historyItem.ImageRelativePath!));
                    break;

                case ClipboardContentType.Files:
                    StringCollection paths = new();
                    paths.AddRange(historyItem.FilePaths.ToArray());
                    System.Windows.Clipboard.SetFileDropList(paths);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(historyItem),
                        historyItem.ContentType,
                        "Unsupported clipboard content type.");
            }
        }
        catch
        {
            suppressedFingerprint = null;
            throw;
        }
    }

    private bool AddOrRefresh(
        ClipboardContentType contentType,
        string fingerprint,
        DateTimeOffset capturedAtUtc,
        string? textContent = null,
        IReadOnlyList<string>? filePaths = null)
    {
        HistoryItem? existing = repository.GetByContentFingerprint(contentType, fingerprint);
        if (existing is not null)
        {
            existing.MarkCopied(capturedAtUtc);
            repository.UpdateState(existing);
            return true;
        }

        repository.Add(new HistoryItem(
            Guid.NewGuid(),
            contentType,
            fingerprint,
            capturedAtUtc,
            capturedAtUtc,
            capturedAtUtc,
            textContent: textContent,
            filePaths: filePaths));
        return true;
    }

    private bool ConsumeSuppression(string fingerprint)
    {
        if (!string.Equals(suppressedFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        suppressedFingerprint = null;
        return true;
    }

    private BitmapImage LoadImage(string relativePath)
    {
        string absolutePath = imageFileStore.GetAbsolutePath(relativePath);
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(absolutePath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static BitmapSource NormalizeToBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32)
        {
            return source;
        }

        FormatConvertedBitmap converted = new(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static byte[] CopyPixels(BitmapSource source)
    {
        int stride = checked(source.PixelWidth * 4);
        byte[] pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static byte[] EncodePng(BitmapSource source)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using MemoryStream stream = new();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Clipboard capture time must use UTC.", nameof(value));
        }
    }
}
