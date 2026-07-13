using ClipHistory.Core.Models;
using ClipHistory.Core.Services;

namespace ClipHistory.Core.Tests.Services;

public sealed class ContentFingerprintTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 7, 12, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TextFingerprintIsStableAndPreservesExactText()
    {
        string first = ContentFingerprint.ForText("Hello\r\n世界");
        string second = ContentFingerprint.ForText("Hello\r\n世界");
        string differentCase = ContentFingerprint.ForText("hello\r\n世界");
        string differentLineEnding = ContentFingerprint.ForText("Hello\n世界");

        Assert.Equal(first, second);
        Assert.NotEqual(first, differentCase);
        Assert.NotEqual(first, differentLineEnding);
    }

    [Fact]
    public void ImageFingerprintIncludesPixelsAndDimensions()
    {
        byte[] twoPixels = [0, 0, 0, 255, 10, 20, 30, 255];
        byte[] changedPixels = [0, 0, 0, 255, 10, 20, 31, 255];

        string horizontal = ContentFingerprint.ForBgra32Image(twoPixels, 2, 1);
        string vertical = ContentFingerprint.ForBgra32Image(twoPixels, 1, 2);
        string changed = ContentFingerprint.ForBgra32Image(changedPixels, 2, 1);

        Assert.NotEqual(horizontal, vertical);
        Assert.NotEqual(horizontal, changed);
    }

    [Fact]
    public void ImageFingerprintRejectsMismatchedPixelLength()
    {
        Assert.Throws<ArgumentException>(
            () => ContentFingerprint.ForBgra32Image([0, 0, 0], 1, 1));
    }

    [Fact]
    public void FileFingerprintUsesWindowsCaseSlashAndOrderRules()
    {
        string first = ContentFingerprint.ForFiles(
            [@"C:\Users\Example\One.txt", @"D:\Images\Two.png"]);
        string equivalent = ContentFingerprint.ForFiles(
            [@"d:/images/two.PNG", @"c:/users/example/one.TXT"]);

        Assert.Equal(first, equivalent);
    }

    [Fact]
    public void FileFingerprintRejectsRelativePath()
    {
        Assert.Throws<ArgumentException>(
            () => ContentFingerprint.ForFiles([@"Documents\relative.txt"]));
    }

    [Fact]
    public void DuplicateCheckRequiresMatchingTypeAndHash()
    {
        string textHash = ContentFingerprint.ForText("same");
        HistoryItem first = CreateTextItem(textHash);
        HistoryItem second = CreateTextItem(textHash);
        HistoryItem different = CreateTextItem(ContentFingerprint.ForText("different"));

        Assert.True(ContentFingerprint.AreDuplicates(first, second));
        Assert.False(ContentFingerprint.AreDuplicates(first, different));
    }

    private static HistoryItem CreateTextItem(string hash)
    {
        return new HistoryItem(
            Guid.NewGuid(),
            ClipboardContentType.Text,
            hash,
            Timestamp,
            Timestamp,
            Timestamp,
            textContent: "same");
    }
}
