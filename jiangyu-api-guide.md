# Jiangyu API — Implementation Guide
## Building Tier 1 and Tier 3 from the Menace API Source

This guide walks you through creating the Jiangyu API from scratch. It assumes you are new to C# modding but can follow instructions carefully. By the end, you will have a working `.dll` that exposes safe, documented IL2CPP access to any modder who references it.

The guide uses the Menace SDK source files as a reference implementation, and Il2CppDumper's `dump.cs` output as the ground truth for verifying game-specific names. Every structural decision is rooted in the Jiangyu principles established in `PRINCIPLES.md`.

---

## Before You Begin

### What You Are Building

The Jiangyu API is a single `.dll` file that MelonLoader loads into the game at startup. Modders reference this `.dll` in their own projects and call its API instead of writing raw IL2CPP access code themselves.

Tier 1 provides safe wrappers around the IL2CPP runtime — finding game types, reading and writing fields, querying live objects, and patching methods. Tier 3 provides the error reporting system that everything else depends on. Together they form the foundation every higher tier is built on.

### What You Are Not Building (Yet)

- A companion desktop app or GUI
- A Lua scripting system
- A C# REPL
- A mod manager or registry
- Any game-specific logic above Tier 2

These are either later-phase additions or out of scope for Jiangyu entirely. The principles in `PRINCIPLES.md` are explicit: be a modkit, not a platform.

### Required Tools

Install these before writing any code:

**Visual Studio 2022 Community** (free)
Download from visualstudio.microsoft.com. During installation, select the `.NET desktop development` workload. This is your code editor and compiler.

**MelonLoader**
Install MelonLoader into your game by running its installer and pointing it at the game's `.exe`. Launch the game once after installation so MelonLoader can generate the `Il2CppAssemblies` folder. Close the game afterwards.

**Il2CppDumper**
Download from github.com/Perfare/Il2CppDumper. Run it against the game's `GameAssembly.dll` and `global-metadata.dat` (both in the game folder). It will produce a `dump.cs` file — a readable listing of every class, field, method, and their exact names as the game compiled them. This is your verification source for all field and method names used in the API.

**dnSpy** (optional but useful)
Download from github.com/dnSpy/dnSpy. Lets you inspect the proxy assemblies MelonLoader generates, which is helpful for verifying that a type exists and seeing what properties it exposes.

### Required Files From the Menace SDK

You need the following source files from the Menace SDK repository. These are your reference implementation — you will port them into Jiangyu:

- `ModError.cs`
- `GameType.cs`
- `GameObj.cs`
- `GameQuery.cs`
- `GamePatch.cs`
- `GameList.cs`, `GameDict.cs`, `GameArray.cs` (may be in a single `Collections.cs`)
- `GameState.cs`
- `Templates.cs`

You will also reconstruct one small file (`Il2CppUtils.cs`) that was not published in the Menace repository but whose contents can be inferred from how it is used.

---

## Part 1 — Project Setup

### 1.1 Create the Project

Open Visual Studio 2022. Select **Create a new project**. Search for **Class Library** and select the C# version (not Visual Basic or F#). Click Next.

Name the project `Jiangyu.API`. Set the framework to `.NET 6.0`. Click Create.

Delete the file `Class1.cs` that Visual Studio generates automatically — you do not need it.

### 1.2 Locate Your Reference DLLs

MelonLoader installs several DLLs that your project needs to reference. Find them at these paths inside your game installation folder:

```
[GameFolder]/MelonLoader/net6/MelonLoader.dll
[GameFolder]/MelonLoader/net6/0Harmony.dll
[GameFolder]/MelonLoader/net6/Il2CppInterop.Runtime.dll
[GameFolder]/MelonLoader/net6/Il2CppInterop.Common.dll
[GameFolder]/MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll
[GameFolder]/MelonLoader/Il2CppAssemblies/UnityEngine.CoreModule.dll
[GameFolder]/MelonLoader/Il2CppAssemblies/Il2Cppmscorlib.dll
[GameFolder]/MelonLoader/Il2CppAssemblies/UnityEngine.IMGUIModule.dll
```

