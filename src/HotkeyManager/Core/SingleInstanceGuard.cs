using System.Security.Principal;

namespace HotkeyManager.Core;

/// <summary>按 Windows 用户隔离的单实例互斥锁。</summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private int _disposed;

    private SingleInstanceGuard(Mutex mutex) => _mutex = mutex;

    /// <summary>
    /// 获取当前用户的实例锁。返回 null 表示该用户已有实例；异常表示无法建立可靠的实例锁。
    /// </summary>
    public static SingleInstanceGuard? TryAcquire()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var userSid = identity.User?.Value
            ?? throw new InvalidOperationException("无法获取当前 Windows 用户标识。");
        var mutex = new Mutex(initiallyOwned: false, $@"Global\HotkeyManager.SingleInstance.{userSid}");

        try
        {
            var acquired = false;
            try
            {
                acquired = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // 上一个实例异常退出时，当前线程已经取得被遗弃的互斥锁。
                acquired = true;
            }

            if (!acquired)
            {
                mutex.Dispose();
                return null;
            }

            return new SingleInstanceGuard(mutex);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            _mutex.ReleaseMutex();
        }
        finally
        {
            _mutex.Dispose();
        }
    }
}
