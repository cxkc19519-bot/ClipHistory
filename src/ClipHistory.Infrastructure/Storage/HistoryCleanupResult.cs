namespace ClipHistory.Infrastructure.Storage;

public sealed record HistoryCleanupResult(
    int DeletedItemCount,
    int DeletedImageCount,
    int FailedImageDeleteCount);

