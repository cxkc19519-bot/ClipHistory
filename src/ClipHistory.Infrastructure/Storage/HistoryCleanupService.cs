using ClipHistory.Core.Models;
using ClipHistory.Core.Services;

namespace ClipHistory.Infrastructure.Storage;

public sealed class HistoryCleanupService
{
    private readonly SqliteHistoryRepository repository;
    private readonly ImageFileStore imageFileStore;

    public HistoryCleanupService(
        SqliteHistoryRepository repository,
        ImageFileStore imageFileStore)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.imageFileStore = imageFileStore ?? throw new ArgumentNullException(nameof(imageFileStore));
    }

    public HistoryCleanupResult DeleteExpired(
        RetentionPeriod retentionPeriod,
        DateTimeOffset asOfUtc)
    {
        _ = retentionPeriod.ToTimeSpan();
        if (asOfUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The cleanup time must use the UTC offset.", nameof(asOfUtc));
        }

        int deletedItems = 0;
        int deletedImages = 0;
        int failedImages = 0;

        foreach (HistoryItem item in repository.GetAll())
        {
            if (!HistoryRetentionPolicy.IsExpired(item, retentionPeriod, asOfUtc))
            {
                continue;
            }

            if (!repository.Delete(item.Id))
            {
                continue;
            }

            deletedItems++;
            if (item.ContentType != ClipboardContentType.Image || item.ImageRelativePath is null)
            {
                continue;
            }

            try
            {
                if (imageFileStore.Delete(item.ImageRelativePath))
                {
                    deletedImages++;
                }
            }
            catch (IOException)
            {
                failedImages++;
            }
            catch (UnauthorizedAccessException)
            {
                failedImages++;
            }
            catch (ArgumentException)
            {
                failedImages++;
            }
        }

        return new HistoryCleanupResult(deletedItems, deletedImages, failedImages);
    }
}

