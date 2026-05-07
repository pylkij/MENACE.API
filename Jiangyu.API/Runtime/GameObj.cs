using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Jiangyu.API.Internal;

namespace Jiangyu.API;

/// <summary>
/// Safe handle for an IL2CPP object. All reads return defaults on failure;
/// all writes return false on failure. Never throws.
/// </summary>
public readonly struct GameObj : IEquatable<GameObj>
{
    public IntPtr Pointer { get; }

    public bool IsNull => Pointer == IntPtr.Zero;

    /// <summary>
    /// Check if the underlying Unity object is still alive (m_CachedPtr != 0).
    /// </summary>
    public bool IsAlive
    {
        get
        {
            if (Pointer == IntPtr.Zero) return false;

            try
            {
                var offset = OffsetCache.ObjectCachedPtrOffset;
                if (offset == 0) return true; // can't verify, assume alive

                var cachedPtr = Marshal.ReadIntPtr(Pointer + (int)offset);
                return cachedPtr != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }
    }

    public GameObj(IntPtr pointer)
    {
        Pointer = pointer;
    }

    public static GameObj Null => default;

    // --- Field reads by name (resolve offset each time unless cached) ---

    public int ReadInt(string fieldName)
    {
        var offset = ResolveFieldOffset(fieldName);
        return offset == 0 ? 0 : ReadInt(offset);
    }

    public float ReadFloat(string fieldName)
    {
        var offset = ResolveFieldOffset(fieldName);
        return offset == 0 ? 0f : ReadFloat(offset);
    }

    public bool ReadBool(string fieldName)
    {
        var offset = ResolveFieldOffset(fieldName);
        if (offset == 0) return false;
        try
        {
            return Marshal.ReadByte(Pointer + (int)offset) != 0;
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj.ReadBool", $"Failed at offset {offset}", ex);
            return false;
        }
    }

    public IntPtr ReadPtr(string fieldName)
    {
        var offset = ResolveFieldOffset(fieldName);
        return offset == 0 ? IntPtr.Zero : ReadPtr(offset);
    }

    public string ReadString(string fieldName)
    {
        var ptr = ReadPtr(fieldName);
        if (ptr == IntPtr.Zero) return null;

        try
        {
            return IL2CPP.Il2CppStringToManaged(ptr);
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj.ReadString", $"Failed to read '{fieldName}'", ex);
            return null;
        }
    }

    public GameObj ReadObj(string fieldName)
    {
        var ptr = ReadPtr(fieldName);
        return new GameObj(ptr);
    }

    // --- Field writes by name ---

    public bool WriteInt(string fieldName, int value)
    {
        var offset = ResolveFieldOffset(fieldName);
        if (offset == 0) return false;
        try
        {
            Marshal.WriteInt32(Pointer + (int)offset, value);
            return true;
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj.WriteInt", $"Failed '{fieldName}'", ex);
            return false;
        }
    }

    public bool WriteFloat(string fieldName, float value)
    {
        var offset = ResolveFieldOffset(fieldName);
        if (offset == 0) return false;
        try
        {
            var intVal = BitConverter.SingleToInt32Bits(value);
            Marshal.WriteInt32(Pointer + (int)offset, intVal);
            return true;
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj.WriteFloat", $"Failed '{fieldName}'", ex);
            return false;
        }
    }

    public bool WritePtr(string fieldName, IntPtr value)
    {
        var offset = ResolveFieldOffset(fieldName);
        if (offset == 0) return false;
        try
        {
            Marshal.WriteIntPtr(Pointer + (int)offset, value);
            return true;
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj.WritePtr", $"Failed '{fieldName}'", ex);
            return false;
        }
    }

    // --- Field reads by pre-cached offset ---

    public int ReadInt(uint offset)
    {
        if (Pointer == IntPtr.Zero || offset == 0) return 0;
        try
        {
            return Marshal.ReadInt32(Pointer + (int)offset);
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj.ReadInt", $"Failed at offset {offset}", ex);
            return 0;
        }
    }

    public float ReadFloat(uint offset)
    {
        if (Pointer == IntPtr.Zero || offset == 0) return 0f;
        try
        {
            var raw = Marshal.ReadInt32(Pointer + (int)offset);
            return BitConverter.Int32BitsToSingle(raw);
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj.ReadFloat", $"Failed at offset {offset}", ex);
            return 0f;
        }
    }

    public IntPtr ReadPtr(uint offset)
    {
        if (Pointer == IntPtr.Zero || offset == 0) return IntPtr.Zero;
        try
        {
            return Marshal.ReadIntPtr(Pointer + (int)offset);
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj.ReadPtr", $"Failed at offset {offset}", ex);
            return IntPtr.Zero;
        }
    }

    // --- Type operations ---

    public GameType GetGameType()
    {
        if (Pointer == IntPtr.Zero) return GameType.Invalid;

        try
        {
            var klass = IL2CPP.il2cpp_object_get_class(Pointer);
            return GameType.FromPointer(klass);
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj.GetGameType", "Failed", ex);
            return GameType.Invalid;
        }
    }

    public bool Is(GameType type)
    {
        if (type == null || !type.IsValid || Pointer == IntPtr.Zero)
            return false;
        return type.IsAssignableFrom(Pointer);
    }

    public string GetTypeName()
    {
        return GetGameType().FullName;
    }

    /// <summary>
    /// Convert this GameObj to its managed IL2CPP proxy type.
    /// The proxy type is derived from the object's actual runtime class, so the result
    /// is always correctly typed. Returns null if the object is dead, has no managed
    /// proxy type, or construction fails.
    /// </summary>
    public object ToManaged()
    {
        if (Pointer == IntPtr.Zero || !IsAlive) return null;

        try
        {
            var gameType = GetGameType();
            var managedType = gameType?.ManagedType;
            if (managedType == null)
            {
                APIError.WarnInternal("GameObj.ToManaged", $"No managed type for {gameType?.FullName}");
                return null;
            }

            var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
            if (ptrCtor == null)
            {
                APIError.WarnInternal("GameObj.ToManaged", $"No IntPtr constructor on {managedType.Name}");
                return null;
            }

            return ptrCtor.Invoke(new object[] { Pointer });
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj.ToManaged", "Conversion failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Convert this GameObj to a specific managed IL2CPP proxy type.
    /// Verifies the object's actual runtime type is assignable to T before constructing the proxy.
    /// Returns null if the type check fails or construction fails.
    /// </summary>
    public T As<T>() where T : class
    {
        if (Pointer == IntPtr.Zero) return null;

        try
        {
            // Verify the object's actual type is assignable to T before constructing the proxy
            var gameType = GetGameType();
            var managedType = gameType?.ManagedType;
            if (managedType == null || !typeof(T).IsAssignableFrom(managedType))
            {
                APIError.WarnInternal("GameObj.As<T>",
                    $"Object type {gameType?.FullName} is not assignable to {typeof(T).Name}");
                return null;
            }

            var ptrCtor = typeof(T).GetConstructor(new[] { typeof(IntPtr) });
            if (ptrCtor == null)
            {
                APIError.WarnInternal("GameObj.As<T>", $"No IntPtr constructor on {typeof(T).Name}");
                return null;
            }

            return (T)ptrCtor.Invoke(new object[] { Pointer });
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj.As<T>", $"Conversion to {typeof(T).Name} failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get the Unity object name via the UnityEngine.Object.name property.
    /// </summary>
    public string GetName()
    {
        if (Pointer == IntPtr.Zero) return null;

        try
        {
            // Use the "name" property via managed type (UnityEngine.Object.name)
            // Verified: UnityEngine.Object.name — dump.cs
            var gameType = GetGameType();
            var managedType = gameType?.ManagedType;
            if (managedType != null)
            {
                var nameProp = managedType.GetProperty("name",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (nameProp != null)
                {
                    var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
                    if (ptrCtor != null)
                    {
                        var proxy = ptrCtor.Invoke(new object[] { Pointer });
                        var name = nameProp.GetValue(proxy);
                        if (name != null)
                            return name.ToString();
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj.GetName", "Failed to retrieve object name", ex);
            return null;
        }
    }

    // --- Equality ---

    public bool Equals(GameObj other) => Pointer == other.Pointer;
    public override bool Equals(object obj) => obj is GameObj other && Equals(other);
    public override int GetHashCode() => Pointer.GetHashCode();
    public static bool operator ==(GameObj left, GameObj right) => left.Pointer == right.Pointer;
    public static bool operator !=(GameObj left, GameObj right) => left.Pointer != right.Pointer;

    public override string ToString()
    {
        if (Pointer == IntPtr.Zero) return "GameObj.Null";
        var name = GetName();
        var typeName = GetTypeName();
        return name != null
            ? $"{typeName} '{name}' @ 0x{Pointer:X}"
            : $"{typeName} @ 0x{Pointer:X}";
    }

    // --- Internal helpers ---

    private uint ResolveFieldOffset(string fieldName)
    {
        if (Pointer == IntPtr.Zero || string.IsNullOrEmpty(fieldName))
            return 0;

        try
        {
            var klass = IL2CPP.il2cpp_object_get_class(Pointer);
            if (klass == IntPtr.Zero) return 0;
            return OffsetCache.GetOrResolve(klass, fieldName);
        }
        catch (Exception ex)
        {
            APIError.ReportInternal("GameObj", $"Failed to resolve offset for '{fieldName}'", ex);
            return 0;
        }
    }
}