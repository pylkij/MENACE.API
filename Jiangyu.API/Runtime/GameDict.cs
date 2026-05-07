using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Jiangyu.API.Internal;

namespace Jiangyu.API;

/// <summary>
/// Safe wrapper for IL2CPP Dictionary&lt;K,V&gt; objects. Iterates the internal
/// _entries array, skipping tombstoned entries (hashCode &lt; 0).
/// </summary>
public readonly struct GameDict
{
    private readonly IntPtr _dictPointer;

    public GameDict(IntPtr dictPointer)
    {
        _dictPointer = dictPointer;
    }

    public GameDict(GameObj dictObj) : this(dictObj.Pointer) { }

    public bool IsValid => _dictPointer != IntPtr.Zero;

    /// <summary>
    /// Returns the actual number of entries in the dictionary.
    /// This is _count minus _freeCount (deleted entries awaiting reuse).
    /// </summary>
    public int Count
    {
        get
        {
            if (_dictPointer == IntPtr.Zero) return 0;

            try
            {
                var klass = IL2CPP.il2cpp_object_get_class(_dictPointer);

                var countOffset = OffsetCache.GetOrResolve(klass, "_count");
                if (countOffset == 0)
                {
                    APIError.ReportInternal("GameDict.Count", "Failed to resolve _count");
                    return 0;
                }

                var count = Marshal.ReadInt32(_dictPointer + (int)countOffset);

                var freeCountOffset = OffsetCache.GetOrResolve(klass, "_freeCount");
                if (freeCountOffset == 0)
                {
                    APIError.WarnInternal("GameDict.Count", "Failed to resolve _freeCount, count may include deleted entries");
                    return count;
                }

                var freeCount = Marshal.ReadInt32(_dictPointer + (int)freeCountOffset);
                return count - freeCount;
            }
            catch (Exception ex)
            {
                APIError.ReportInternal("GameDict.Count", "Failed to read count", ex);
                return 0;
            }
        }
    }

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator
    {
        private readonly IntPtr _entriesArray;
        private readonly int _count;
        private readonly int _entryStride;
        private readonly int _arrayHeader;
        private int _index;

        internal Enumerator(GameDict dict)
        {
            _entriesArray = IntPtr.Zero;
            _count = 0;
            _entryStride = 0;
            _arrayHeader = IntPtr.Size * 4; // Unverified — assumes standard IL2CPP array header size
            _index = -1;
            Current = (GameObj.Null, GameObj.Null);

            if (dict._dictPointer == IntPtr.Zero) return;

            try
            {
                var klass = IL2CPP.il2cpp_object_get_class(dict._dictPointer);

                // Read _entries array pointer
                var entriesOffset = OffsetCache.GetOrResolve(klass, "_entries");
                if (entriesOffset == 0) return;

                _entriesArray = Marshal.ReadIntPtr(dict._dictPointer + (int)entriesOffset);
                if (_entriesArray == IntPtr.Zero) return;

                // Read _count
                var countOffset = OffsetCache.GetOrResolve(klass, "_count");
                if (countOffset != 0)
                    _count = Marshal.ReadInt32(dict._dictPointer + (int)countOffset);

                // Resolve entry stride from the entries array's element class
                var arrKlass = IL2CPP.il2cpp_object_get_class(_entriesArray);
                if (arrKlass == IntPtr.Zero)
                {
                    APIError.ReportInternal("GameDict.Enumerator", "Failed to resolve entries array class");
                    return;
                }

                var elemKlass = IL2CPP.il2cpp_class_get_element_class(arrKlass);
                if (elemKlass == IntPtr.Zero)
                {
                    APIError.ReportInternal("GameDict.Enumerator", "Failed to resolve entry element class");
                    return;
                }

                _entryStride = (int)IL2CPP.il2cpp_class_instance_size(elemKlass) - (2 * IntPtr.Size); // Unverified
                if (_entryStride <= 0)
                {
                    APIError.ReportInternal("GameDict.Enumerator", $"Resolved entry stride is invalid: {_entryStride}");
                    return;
                }
            }
            catch (Exception ex)
            {
                APIError.ReportInternal("GameDict.Enumerator", "Init failed", ex);
            }
        }

        public (GameObj Key, GameObj Value) Current { get; private set; }

        public bool MoveNext()
        {
            if (_entriesArray == IntPtr.Zero || _entryStride <= 0)
                return false;

            while (true)
            {
                _index++;
                if (_index >= _count) return false;

                try
                {
                    var entryBase = _entriesArray + _arrayHeader + _index * _entryStride;

                    // Entry layout verified against dump.cs: [int hashCode][int next][TKey key][TValue value]
                    // +8 offset verified: hashCode (4 bytes) + next (4 bytes)
                    // hashCode < 0 tombstone convention verified against dump.cs
                    var hashCode = Marshal.ReadInt32(entryBase);
                    if (hashCode < 0) continue; // tombstoned entry

                    var keyPtr = Marshal.ReadIntPtr(entryBase + 8);
                    var valuePtr = Marshal.ReadIntPtr(entryBase + 8 + IntPtr.Size);

                    Current = (new GameObj(keyPtr), new GameObj(valuePtr));
                    return true;
                }
                catch (Exception ex)
                {
                    APIError.ReportInternal(
                        "GameDict.Enumerator",
                        $"Iteration aborted at index {_index}", ex);
                    return false;
                }
            }
        }
    }
}