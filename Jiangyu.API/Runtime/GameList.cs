using System;
using System.Runtime.InteropServices;
using Jiangyu.API.Internal;

namespace Jiangyu.API;

/// <summary>
/// Safe wrapper for IL2CPP List&lt;T&gt; objects. Reads the internal _items array
/// and _size field directly via cached offsets.
/// <para>
/// The underlying list is live game state. <c>readonly</c> applies to the pointer
/// only — list contents can change at any time. Do not iterate while the game may
/// be mutating the list; the enumerator snapshots <c>Count</c> at construction and
/// has no mutation guard.
/// </para>
/// </summary>
public readonly struct GameList
{
    private readonly IntPtr _listPointer;

    // IL2CPP array header: [klass][monitor][bounds][max_length]
    // Each pointer-sized. Data starts after header.
    private static readonly int ArrayHeader = IntPtr.Size * 4; // Unverified
    private static readonly int MaxLengthOffset = IntPtr.Size * 3; // Unverified

    public GameList(IntPtr listPointer)
    {
        _listPointer = listPointer;
    }

    public GameList(GameObj listObj) : this(listObj.Pointer) { }

    public bool IsValid => _listPointer != IntPtr.Zero;

    public int Count
    {
        get
        {
            if (_listPointer == IntPtr.Zero) return 0;

            try
            {
                var sizeOffset = OffsetCache.ListSizeOffset;
                if (sizeOffset == 0)
                {
                    // Fallback: resolve from the object's class
                    var klass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(_listPointer);
                    sizeOffset = OffsetCache.GetOrResolve(klass, "_size"); // Verified with dump.cs
                }
                if (sizeOffset == 0) return 0;

                return Marshal.ReadInt32(_listPointer + (int)sizeOffset);
            }
            catch (Exception ex)
            {
                APILogger.ReportInternal("GameList.Count", "Failed to read _size field", ex);
                return 0;
            }
        }
    }

    // Access elements by zero-based index: list[0], list[1], etc.
    public GameObj this[int index]
    {
        get
        {
            if (_listPointer == IntPtr.Zero || index < 0) return GameObj.Null;

            try
            {
                var itemsPtr = GetItemsArray();
                if (itemsPtr == IntPtr.Zero) return GameObj.Null;

                // Validate against array max_length
                var maxLength = Marshal.ReadInt32(itemsPtr + MaxLengthOffset);
                if (index >= maxLength) return GameObj.Null;

                var elementPtr = Marshal.ReadIntPtr(itemsPtr + ArrayHeader + index * IntPtr.Size);
                return new GameObj(elementPtr);
            }
            catch (Exception ex)
            {
                APILogger.ReportInternal("GameList.Indexer", $"Failed to read element at index {index}", ex);
                return GameObj.Null;
            }
        }
    }

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator
    {
        private readonly GameList _list;
        private int _index;
        private readonly int _count;

        internal Enumerator(GameList list)
        {
            _list = list;
            _index = -1;
            _count = list.Count;
        }

        public readonly GameObj Current => _list[_index];

        public bool MoveNext()
        {
            _index++;
            return _index < _count;
        }
    }

    private IntPtr GetItemsArray()
    {
        if (_listPointer == IntPtr.Zero) return IntPtr.Zero;

        try
        {
            var itemsOffset = OffsetCache.ListItemsOffset;
            if (itemsOffset == 0)
            {
                var klass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(_listPointer);
                itemsOffset = OffsetCache.GetOrResolve(klass, "_items"); // Verified with dump.cs
            }
            if (itemsOffset == 0) return IntPtr.Zero;

            return Marshal.ReadIntPtr(_listPointer + (int)itemsOffset);
        }
        catch (Exception ex)
        {
            APILogger.ReportInternal("GameList.GetItemsArray", "Failed to read _items pointer", ex);
            return IntPtr.Zero;
        }
    }
}