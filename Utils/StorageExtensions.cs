using System;
using Avalonia.Platform.Storage;

namespace AvaloniaMvvmDraw.Utils
{
    internal static class StorageExtensions
    {
        // Attempt to get a local filesystem path from an IStorageItem if the platform exposes one.
        public static string? TryGetLocalPath(this IStorageItem item)
        {
            try
            {
                // On some platforms IStorageItem may implement additional properties (like "Path").
                var t = item.GetType();
                var pi = t.GetProperty("Path");
                if (pi != null)
                {
                    var v = pi.GetValue(item) as string;
                    if (!string.IsNullOrEmpty(v)) return v;
                }

                pi = t.GetProperty("FullPath");
                if (pi != null)
                {
                    var v = pi.GetValue(item) as string;
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }
    }
}
