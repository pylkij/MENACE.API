using System;
using System.Runtime.InteropServices;

namespace Jiangyu.API;

/// <summary>
/// Safe wrapper for IL2CPP native arrays. Reads elements directly from the
/// array's data region after the IL2CPP array header.
/// </summary>
public readonly struct GameArray
{
    private readonly IntPtr _arrayPointer;

    // IL2CPP array layout: [klass][monitor][bounds][max_length][data...]
    // Offsets assume 64-bit IL2CPP standard layout. Verify against dump.cs or
    // live probe before shipping.
    private static readonly int MaxLengthOffset = IntPtr.Size * 3; // Unverified
    private static readonly int DataOffset = IntPtr.Size * 4; // Unverified

    public GameArray(IntPtr arrayPointer)
    {
        _arrayPointer = arrayPointer;
    }

    public bool IsValid => _arrayPointer != IntPtr.Zero;

    public int Length
    {
        get
        {
            if (_arrayPointer == IntPtr.Zero) return 0;
            try
            {
                return Marshal.ReadInt32(_arrayPointer + MaxLengthOffset);
            }
            catch (Exception ex)
            {
                APIError.WarnInternal("GameArray.Length", $"Failed to read array length at 0x{_arrayPointer:X}: {ex.Message}");
                return 0;
            }
        }
    }

    /// <summary>
    /// Only valid for IL2CPP arrays whose element type is a reference type (object[]).
    /// For value-type arrays use ReadInt or ReadFloat.
    /// </summary>
    public GameObj this[int index]
    {
        get
        {
            if (_arrayPointer == IntPtr.Zero || index < 0) return GameObj.Null;

            try
            {
                var length = Marshal.ReadInt32(_arrayPointer + MaxLengthOffset);
                if (index >= length) return GameObj.Null;

                var ptr = Marshal.ReadIntPtr(_arrayPointer + DataOffset + index * IntPtr.Size);
                return new GameObj(ptr);
            }
            catch (Exception ex)
            {
                APIError.WarnInternal("GameArray[]", $"Failed to read element at index {index} from array 0x{_arrayPointer:X}: {ex.Message}");
                return GameObj.Null;
            }
        }
    }

    /// <summary>
    /// Read a primitive int element at the given index.
    /// Only valid for IL2CPP arrays whose element type is a 4-byte value type (e.g. int[]).
    /// Do NOT use on reference-type arrays — use the indexer instead.
    /// </summary>
    public int ReadInt(int index)
    {
        if (_arrayPointer == IntPtr.Zero || index < 0) return 0;
        try
        {
            var length = Marshal.ReadInt32(_arrayPointer + MaxLengthOffset);
            if (index >= length) return 0;
            return Marshal.ReadInt32(_arrayPointer + DataOffset + index * 4);
        }
        catch (Exception ex)
        {
            APIError.WarnInternal("GameArray.ReadInt", $"Failed to read int at index {index} from array 0x{_arrayPointer:X}: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Read a primitive float element at the given index.
    /// Only valid for IL2CPP arrays whose element type is a 4-byte value type (e.g. float[]).
    /// Do NOT use on reference-type arrays — use the indexer instead.
    /// </summary>
    public float ReadFloat(int index)
    {
        if (_arrayPointer == IntPtr.Zero || index < 0) return 0f;
        try
        {
            var length = Marshal.ReadInt32(_arrayPointer + MaxLengthOffset);
            if (index >= length) return 0f;
            var raw = Marshal.ReadInt32(_arrayPointer + DataOffset + index * 4);
            return BitConverter.Int32BitsToSingle(raw);
        }
        catch (Exception ex)
        {
            APIError.WarnInternal("GameArray.ReadFloat", $"Failed to read float at index {index} from array 0x{_arrayPointer:X}: {ex.Message}");
            return 0f;
        }
    }

    /// <summary>
    /// Enumerates elements as <see cref="GameObj"/>. Only valid for reference-type arrays.
    /// Value-type arrays (int[], float[]) must be iterated manually via ReadInt/ReadFloat.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator
    {
        private readonly GameArray _array;
        private int _index;
        private readonly int _length;

        internal Enumerator(GameArray array)
        {
            _array = array;
            _index = -1;
            _length = array.Length;
        }

        public readonly GameObj Current => _array[_index];

        public bool MoveNext()
        {
            _index++;
            return _index < _length;
        }
    }
}