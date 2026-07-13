using ClipHistory.Infrastructure.Storage;

namespace ClipHistory.Infrastructure.Tests.Storage;

public sealed class ImageFileStoreTests
{
    [Fact]
    public void SavePngWritesDataAndReturnsApplicationRelativePath()
    {
        using TemporaryDirectory temporaryDirectory = new();
        AppDataPaths paths = new(temporaryDirectory.Path);
        ImageFileStore store = new(paths);
        Guid id = Guid.NewGuid();
        byte[] expectedBytes = [137, 80, 78, 71, 1, 2, 3];

        string relativePath = store.SavePng(id, expectedBytes);
        string absolutePath = store.GetAbsolutePath(relativePath);

        Assert.False(Path.IsPathRooted(relativePath));
        Assert.StartsWith("images", relativePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedBytes, File.ReadAllBytes(absolutePath));
    }

    [Fact]
    public void SavePngAtomicallyReplacesExistingImageForSameItem()
    {
        using TemporaryDirectory temporaryDirectory = new();
        ImageFileStore store = new(new AppDataPaths(temporaryDirectory.Path));
        Guid id = Guid.NewGuid();
        string firstPath = store.SavePng(id, [1, 2, 3]);

        string secondPath = store.SavePng(id, [4, 5, 6]);

        Assert.Equal(firstPath, secondPath);
        Assert.Equal([4, 5, 6], File.ReadAllBytes(store.GetAbsolutePath(secondPath)));
    }

    [Fact]
    public void DeleteRemovesOnlyStoredImage()
    {
        using TemporaryDirectory temporaryDirectory = new();
        ImageFileStore store = new(new AppDataPaths(temporaryDirectory.Path));
        string relativePath = store.SavePng(Guid.NewGuid(), [1, 2, 3]);

        bool firstDelete = store.Delete(relativePath);
        bool secondDelete = store.Delete(relativePath);

        Assert.True(firstDelete);
        Assert.False(secondDelete);
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("data/not-an-image.png")]
    [InlineData(@"C:\outside.png")]
    public void GetAbsolutePathRejectsPathOutsideImagesDirectory(string unsafePath)
    {
        using TemporaryDirectory temporaryDirectory = new();
        ImageFileStore store = new(new AppDataPaths(temporaryDirectory.Path));

        Assert.Throws<ArgumentException>(() => store.GetAbsolutePath(unsafePath));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            string tempRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            Path = System.IO.Path.Combine(tempRoot, $"ClipHistory.Tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            string tempRoot = System.IO.Path.TrimEndingDirectorySeparator(
                System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()));
            string resolvedPath = System.IO.Path.GetFullPath(Path);
            if (!resolvedPath.StartsWith(
                tempRoot + System.IO.Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to delete a directory outside the temp root.");
            }

            if (Directory.Exists(resolvedPath))
            {
                Directory.Delete(resolvedPath, recursive: true);
            }
        }
    }
}
