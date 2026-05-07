using System;
using System.Collections.Concurrent;
using Il2CppInterop.Runtime;

namespace Jiangyu.API.Internal;

/// <summary>
/// Thread-safe cache for IL2CPP (classPointer, fieldName) -> field offset lookups.
/// Avoids repeated il2cpp_class_get_field_from_name calls for hot-path field reads.
/// </summary>
internal static class OffsetCache
{
    // Key: (classPointer, fieldName), Value: offset (0 = not found/invalid)
    private static readonly ConcurrentDictionary<(IntPtr, string), uint> _cache = new();

    // Pre-resolved common offsets
    internal static uint ObjectCachedPtrOffset;
    internal static uint ListItemsOffset;
    internal static uint ListSizeOffset;

    /// <summary>
    /// Initialize common offsets. Called once from APILoader.OnInitializeMelon.
    /// Failures are non-fatal — offsets remain 0 and callers check for that.
    /// </summary>
    internal static void Initialize()
    {
        try
        {
            // UnityEngine.Object.m_CachedPtr
            var objectClass = IL2CPP.GetIl2CppClass("UnityEngine.CoreModule.dll", "UnityEngine", "Object");
            if (objectClass != IntPtr.Zero)
            {
                ObjectCachedPtrOffset = ResolveOffset(objectClass, "m_CachedPtr");
            }
        }
        catch (Exception ex)
        {
            APILogger.ReportInternal("OffsetCache", "Failed to resolve m_CachedPtr offset", ex);
        }

        try
        {
            // System.Collections.Generic.List<T>._items and _size
            var listClass = IL2CPP.GetIl2CppClass("mscorlib.dll", "System.Collections.Generic", "List`1");
            if (listClass != IntPtr.Zero)
            {
                ListItemsOffset = ResolveOffset(listClass, "_items");
                ListSizeOffset = ResolveOffset(listClass, "_size");
            }
        }
        catch (Exception ex)
        {
            APILogger.ReportInternal("OffsetCache", "Failed to resolve List offsets", ex);
        }
    }

    /// <summary>
    /// Get or resolve the field offset for a given class pointer and field name.
    /// Returns 0 if the field cannot be found.
    /// </summary>
    internal static uint GetOrResolve(IntPtr classPointer, string fieldName)
    {
        if (classPointer == IntPtr.Zero || string.IsNullOrEmpty(fieldName))
            return 0;

        var key = (classPointer, fieldName);
        if (_cache.TryGetValue(key, out var offset))
            return offset;

        offset = ResolveOffset(classPointer, fieldName);
        _cache.TryAdd(key, offset);
        return offset;
    }

    /// <summary>
    /// Walks the class hierarchy from <paramref name="klass"/> to its root, returning
    /// the first field matching <paramref name="fieldName"/> exactly. Returns
    /// <see cref="IntPtr.Zero"/> if no match is found. Callers are responsible for
    /// passing a verified field name from dump.cs.
    /// </summary>
    internal static IntPtr FindField(IntPtr klass, string fieldName)
    {
        if (klass == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr searchKlass = klass;
        while (searchKlass != IntPtr.Zero)
        {
            IntPtr field = IL2CPP.il2cpp_class_get_field_from_name(searchKlass, fieldName);
            if (field != IntPtr.Zero)
                return field;
            searchKlass = IL2CPP.il2cpp_class_get_parent(searchKlass);
        }
        return IntPtr.Zero;
    }

    private static uint ResolveOffset(IntPtr classPointer, string fieldName)
    {
        var field = FindField(classPointer, fieldName);
        if (field == IntPtr.Zero)
            return 0;

        try
        {
            return IL2CPP.il2cpp_field_get_offset(field);
        }
        catch (Exception ex)
        {
            APILogger.ReportInternal("OffsetCache", $"Failed to get offset for field '{fieldName}'", ex);
            return 0;
        }
    }
}