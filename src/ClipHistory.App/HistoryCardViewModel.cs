using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;
using ClipHistory.Core.Models;
using ClipHistory.App.Localization;
using ClipHistory.Infrastructure.Storage;

namespace ClipHistory.App;

public sealed class HistoryCardViewModel
{
    public HistoryCardViewModel(HistoryItem item, ImageFileStore imageFileStore)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(imageFileStore);
        Id = item.Id;
        TypeLabel = item.ContentType switch
        {
            ClipboardContentType.Text => LocalizationService.Get("Text"),
            ClipboardContentType.Image => LocalizationService.Get("Image"),
            ClipboardContentType.Files => item.FilePaths.Count == 1
                ? LocalizationService.Get("Files")
                : LocalizationService.Format("MultipleFiles", item.FilePaths.Count),
            _ => "未知",
        };
        TimeText = item.LastCopiedAtUtc.ToLocalTime().ToString(
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.CurrentCulture);
        PinButtonText = item.IsPinned ? LocalizationService.Get("Unpin") : LocalizationService.Get("Pinned");
        PinVisibility = item.IsPinned ? Visibility.Visible : Visibility.Collapsed;
        Preview = CreatePreview(item);
        ImageSource = LoadImage(item, imageFileStore);
    }

    public Guid Id { get; }
    public string TypeLabel { get; }
    public string TimeText { get; }
    public string PinButtonText { get; }
    public Visibility PinVisibility { get; }
    public string Preview { get; }
    public BitmapImage? ImageSource { get; }

    private static string CreatePreview(HistoryItem item)
    {
        if (item.ContentType == ClipboardContentType.Text)
        {
            const int maximumLength = 300;
            string text = item.TextContent ?? string.Empty;
            return text.Length <= maximumLength ? text : $"{text[..maximumLength]}…";
        }

        if (item.ContentType == ClipboardContentType.Files)
        {
            return string.Join(Environment.NewLine, item.FilePaths.Select(path =>
            {
                string name = Path.GetFileName(path);
                bool exists = File.Exists(path) || Directory.Exists(path);
                return exists ? name : LocalizationService.Format("MissingFile", name);
            }));
        }

        return string.Empty;
    }

    private static BitmapImage? LoadImage(HistoryItem item, ImageFileStore imageFileStore)
    {
        if (item.ContentType != ClipboardContentType.Image || item.ImageRelativePath is null)
        {
            return null;
        }

        string path;
        try
        {
            path = imageFileStore.GetAbsolutePath(item.ImageRelativePath);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (!File.Exists(path))
        {
            return null;
        }

        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 480;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
