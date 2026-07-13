using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ClipHistory.Core.Models;

namespace ClipHistory.Core.Services;

public static class ContentFingerprint
{
    private const int Bgra32BytesPerPixel = 4;

    public static string ForText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Compute("text", Encoding.UTF8.GetBytes(text));
    }

    public static string ForBgra32Image(
        ReadOnlySpan<byte> pixelBytes,
        int pixelWidth,
        int pixelHeight)
    {
        if (pixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), "Image width must be positive.");
        }

        if (pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), "Image height must be positive.");
        }

        long expectedLength = (long)pixelWidth * pixelHeight * Bgra32BytesPerPixel;
        if (pixelBytes.Length != expectedLength)
        {
            throw new ArgumentException(
                "The pixel data length must match tightly packed BGRA32 dimensions.",
                nameof(pixelBytes));
        }

        Span<byte> dimensions = stackalloc byte[sizeof(int) * 2];
        BinaryPrimitives.WriteInt32LittleEndian(dimensions, pixelWidth);
        BinaryPrimitives.WriteInt32LittleEndian(dimensions[sizeof(int)..], pixelHeight);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendSegment(hash, "image-bgra32"u8);
        AppendSegment(hash, dimensions);
        AppendSegment(hash, pixelBytes);

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public static string ForFiles(IEnumerable<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        string[] normalizedPaths = filePaths
            .Select(NormalizeWindowsPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedPaths.Length == 0)
        {
            throw new ArgumentException("At least one file path is required.", nameof(filePaths));
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendSegment(hash, "files-windows"u8);

        foreach (string path in normalizedPaths)
        {
            AppendSegment(hash, Encoding.UTF8.GetBytes(path.ToUpperInvariant()));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public static bool AreDuplicates(HistoryItem first, HistoryItem second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return first.ContentType == second.ContentType
            && string.Equals(first.ContentHash, second.ContentHash, StringComparison.Ordinal);
    }

    private static string Compute(string domain, ReadOnlySpan<byte> content)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendSegment(hash, Encoding.UTF8.GetBytes(domain));
        AppendSegment(hash, content);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendSegment(IncrementalHash hash, ReadOnlySpan<byte> segment)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, segment.Length);
        hash.AppendData(length);
        hash.AppendData(segment);
    }

    private static string NormalizeWindowsPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string windowsPath = path.Replace('/', Path.DirectorySeparatorChar);
        if (!Path.IsPathFullyQualified(windowsPath))
        {
            throw new ArgumentException("File paths must be fully qualified Windows paths.", nameof(path));
        }

        string fullPath = Path.GetFullPath(windowsPath);
        string? root = Path.GetPathRoot(fullPath);

        return root is not null && fullPath.Length > root.Length
            ? Path.TrimEndingDirectorySeparator(fullPath)
            : fullPath;
    }
}
