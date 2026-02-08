using System.Collections.Concurrent;

namespace MagickCrop;

internal static class Singleton<T> where T : new()
{
    private static ConcurrentDictionary<Type, T> _instances = new();

    public static T Instance
    {
        get
        {
            return _instances.GetOrAdd(typeof(T), (t) => new T());
        }
    }
}
