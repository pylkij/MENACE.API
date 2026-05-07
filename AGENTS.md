# AGENTS.md — Jiangyu API

This file gives a future agent the context needed to assist the operator without
re-litigating decisions already made. Read it fully before responding to any request
related to the Jiangyu API project.

---

## What This Project Is

**Jiangyu** is a modkit for an IL2CPP Unity game built on MelonLoader. It is not a
mod manager, not a platform, not a full IDE. It is a modkit.

**Jiangyu.API** is the runtime code layer of that modkit — a single `.dll` that
MelonLoader loads once, giving modders safe access to game types, fields, and methods
without writing raw IL2CPP access code. It is one layer within Jiangyu, not the
whole thing.

The game uses **Unity 6** and **IL2CPP**. MelonLoader is the mod loader.
HarmonyLib (bundled with MelonLoader) handles method patching.

---

## Source of Truth

| Document | Role |
|---|---|
| `PRINCIPLES.md` | The governing design document. Every structural decision defers to it. |
| `jiangyu-api-guide.md` | The implementation guide. Parts 1–8 walk a novice through building Tier 1 and Tier 3. |
| `dump.cs` (operator-generated) | Ground truth for all game field, method, and class names. Always verify against this before treating a name as correct. |
| Menace SDK source files | Reference implementation only. Port with modifications — do not copy blindly. |

If a decision conflicts with `PRINCIPLES.md`, the principles win over convenience.
If a game-specific name is unverified against `dump.cs`, it is an assumption, not a fact.

---

## Decisions Already Made

Do not relitigate these without a strong technical reason. Summarise the reason
before proposing a change.

| Decision | Rationale |
|---|---|
| Named `Jiangyu.API`, not `Jiangyu.SDK` | The `.dll` exposes an API, not a full kit. SDK implies tools, examples, and a companion app — scope Jiangyu explicitly does not want. |
| Entry point class is `APILoader`, not `SDKLoader` | Consistency with the API naming. |
| Error system is `APIError`, not `ModError` | `ModError` is Menace-specific naming. `_log` conflicts with the common MelonLogger convention modders already use. |
| `APIError.cs` is written from scratch, not ported from `ModError.cs` | The Menace ring buffer, rate limiting, and deduplication exist to feed a DevConsole. Jiangyu has no DevConsole. Carrying that complexity violates Principle 8. |
| `APIError` writes to `Latest.log` via `MelonLogger` only | Jiangyu has no separate log file. MelonLoader's log is the output surface. |
| `APIError` keeps a running `ErrorCount` integer | Sufficient to drive a future lightweight in-game notification without the overhead of storing entries. |
| `APIError` public methods are named by severity: `Error`, `Warn`, `Info`, `Fatal` | `Report` was ambiguous — its severity was implicit. Naming by severity is self-documenting and consistent. |
| `ErrorSeverity` is a plain enum, not a `[Flags]` enum | Flags were needed for DevConsole filtering. No DevConsole, no flags needed. |
| `OffsetCache.cs` is ported |
| Localization methods stripped from `Templates.cs` | They depend on `MultiLingualLocalization`, a separate SDK file not being ported. Removable without affecting anything above them in the file. |
| `Templates.Clone` retained but documented as not registering in `DataTemplateLoader` | Runtime cloning is valid. Registration is a data-layer concern. The boundary is kept honest per Principle 6. |
| `DataTemplateLoader` string must be verified against `dump.cs` before shipping | It is a runtime string lookup against a game class name. Unverified assumptions must not be baked into the foundation per Principle 2. |
| No DevConsole, no REPL, no Lua scripting | These are Menace platform features. Jiangyu's scope is explicitly a modkit, not a platform. Principle 8. |
| `Private>false</Private>` on all references | References are present at runtime via MelonLoader. Bundling them into output causes conflicts. |
| `Directory.Build.props` for path configuration | Keeps machine-specific paths out of the `.csproj`. Required for any multi-contributor or CI scenario. |

---

## Architecture

