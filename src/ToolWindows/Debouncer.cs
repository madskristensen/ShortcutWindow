using System.Collections.Concurrent;
using System.Threading;

namespace ShortcutWindow
{
    public static class Debouncer
    {
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _tokens = new();
        public static void Debounce(string uniqueKey, Action action, int milliseconds)
        {
            CancellationTokenSource token = _tokens.AddOrUpdate(uniqueKey,
                (key) => //key not found - create new
                {
                    return new CancellationTokenSource();
                },
                (key, existingToken) => //key found - cancel task and recreate
                {
                    existingToken.Cancel(); //cancel previous
                    return new CancellationTokenSource();
                }
            );

            //schedule execution after pause
            _ = Task.Delay(milliseconds, token.Token).ContinueWith(task =>
            {
                if (!task.IsCanceled)
                {
                    action(); //run
                    if (_tokens.TryRemove(uniqueKey, out CancellationTokenSource cts))
                    {
                        cts.Dispose(); //cleanup
                    }
                }
            }, token.Token);
        }
    }
}