### 1.3 Create the Build Properties File

Rather than hardcoding these paths into your project file (which breaks when anyone else builds the project on a different machine), create a `Directory.Build.props` file in the folder *above* your project folder. This file is picked up automatically by MSBuild.

Create a plain text file named `Directory.Build.props` containing:

```xml
<Project>
  <PropertyGroup>
    <MelonLoaderPath>C:\Path\To\YourGame\MelonLoader\net6</MelonLoaderPath>
    <GameAssembliesPath>C:\Path\To\YourGame\MelonLoader\Il2CppAssemblies</GameAssembliesPath>
  </PropertyGroup>
</Project>
```

Replace the paths with the actual paths to your game installation.

### 1.4 Edit the Project File

Right-click your project in Visual Studio's Solution Explorer and select **Edit Project File**. Replace its contents entirely with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="MelonLoader">
      <HintPath>$(MelonLoaderPath)/MelonLoader.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(MelonLoaderPath)/0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Il2CppInterop.Runtime">
      <HintPath>$(MelonLoaderPath)/Il2CppInterop.Runtime.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Il2CppInterop.Common">
      <HintPath>$(MelonLoaderPath)/Il2CppInterop.Common.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(GameAssembliesPath)/UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Il2Cppmscorlib">
      <HintPath>$(GameAssembliesPath)/Il2Cppmscorlib.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(GameAssembliesPath)/Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.IMGUIModule">
      <HintPath>$(GameAssembliesPath)/UnityEngine.IMGUIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>

</Project>
```

`Private>false</Private>` on every reference tells the compiler "this DLL will already be present at runtime — do not bundle it into my output." This is important. Without it, your API output might try to include MelonLoader itself, which causes conflicts.

`Newtonsoft.Json` is included now because `ModSettings` (Tier 3) uses it for persisting settings to disk. It is the only NuGet package you need for Tiers 1 and 3.

### 1.5 Create Your Folder Structure

Create the following folders inside your project:

```
Jiangyu.API/
  Core/          ← Tier 3: APIError lives here
  Internal/      ← Implementation infrastructure
  Runtime/       ← Tier 1: GameType, GameObj, etc. live here
  Loader/        ← The MelonMod entry point lives here
```

You can create folders by right-clicking the project in Solution Explorer and selecting Add → New Folder.

---

Here is a revised Part 2 that strips the ring buffer and replaces it with a lean wrapper plus error counter:

---

## Part 2 — Tier 3 First: APIError

The single most important rule in the Jiangyu build order: **write `APIError` before any other file.** Every Tier 1 file routes its failures through `APIError`. If it does not exist, nothing else will compile.

This is a direct expression of **Principle 2 — Correctness Before Convenience**. The error reporting contract must be solid before any code that depends on it is written.

### 2.1 Do Not Port ModError.cs Directly

The Menace SDK's `ModError.cs` includes a queryable ring buffer, rate limiting, deduplication, and an `OnError` event system. These exist to feed the Menace DevConsole — a feature Jiangyu is not building. Carrying that complexity into Jiangyu would violate Principle 8 by importing infrastructure for a platform feature that does not exist.

Instead, create `APIError.cs` from scratch. It is a simpler, honest implementation of what Jiangyu actually needs: a consistent error reporting surface that writes to `Latest.log` via MelonLoader and tracks a running error count for any future in-game notification.

### 2.2 Create APIError.cs

Create a new file at `Core/APIError.cs` with the following content:

```csharp
using System;
using MelonLoader;

namespace Jiangyu.API;

public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// Central error reporting for Jiangyu API. Writes all output to MelonLoader's
/// Latest.log. Never throws. Tracks a running error count for in-game notification.
/// </summary>
public static class APIError
{
    private static int _errorCount = 0;