```
Jiangyu (modkit)
  |
  +-- Asset replacement layer      (data-driven, no code required — not yet built)
  +-- Declarative patch layer      (JSON/data-driven — not yet built)
  +-- Jiangyu.API.dll              (this project — runtime code layer)
        |
        +-- Tier 3: APIError       ← build first, everything depends on it
        +-- Tier 1: GameType       ← build second
        +-- Tier 1: OffsetCache    ← internal only
        +-- Tier 1: Il2CppUtils    ← internal only
        +-- Tier 1: GameObj
        +-- Tier 1: GameQuery
        +-- Tier 1: GamePatch
        +-- Tier 1: Collections    ← GameList, GameDict, GameArray
        +-- Tier 2: GameState      ← simpler than Tier 1 despite classification
        +-- Tier 2: Templates      ← depends on everything above
        +-- Loader: APILoader      ← MelonMod entry point, wires GameState lifecycle
```

Higher tiers (Tactical, Map, Strategy, AI) are not yet ported. Each requires
field and method name verification against `dump.cs` before porting begins.

---

## Namespace and Naming

| Thing | Name |
|---|---|
| Namespace | `Jiangyu.API` |
| Output DLL | `Jiangyu.API.dll` |
| MelonInfo name string | `"Jiangyu API"` |
| Entry point class | `APILoader` |
| Entry point file | `Loader/APILoader.cs` |
| Error system | `APIError` |
| Error system file | `Core/APIError.cs` |

The MelonInfo name string `"Jiangyu API"` is what modders put in their
`MelonDependency` attribute. It must match exactly.

---

## Project File Essentials

Target framework: `net6.0`
No Roslyn, no MoonSharp, no SharpGLTF. Those are Menace Modkit app dependencies,
not SDK dependencies. The only NuGet package currently needed is `Newtonsoft.Json`
for `ModSettings` (Tier 3, not yet built).

All MelonLoader and game assembly references use `Private>false</Private>`.
Paths are resolved via `Directory.Build.props` one level above the `.csproj`.

---

## APIError Contract

```csharp
// Public — for modders
APIError.Error(string modId, string message, Exception ex = null)
APIError.Warn(string modId, string message, Exception ex = null)
APIError.Info(string modId, string message)
APIError.Fatal(string modId, string message, Exception ex = null)
APIError.ErrorCount        // int, errors + fatals since startup
APIError.ResetCount()      // call on scene load if per-scene tracking needed

// Internal — for Jiangyu.API files only
APIError.ReportInternal(string context, string message, Exception ex = null)
APIError.WarnInternal(string context, string message)
APIError.InfoInternal(string context, string message)
```

Log prefix format: `[ModId:Context] Message` or `[ModId] Message` if no context.
The `Jiangyu.API` mod ID is reserved for internal API output.
All output goes to `Latest.log` via `MelonLogger`. No separate log file.

---

## APILoader Contract

```csharp
[assembly: MelonInfo(typeof(Jiangyu.API.APILoader), "Jiangyu API", "1.0.0", "YourName")]
[assembly: MelonGame("StudioName", "GameName")]

public class APILoader : MelonMod
{
    public override void OnInitializeMelon()   // startup
    public override void OnSceneLoaded(...)    // Unity 6 hook — wires GameState.NotifySceneLoaded
    public override void OnUpdate()            // per-frame — wires GameState.ProcessUpdate
}
```

Note: the game uses Unity 6. MelonLoader for Unity 6 uses
`OnSceneLoaded` instead of `OnSceneWasLoaded`.
The guide currently notes this as unresolved.

The `MelonGame` studio and game name strings must be read from `Latest.log`,
not guessed. MelonLoader prints them on every launch.

---

## Verification Checklist Per File

Before any file is considered done:

- [ ] Namespace is `Jiangyu.API`
- [ ] All references to `"Menace.SDK"` string literals changed to `"Jiangyu.API"`
- [ ] All game-specific names (field names, class names, method names) verified in `dump.cs`
- [ ] Any unverified names are flagged with a `// UNVERIFIED` comment and added to open questions
- [ ] File compiles cleanly in isolation before the next file is started

