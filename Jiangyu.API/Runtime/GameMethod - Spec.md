## GameMethod — Spec

**File:** `Runtime/GameMethod.cs`
**Namespace:** `Jiangyu.API`
**Tier:** 1 (depends on `GamePatch` for type resolution, `APILogger` for errors)

### Purpose
Invoke methods on IL2CPP game objects by name via reflection, with cached `MethodInfo` lookups to avoid repeated scanning. Complements `GameObj` field reads with method call capability.

### Required Features

**Static method calls** (for singletons like `TacticalManager.Get()`)
```csharp
GameMethod.CallStatic("Il2CppMenace.Tactical.TacticalManager", "Get") // returns object
```

**Instance method calls**
```csharp
GameMethod.CallInt(instancePtr, "Il2CppMenace.Tactical.TacticalManager", "GetRound")
GameMethod.CallBool(instancePtr, "Il2CppMenace.Tactical.TacticalManager", "IsPlayerTurn")
GameMethod.Call(instancePtr, "Il2CppMenace.Tactical.TacticalManager", "MethodName") // returns object
```

**Caching**
- `MethodInfo` lookups cached by `typeName + methodName` key, same pattern as `GamePatch._typeCache`
- Cache survives for the lifetime of the process — `MethodInfo` on a stable game assembly does not change

**Error handling**
- Never throws — all failures route to `APILogger.ReportInternal`
- Returns `default` for the return type on failure (`0`, `false`, `null`, `IntPtr.Zero`)

**Overload resolution**
- Simple case: match by name only when unambiguous
- Overloaded case: accept optional `Type[]` parameter types to disambiguate, same pattern as `GamePatch.PatchInternalWithParams`

### Immediate Call Sites in TacticalEvents
```csharp
// Get singleton instance
var tm = GameMethod.CallStatic("Il2CppMenace.Tactical.TacticalManager", "Get");

// Get current round number
GameMethod.CallInt(tmPtr, "Il2CppMenace.Tactical.TacticalManager", "GetRound");

// Check if it is the player's turn
GameMethod.CallBool(tmPtr, "Il2CppMenace.Tactical.TacticalManager", "IsPlayerTurn");
```

### What It Should Not Do
- No Unity object discovery — that is `GameQuery`'s responsibility
- No field reads or writes — that is `GameObj`'s responsibility
- No Harmony patching — that is `GamePatch`'s responsibility

---

The three immediate call sites in `TacticalEvents` are the minimum needed to unblock the commented-out code. Everything else in the spec is for completeness and future use.