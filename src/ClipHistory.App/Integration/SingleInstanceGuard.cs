using System.Threading;

namespace ClipHistory.App.Integration;

public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "Local\\ClipHistory.SingleInstance.7F8D1D42";
    private readonly Mutex mutex;
    private readonly bool ownsMutex;
    private bool disposed;

    public SingleInstanceGuard()
    {
        mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        ownsMutex = createdNew;
    }

    public bool IsPrimaryInstance => ownsMutex;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (ownsMutex)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
        GC.SuppressFinalize(this);
    }
}