    /// <summary>
    /// Total number of errors and fatals reported since startup.
    /// Suitable for driving a lightweight in-game notification.
    /// </summary>
    public static int ErrorCount => _errorCount;

    /// <summary>
    /// Reset the error count. Call when entering a new scene
    /// if you want per-scene error tracking.
    /// </summary>
    public static void ResetCount() => _errorCount = 0;

    // --- Public API for modders ---

    public static void Error(string modId, string message, Exception ex = null)
        => Write(modId, null, message, ErrorSeverity.Error, ex);

    public static void Warn(string modId, string message, Exception ex = null)
        => Write(modId, null, message, ErrorSeverity.Warning, ex);

    public static void Info(string modId, string message)
        => Write(modId, null, message, ErrorSeverity.Info, null);

    public static void Fatal(string modId, string message, Exception ex = null)
        => Write(modId, null, message, ErrorSeverity.Fatal, ex);

    // --- Internal API for Jiangyu.API itself ---

    internal static void ReportInternal(string context, string message, Exception ex = null)
        => Write("Jiangyu.API", context, message, ErrorSeverity.Error, ex);

    internal static void WarnInternal(string context, string message)
        => Write("Jiangyu.API", context, message, ErrorSeverity.Warning, null);

    internal static void InfoInternal(string context, string message)
        => Write("Jiangyu.API", context, message, ErrorSeverity.Info, null);

    // --- Core writer ---

    private static void Write(string modId, string context, string message,
        ErrorSeverity severity, Exception ex)
    {
        try
        {
            var prefix = string.IsNullOrEmpty(context)
                ? $"[{modId}]"
                : $"[{modId}:{context}]";

            switch (severity)
            {
                case ErrorSeverity.Info:
                    MelonLogger.Msg($"{prefix} {message}");
                    break;
                case ErrorSeverity.Warning:
                    MelonLogger.Warning($"{prefix} {message}");
                    break;
                case ErrorSeverity.Error:
                case ErrorSeverity.Fatal:
                    MelonLogger.Error($"{prefix} {message}");
                    if (ex != null)
                        MelonLogger.Error($"{prefix} {ex}");
                    _errorCount++;
                    break;
            }
        }
        catch
        {
            // Never crash from error reporting
        }
    }
}
```

### 2.3 What Was Removed and Why

Compared to the Menace `ModError.cs`, three systems were deliberately dropped:

**The ring buffer** — `_entries`, `GetErrors()`, `RecentErrors`, and the `OnError` event. These exist to feed a DevConsole panel at runtime. Jiangyu has no DevConsole. The data was being collected with nowhere to go.

**Rate limiting and deduplication** — the token bucket and five-second deduplication window. These are valuable when a broken `OnUpdate` loop can flood an in-game console with thousands of entries per second. Since Jiangyu's only output is `Latest.log`, MelonLoader's own file writing is the natural throttle. If a broken loop produces ten thousand log lines, that is useful diagnostic information rather than noise to suppress.

**The `Flags` enum** — `ErrorSeverity` in Menace was a flags enum to support filtering in the DevConsole. Without filtering, a plain enum is correct.

### 2.4 What Was Kept and Why

**The internal vs public method split** — `ReportInternal`, `WarnInternal`, and `InfoInternal` remain internal. This keeps the `[Jiangyu.API:context]` prefix reserved for the API's own output, so a modder reading the log can immediately tell whether an error came from their code or from the API itself.

**The prefix format** — `[ModId:Context] Message` is kept exactly as Menace defined it. It is filterable, specific, and any modder already familiar with Menace's logging will recognise it immediately.

**The error count** — a single integer is enough to drive a future lightweight notification ("N errors — check Latest.log") without the overhead of storing every entry. It costs nothing to carry.

**The outer try/catch in `Write`** — error reporting must never itself be the source of a crash. Even if `MelonLogger` somehow throws, the game continues.

### 2.5 How Modders Use It

```csharp
// Reporting an error from mod code
APIError.Error("MyWeaponMod", "WeaponTemplate 'Sword' not found");

