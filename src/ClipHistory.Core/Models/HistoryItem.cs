using System.Collections.ObjectModel;

namespace ClipHistory.Core.Models;

public sealed class HistoryItem
{
    public HistoryItem(
        Guid id,
        ClipboardContentType contentType,
        string contentHash,
        DateTimeOffset createdAtUtc,
        DateTimeOffset lastCopiedAtUtc,
        DateTimeOffset retentionBaseAtUtc,
        bool isPinned = false,
        string? textContent = null,
        string? imageRelativePath = null,
        IEnumerable<string>? filePaths = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The history item ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        EnsureUtc(lastCopiedAtUtc, nameof(lastCopiedAtUtc));
        EnsureUtc(retentionBaseAtUtc, nameof(retentionBaseAtUtc));

        if (lastCopiedAtUtc < createdAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastCopiedAtUtc),
                "The last copied time cannot be earlier than the creation time.");
        }

        if (retentionBaseAtUtc < createdAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retentionBaseAtUtc),
                "The retention base time cannot be earlier than the creation time.");
        }

        string[] copiedFilePaths = filePaths?.ToArray() ?? [];
        ValidateContent(contentType, textContent, imageRelativePath, copiedFilePaths);

        Id = id;
        ContentType = contentType;
        ContentHash = contentHash;
        CreatedAtUtc = createdAtUtc;
        LastCopiedAtUtc = lastCopiedAtUtc;
        RetentionBaseAtUtc = retentionBaseAtUtc;
        IsPinned = isPinned;
        TextContent = textContent;
        ImageRelativePath = imageRelativePath;
        FilePaths = Array.AsReadOnly(copiedFilePaths);
    }

    public Guid Id { get; }

    public ClipboardContentType ContentType { get; }

    public string ContentHash { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset LastCopiedAtUtc { get; private set; }

    public DateTimeOffset RetentionBaseAtUtc { get; private set; }

    public bool IsPinned { get; private set; }

    public string? TextContent { get; }

    public string? ImageRelativePath { get; }

    public ReadOnlyCollection<string> FilePaths { get; }

    public bool MarkCopied(DateTimeOffset copiedAtUtc)
    {
        EnsureUtc(copiedAtUtc, nameof(copiedAtUtc));

        DateTimeOffset earliestAllowed = IsPinned
            ? LastCopiedAtUtc
            : Max(LastCopiedAtUtc, RetentionBaseAtUtc);

        if (copiedAtUtc < earliestAllowed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(copiedAtUtc),
                "The copy time cannot move the history item timeline backwards.");
        }

        if (copiedAtUtc == LastCopiedAtUtc)
        {
            return false;
        }

        LastCopiedAtUtc = copiedAtUtc;
        if (!IsPinned)
        {
            RetentionBaseAtUtc = copiedAtUtc;
        }

        return true;
    }

    public bool Pin()
    {
        if (IsPinned)
        {
            return false;
        }

        IsPinned = true;
        return true;
    }

    public bool Unpin(DateTimeOffset unpinnedAtUtc)
    {
        EnsureUtc(unpinnedAtUtc, nameof(unpinnedAtUtc));

        if (unpinnedAtUtc < LastCopiedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unpinnedAtUtc),
                "The unpin time cannot be earlier than the most recent copy time.");
        }

        if (!IsPinned)
        {
            return false;
        }

        IsPinned = false;
        RetentionBaseAtUtc = unpinnedAtUtc;
        return true;
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamps must use the UTC offset.", parameterName);
        }
    }

    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second)
    {
        return first >= second ? first : second;
    }

    private static void ValidateContent(
        ClipboardContentType contentType,
        string? textContent,
        string? imageRelativePath,
        IReadOnlyCollection<string> filePaths)
    {
        bool hasImage = !string.IsNullOrWhiteSpace(imageRelativePath);
        bool hasFiles = filePaths.Count > 0;

        if (filePaths.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("File paths cannot contain empty values.", nameof(filePaths));
        }

        bool isValid = contentType switch
        {
            ClipboardContentType.Text => textContent is not null && !hasImage && !hasFiles,
            ClipboardContentType.Image => textContent is null && hasImage && !hasFiles,
            ClipboardContentType.Files => textContent is null && !hasImage && hasFiles,
            _ => throw new ArgumentOutOfRangeException(
                nameof(contentType),
                contentType,
                "The clipboard content type is not supported."),
        };

        if (!isValid)
        {
            throw new ArgumentException(
                "Exactly one content payload must match the selected clipboard content type.",
                nameof(contentType));
        }
    }
}