---

## Open Questions

Items that were identified during design but not yet resolved. Verify and update
this list as each is resolved.

| Question | How to resolve |
|---|---|
| `DataTemplateLoader` exists in `Assembly-CSharp` | Identify that correct loading mechanism is used. |
| What are the exact `MelonGame` studio and game name strings? | Read from `Latest.log` on first launch with MelonLoader installed |
| `m_CachedPtr` is present and named correctly in this Unity 6 build | Unity internal — confirmed in `dump.cs` |
| What is the exact tactical scene name the game uses? | Search `dump.cs` or check build settings. `GameState` currently checks for `"Tactical"` — must match exactly |
| `Il2CppUtils` has no call sites — is it needed? | When a higher-tier file first needs `ToManagedString` or `GetManagedProxy`, wire it then. If no file in Tier 1 or Tier 2 requires it by the time those tiers are complete, remove it. |
| `GameQuery.ClearCache` has no call sites and `FindAllCached` has been removed | Determine whether any future caching in `GameQuery` is planned. If not, remove `ClearCache` entirely. If a narrower cache is introduced later (e.g. for explicit singleton registration), re-evaluate at that point. Do not wire `ClearCache` into `APILoader.OnSceneLoaded` until there is something for it to clear. |
| `GameDict` does not clear the inclusion bar that `GameList` and `GameArray` do — it wraps a less common type, carries two unverified assumptions requiring live runtime probing, and offers no keyed access. | Determine whether any real mod use case requires raw dictionary traversal with no accessible method surface on the owning class. If none is identified, remove `GameDict` and the Collections package becomes `GameList` and `GameArray` only. |
| Which method on `TacticalManager` is the correct patch site for `GameState.NotifyTacticalReady`? The patch site must be a method that fires after all tactical fields and methods are safe to access. `OnTurnStart` firing on the first turn is the current best candidate, but has not been confirmed. | Observe `Latest.log` and game behaviour on first tactical session load. Confirm that when `OnTurnStart` fires for round 1, all `TacticalManager` fields and methods return valid state. Wire `APILoader` postfix accordingly once confirmed. |

| Decision | Rationale |
|---|---|
| `GameType.HasField` removed | Thin public wrapper over `OffsetCache.FindField` that adds no value and invites unverified speculative lookups. Higher tier API files that need field existence checks should call `OffsetCache.FindField` directly with a verified name from `dump.cs`. |
| `Il2CppUtils` is `internal static` in `Jiangyu.API.Internal`, file `Internal/Il2CppUtils.cs` | Utility methods for IL2CPP string conversion and proxy construction are internal infrastructure, not public API surface. Modders use `GameObj.ReadString`, `ToManaged`, and `As<T>`. Moving to `Internal` is consistent with `OffsetCache`. |
| `FindAllCached` removed from `GameQuery` | `GameObj[]` is a snapshot of live Unity instances, not a stable resolution like an IL2CPP class pointer. A scene-scoped cache is only correct for types whose instance count does not change after scene load, but the method provided no way to express or enforce that constraint. Callers who need caching for a known-stable type can hold the result themselves. Correctness before convenience — Principle 8. |

---

## What a Future Agent Should and Should Not Do

**Should:**
- Ask for `dump.cs` output before confirming any game-specific name
- Refer to `PRINCIPLES.md` when a structural decision arises
- Flag unverified assumptions explicitly rather than treating them as facts
- Treat the public API surface as a promise — do not remove or rename public members without flagging it as a breaking change
- Keep suggestions within Jiangyu's stated scope — modkit, not platform

**Should not:**
- Re-introduce the ring buffer, rate limiting, or `OnError` event into `APIError` without a concrete use case that cannot be served by `ErrorCount` alone
- Add Roslyn, MoonSharp, SharpGLTF, or other Menace Modkit app dependencies
- Port higher tiers (Tier 4+) without first verifying every field and method name in `dump.cs`
- Treat Menace SDK source as correct for this game without verification
- Suggest a GUI, registry, or platform feature without noting it conflicts with Principle 8