// With an exception
try { ... }
catch (Exception ex)
{
    APIError.Error("MyWeaponMod", "Failed to read damage field", ex);
}

// Informational
APIError.Info("MyWeaponMod", "Weapon templates loaded successfully");
```

All output goes to `Latest.log`. Nothing else required.

### 2.6 Verify It Compiles

Build the project now. `APIError.cs` has no dependencies except `MelonLoader` and standard .NET — it should compile cleanly in isolation. Do not proceed to Tier 1 until this succeeds.

---

## Part 3 — Tier 1: The IL2CPP Foundation

Tier 1 is the layer that translates between the C# world your mod code lives in and the IL2CPP compiled binary the game is. It consists of seven files. Port them in the order given — each one depends on the ones before it.

### A Note on Verification

Every field name, method name, and class name that your Tier 1 code references by string must be verified against `dump.cs` before you treat it as correct. This is **Principle 2** and **Principle 7** working together: the dump is your ground truth for what the game actually compiled, and you should prefer explicit verified names over guesses.

When you see a string like `"m_CachedPtr"` or `"_items"` in the Menace SDK source, search for that exact string in `dump.cs` before using it. If it appears there, the contract is verified. If it does not appear, note it — it may be a Unity internal name (safe to trust) or it may need investigation.

Unity internal field names such as `m_CachedPtr` (the pointer Unity uses to track whether a native object is still alive) are part of the Unity runtime and will not appear in your game's `dump.cs`. They are safe to use as-is.

### 3.1 Port GameType.cs

Create `Runtime/GameType.cs`. Copy from the Menace source. Change the namespace to `Jiangyu.API`.

`GameType` wraps an IL2CPP class pointer. Its job is to resolve a type by name, cache the result so the lookup only happens once, and provide field offset resolution. When you see calls like `GameType.Find("WeaponTemplate")` in mod code, this is the file that handles them.

No game-specific strings live in this file — it works with whatever type name it is given. No further changes beyond the namespace are needed.

### 3.2 Port OffsetCache.cs
Create `Runtime/OffsetCache.cs` and copy the Menace SDK `OffsetCache.cs` in its entiery. Change the namespace to `Jiangyu.API.Internal` and `ModError` to `APIError`. Make sure that `Initialize()` is called from `APILoader` in section 6.1.

### 3.3 Port GameObj.cs

Create `Runtime/GameObj.cs`. Copy from Menace source. Change namespace.

`GameObj` is a safe handle for a live IL2CPP object — the type modders will interact with most. It wraps a native pointer and provides typed read/write methods for common field types (`int`, `float`, `bool`, `string`). Every read checks `IsAlive` first. Every failure returns a safe default and routes to `APIError`.

The `IsAlive` check uses `m_CachedPtr` — a Unity internal field. This does not appear in `dump.cs` and does not need to. It is a Unity runtime contract, not a game-specific one.

### 3.4 Port GameQuery.cs

Create `Runtime/GameQuery.cs`. Copy from Menace source. Change namespace.

`GameQuery` provides static helpers for `FindObjectsOfTypeAll` — Unity's mechanism for finding all live instances of a type. Modders use this to locate game objects without needing to know how the game stores them internally.

### 3.5 Port GamePatch.cs

Create `Runtime/GamePatch.cs`. Copy from Menace source. Change namespace.

`GamePatch` wraps HarmonyLib patching behind a safe interface. Where raw Harmony requires 12–15 lines per patch and throws on failure, `GamePatch.Prefix(...)` and `GamePatch.Postfix(...)` resolve the target method, apply the patch, and return `false` on failure instead of throwing.

### 3.6 Port Collections

Create `Runtime/Collections.cs`. Copy from Menace source (this may be a single file or three separate files — `GameList.cs`, `GameDict.cs`, `GameArray.cs`). Change namespace.

These three types wrap IL2CPP collection types that the game uses internally:

- `GameList` — wraps `IL2CPP List<T>`, exposing `Count`, an indexer, and `foreach` enumeration
- `GameDict` — wraps `IL2CPP Dictionary<K,V>`, iterating entries and skipping deleted slots
- `GameArray` — wraps IL2CPP native arrays, providing typed element access

The internal field names these types reference (`_items`, `_entries`, `_size`) are standard Unity/Mono collection internals. Verify them in `dump.cs` if you want to be certain, but they are extremely unlikely to differ.

### 3.7 Build and Verify Tier 1

Build the project again. All six Tier 1 files plus `APIError` should compile cleanly.

If you see errors referencing missing types, the most common cause is a missing `using` directive at the top of a file. The Menace source files use `namespace Menace.SDK;` as a file-scoped namespace declaration, which means all types in other files under the same namespace are automatically visible. Since you have changed the namespace to `Jiangyu.API` across all files, this should continue to work — but double-check that every file's namespace declaration matches exactly.

---

## Part 4 — Tier 1 Continued: GameState

`GameState` is technically Tier 2 in the Menace SDK's classification, but its dependency footprint is simpler than most of Tier 1. It only touches `APIError` and standard .NET types — no IL2CPP calls, no Unity APIs beyond `AppDomain`. Port it now so it is available when `Templates` needs it.

### 4.1 Port GameState.cs

Create `Runtime/GameState.cs`. Copy from Menace source. Change namespace.

`GameState` provides scene awareness and deferred execution. Its key responsibilities are:

- Tracking the current scene name and firing events when scenes change
- Firing `TacticalReady` 30 frames after the tactical scene loads, giving the game time to finish initialising before mods interact with it
- `RunDelayed(frames, callback)` — schedules a callback N frames in the future
- `RunWhen(condition, callback)` — polls a predicate each frame and fires when it becomes true

Notice that `GameState` uses the scene name `"Tactical"` as a string. Search your `dump.cs` for this name or look in the game's build settings to verify it matches your game's actual scene name. If the tactical scene is named differently, update the string. This is **Principle 7** in practice: prefer explicit verified names.

### 4.2 Wire GameState Into the Loader

`GameState` exposes two `internal` methods — `NotifySceneLoaded` and `ProcessUpdate` — that must be called by your MelonMod entry point. You will create that entry point in Part 6. Make a note of these two method calls now so you remember to add them.

---

## Part 5 — Tier 2: Templates

With the full Tier 1 foundation in place, `Templates` can now be ported.

### 5.1 A Principle 2 Checkpoint: DataTemplateLoader

Before porting `Templates.cs`, there is one verification step required by **Principle 2 — Correctness Before Convenience**.

Open `Templates.cs` from the Menace SDK and find the `EnsureTemplatesLoaded` method. It searches for a class named `"DataTemplateLoader"` in `Assembly-CSharp` at runtime:

```csharp
var loaderType = gameAssembly.GetTypes()
    .FirstOrDefault(t => t.Name == "DataTemplateLoader");
