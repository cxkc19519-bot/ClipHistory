using ClipHistory.Core.Models;

namespace ClipHistory.Core.Tests.Models;

public sealed class HistoryItemTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 12, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConstructorCreatesTextItem()
    {
        HistoryItem item = CreateTextItem("hello");

        Assert.Equal(ClipboardContentType.Text, item.ContentType);
        Assert.Equal("hello", item.TextContent);
        Assert.Null(item.ImageRelativePath);
        Assert.Empty(item.FilePaths);
    }

    [Fact]
    public void ConstructorAllowsWhitespaceOnlyTextBecauseClipboardTextIsPreserved()
    {
        HistoryItem item = CreateTextItem("   ");

        Assert.Equal("   ", item.TextContent);
    }

    [Fact]
    public void ConstructorCreatesImageItem()
    {
        HistoryItem item = new(
            Guid.NewGuid(),
            ClipboardContentType.Image,
            "image-hash",
            CreatedAtUtc,
            CreatedAtUtc,
            CreatedAtUtc,
            imageRelativePath: "images/image.png");

        Assert.Equal("images/image.png", item.ImageRelativePath);
    }

    [Fact]
    public void ConstructorCopiesFilePathCollection()
    {
        string[] sourcePaths = [@"C:\Files\one.txt"];
        HistoryItem item = new(
            Guid.NewGuid(),
            ClipboardContentType.Files,
            "files-hash",
            CreatedAtUtc,
            CreatedAtUtc,
            CreatedAtUtc,
            filePaths: sourcePaths);

        sourcePaths[0] = @"C:\Files\changed.txt";

        Assert.Equal(@"C:\Files\one.txt", item.FilePaths[0]);
    }

    [Fact]
    public void ConstructorRejectsPayloadThatDoesNotMatchContentType()
    {
        Assert.Throws<ArgumentException>(() => new HistoryItem(
            Guid.NewGuid(),
            ClipboardContentType.Text,
            "text-hash",
            CreatedAtUtc,
            CreatedAtUtc,
            CreatedAtUtc,
            imageRelativePath: "images/wrong.png"));
    }

    [Fact]
    public void ConstructorRejectsNonUtcTimestamp()
    {
        DateTimeOffset localTime = CreatedAtUtc.ToOffset(TimeSpan.FromHours(8));

        Assert.Throws<ArgumentException>(() => new HistoryItem(
            Guid.NewGuid(),
            ClipboardContentType.Text,
            "text-hash",
            localTime,
            localTime,
            localTime,
            textContent: "hello"));
    }

    [Fact]
    public void ConstructorRejectsLastCopiedTimeBeforeCreation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HistoryItem(
            Guid.NewGuid(),
            ClipboardContentType.Text,
            "text-hash",
            CreatedAtUtc,
            CreatedAtUtc.AddSeconds(-1),
            CreatedAtUtc,
            textContent: "hello"));
    }

    [Fact]
    public void PinChangesOnlyUnpinnedItem()
    {
        HistoryItem item = CreateTextItem("hello");
        DateTimeOffset originalRetentionBase = item.RetentionBaseAtUtc;

        bool firstResult = item.Pin();
        bool secondResult = item.Pin();

        Assert.True(firstResult);
        Assert.False(secondResult);
        Assert.True(item.IsPinned);
        Assert.Equal(originalRetentionBase, item.RetentionBaseAtUtc);
    }

    [Fact]
    public void UnpinResetsRetentionBaseToUnpinTime()
    {
        HistoryItem item = CreateTextItem("hello", isPinned: true);
        DateTimeOffset unpinnedAtUtc = CreatedAtUtc.AddHours(2);

        bool result = item.Unpin(unpinnedAtUtc);

        Assert.True(result);
        Assert.False(item.IsPinned);
        Assert.Equal(unpinnedAtUtc, item.RetentionBaseAtUtc);
    }

    [Fact]
    public void UnpinDoesNotResetRetentionBaseWhenItemIsAlreadyUnpinned()
    {
        HistoryItem item = CreateTextItem("hello");
        DateTimeOffset originalRetentionBase = item.RetentionBaseAtUtc;

        bool result = item.Unpin(CreatedAtUtc.AddHours(2));

        Assert.False(result);
        Assert.Equal(originalRetentionBase, item.RetentionBaseAtUtc);
    }

    [Fact]
    public void UnpinRejectsTimeBeforeMostRecentCopy()
    {
        DateTimeOffset lastCopiedAtUtc = CreatedAtUtc.AddHours(1);
        HistoryItem item = new(
            Guid.NewGuid(),
            ClipboardContentType.Text,
            "text-hash",
            CreatedAtUtc,
            lastCopiedAtUtc,
            CreatedAtUtc,
            isPinned: true,
            textContent: "hello");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => item.Unpin(lastCopiedAtUtc.AddTicks(-1)));
    }

    [Fact]
    public void UnpinRejectsNonUtcTime()
    {
        HistoryItem item = CreateTextItem("hello", isPinned: true);
        DateTimeOffset nonUtcTime = CreatedAtUtc.ToOffset(TimeSpan.FromHours(8));

        Assert.Throws<ArgumentException>(() => item.Unpin(nonUtcTime));
    }

    [Fact]
    public void MarkCopiedUpdatesTimeAndRetentionForUnpinnedItem()
    {
        HistoryItem item = CreateTextItem("hello");
        DateTimeOffset copiedAtUtc = CreatedAtUtc.AddMinutes(5);

        bool result = item.MarkCopied(copiedAtUtc);

        Assert.True(result);
        Assert.Equal(copiedAtUtc, item.LastCopiedAtUtc);
        Assert.Equal(copiedAtUtc, item.RetentionBaseAtUtc);
    }

    [Fact]
    public void MarkCopiedDoesNotChangeRetentionBaseForPinnedItem()
    {
        HistoryItem item = CreateTextItem("hello", isPinned: true);
        DateTimeOffset originalRetentionBase = item.RetentionBaseAtUtc;
        DateTimeOffset copiedAtUtc = CreatedAtUtc.AddMinutes(5);

        bool result = item.MarkCopied(copiedAtUtc);

        Assert.True(result);
        Assert.Equal(copiedAtUtc, item.LastCopiedAtUtc);
        Assert.Equal(originalRetentionBase, item.RetentionBaseAtUtc);
    }

    [Fact]
    public void MarkCopiedIsNoOpForSameTimestamp()
    {
        HistoryItem item = CreateTextItem("hello");

        bool result = item.MarkCopied(CreatedAtUtc);

        Assert.False(result);
    }

    [Fact]
    public void MarkCopiedRejectsTimelineMovingBackwards()
    {
        HistoryItem item = CreateTextItem("hello");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => item.MarkCopied(CreatedAtUtc.AddTicks(-1)));
    }

    private static HistoryItem CreateTextItem(string text, bool isPinned = false)
    {
        return new HistoryItem(
            Guid.NewGuid(),
            ClipboardContentType.Text,
            "text-hash",
            CreatedAtUtc,
            CreatedAtUtc,
            CreatedAtUtc,
            isPinned,
            textContent: text);
    }
}