```

Open your `dump.cs` and search for `DataTemplateLoader`. If it appears, the contract is verified — this class exists in your game and the name is correct. Note its full name and namespace in a comment above the method.

If it does not appear, the game uses a different mechanism to load templates. In that case, `EnsureTemplatesLoaded` will silently do nothing (it handles the null gracefully), and `Templates.Find` will still work for types that are already loaded into memory — but templates may not be found until something in the game has loaded them first. Document this clearly if it applies to your game.

This verification step is not optional. Baking an unverified string lookup into the foundation is exactly the kind of assumption **Principle 2** exists to prevent.

### 5.2 Port Templates.cs

Create `Runtime/Templates.cs`. Copy from Menace source. Change namespace.

**Remove the localization block.** Lines from the comment `// Localization Field Helpers` to the end of the file (the `GetLocalizationKey`, `GetLocalizationTable`, `GetLocalizedText`, `GetAllLocalizedTexts`, `GetLocalizationInfo`, `SetLocalizationKey`, and `IsLocalizationField` methods) reference `MultiLingualLocalization`, which is a separate SDK file not being ported in this phase. Delete these methods entirely. They can be added later when localization support is needed.

**Note the Clone limitation.** The `Clone` method's comment states explicitly that it does not register the cloned template in `DataTemplateLoader`. In Jiangyu's model, template cloning is intended to be a data-driven operation handled by a verified loader path — not a raw in-memory operation that modders call directly. For now the method is present and functional for runtime use, but it should be documented clearly:

```csharp
/// <summary>
/// Clone an existing template via UnityEngine.Object.Instantiate.
/// NOTE: The clone is not registered in DataTemplateLoader and will not
/// be findable by name through the game's template registry. For
/// data-driven template cloning, use Jiangyu's patch layer instead.
/// </summary>
```

This is **Principle 6 — Keep Layers Honest**. The runtime code layer and the data patch layer are distinct, and the boundary should be visible in the documentation.

---

## Part 6 — The Entry Point

### 6.1 Create APILoader.cs

Create `Loader/APILoader.cs`. This is the class MelonLoader recognises as a mod and calls into at startup:

```csharp
using MelonLoader;
using Jiangyu.API;

[assembly: MelonInfo(typeof(Jiangyu.API.APILoader), "Jiangyu API", "1.0.0", "YourName")]
[assembly: MelonGame("StudioName", "GameName")]

namespace Jiangyu.API;

public class APILoader : MelonMod
{
    public override void OnInitializeMelon()
    {
        OffsetCache.Initialize();
        LoggerInstance.Msg("Jiangyu API initialised.");
    }

    public override void OnSceneLoaded(int buildIndex, string sceneName)
    {
        GameState.NotifySceneLoaded(sceneName);
    }

    public override void OnUpdate()
    {
        GameState.ProcessUpdate();
    }
}
```

**The `MelonGame` attribute** requires the exact studio name and game name strings that MelonLoader expects. Find these by launching the game with MelonLoader installed and checking `MelonLoader/Latest.log` — the game identity strings are printed near the top of the log on every launch.

**`OnUpdate`** is called every frame by MelonLoader. Wiring it to `GameState.ProcessUpdate()` is what makes `RunDelayed` and `RunWhen` actually tick. Without this, any mod that calls `GameState.RunDelayed(...)` will schedule a callback that never fires.

### 6.2 Final Build

Build the project. The output should be a single file: `Jiangyu.API.dll`.

---

## Part 7 — Testing

### 7.1 Deploy the API

Copy `Jiangyu.API.dll` to your game's `Mods/` folder. Launch the game. Open `MelonLoader/Latest.log` and verify you see:

```
[Jiangyu API] Jiangyu API initialised.
```

If the line does not appear, check that the `MelonGame` attribute matches your game's identity strings exactly — MelonLoader will refuse to load a mod whose game attribute does not match.

### 7.2 Write a Smoke Test Mod

Create a second, separate Visual Studio project — a new Class Library also targeting `.NET 6.0`. This is a test mod, not part of the API itself. Add a reference to `Jiangyu.API.dll` (and to `MelonLoader.dll`, `0Harmony.dll`, and the game assemblies, all with `Private>false</Private>`).

Write the smallest possible test:

```csharp
using MelonLoader;
using Jiangyu.API;

[assembly: MelonInfo(typeof(SmokeTest.SmokeTestMod), "API Smoke Test", "1.0.0", "YourName")]
[assembly: MelonGame("StudioName", "GameName")]

namespace SmokeTest;

public class SmokeTestMod : MelonMod
{
    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        // Pick any class name you can see in dump.cs
        var testType = GameType.Find("SomeClassName");
        if (testType.IsValid)
            LoggerInstance.Msg($"GameType.Find success: {testType.FullName}");
        else
            LoggerInstance.Warning("GameType.Find returned invalid — check the class name");
    }
}
```

Replace `"SomeClassName"` with a class name you can see in `dump.cs` — any common one will do. Build, copy the test mod's DLL to `Mods/`, launch the game, and check the log for your message.

A valid `GameType` returned means Tier 1 is working. If you get a warning, double-check the class name against `dump.cs` — capitalisation must match exactly.

### 7.3 Test APIError

Add this to your smoke test to verify the error system:

```csharp
APIError.Info("SmokeTest", "APIError routing confirmed");
APIError.Warn("SmokeTest", "This is a test warning");
```

Both should appear in `Latest.log` with the `[SmokeTest]` prefix. If they do, Tier 3 is working.

---

## Part 8 — What Comes Next

With Tier 1 and Tier 3 verified, the foundation is complete. The files you have built provide everything needed for a modder to find game types, read and write fields on live objects, safely patch methods, and receive structured error reporting.

### The Tier 2 Handoff

`Templates` and `GameState` are already ported. They become fully useful once you have verified the `DataTemplateLoader` contract (Part 5.1) and confirmed that `GameState`'s scene names match your game.

### What Jiangyu Adds Above This

The Menace SDK's higher tiers (Tactical, Map, Strategy, AI) are game-specific wrappers. Before porting any of them, each field and method name they reference should be verified against `dump.cs`. The process is the same as Part 5.1 — find the string in the source, search `dump.cs`, note the result, proceed only when confirmed.

The layers below the SDK — asset replacement and declarative template patching — are not part of the SDK at all. They are separate Jiangyu components that sit underneath it and do not require C# from the modder. Those are designed and built independently.

### Adding a Modder to the API

When a modder wants to use Jiangyu.API, they:

1. Add `Jiangyu.API.dll` as a reference in their project with `Private>false</Private>`
2. Add `using Jiangyu.API;` at the top of their files
3. Place `Jiangyu.API.dll` in the game's `Mods/` folder alongside their own mod DLL

The API is loaded once by MelonLoader at startup. All mods that reference it share the same loaded instance.

---

## Appendix: Porting Checklist

Use this to verify each file before marking it done.

| File | Namespace changed | Internal strings updated | Game-specific names verified in dump.cs | Compiles cleanly |
|---|---|---|---|---|
| `APIError.cs` | [x] | [x] | n/a | [x] |
| `GameType.cs` | [x] | n/a | [x] | ☐ |
| `OffsetCache.cs` | [x] | [x] | [x] | ☐ |
| `GameObj.cs` | [x] | n/a | [x] | ☐ |
| `GameQuery.cs` | [x] | n/a | [x] | ☐ |
| `GamePatch.cs` | [x] | n/a | [x] | [x] |
| `Collections.cs` | [x] | n/a | [x] | [x] |
| `GameState.cs` | ☐ | n/a | ☐ | ☐ |
| `Templates.cs` | ☐ | n/a | ☐ (DataTemplateLoader) | ☐ |
| `APILoader.cs` | ☐ | n/a | ☐ (MelonGame strings) | ☐ |

## Appendix: Key Principles Reference

| Principle | How It Applies to This Build |
|---|---|
| 1 — Simple things stay simple | The API is only the code layer. Asset replacement and template patching below it must not require C#. |
| 2 — Correctness before convenience | Verify every game-specific name in dump.cs before treating it as correct. Never ship an assumption as foundation truth. |
| 3 — Derive from known-good contracts | `Templates.Clone` wraps Unity's `Instantiate` — a known-good operation. It does not invent new game structures. |
| 4 — Data first, code when necessary | The API is only needed for runtime behaviour. Simpler mod tasks belong in layers below it. |
| 6 — Keep layers honest | The Clone limitation is documented. Runtime truth and data-layer truth are not conflated. |
| 7 — Explicit contracts over guessing | `DataTemplateLoader` is verified against dump.cs before being trusted. Scene names are verified. MelonGame strings are read from the log, not guessed. |
| 8 — Be a modkit, not a platform | No REPL, no Lua, no registry, no GUI. Those are later problems or out of scope. |
