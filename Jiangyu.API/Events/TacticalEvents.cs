using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Jiangyu.API.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Jiangyu.API;

/// <summary>
/// Harmony hooks for TacticalManager events that fire Lua callbacks.
///
/// Hooks all InvokeOnX methods in TacticalManager to provide a comprehensive
/// event system for both C# plugins and Lua scripts.
///
/// C# Usage:
///   TacticalEventHooks.OnActorKilled += (actor, killer, faction) => { ... };
///   TacticalEventHooks.OnDamageReceived += (target, attacker, skill) => { ... };
///
/// Lua Usage:
///   on("actor_killed", function(data) log(data.actor .. " killed by " .. data.killer) end)
///   on("damage_received", function(data) log(data.target .. " took damage") end)
/// </summary>
public static class TacticalEvents
{
    private static bool _initialized;
    // Combat Events
    public static event Action<IntPtr, IntPtr, int> OnActorKilled;           // actor, killer, killerFaction
    public static event Action<IntPtr, IntPtr, IntPtr> OnDamageReceived;     // entity, attacker, skill
    public static event Action<IntPtr, IntPtr, IntPtr> OnAttackMissed;       // entity, attacker, skill
    public static event Action<IntPtr, IntPtr> OnAttackTileStart;            // attacker, tile
    public static event Action<IntPtr, int> OnBleedingOut;                   // leader, remainingRounds
    public static event Action<IntPtr, IntPtr> OnStabilized;                 // leader, savior
    public static event Action<IntPtr> OnSuppressed;                         // actor
    public static event Action<IntPtr, float, IntPtr> OnSuppressionApplied;  // actor, change, suppressor

    // Actor State Events
    public static event Action<IntPtr> OnActorStateChanged;                  // actor
    public static event Action<IntPtr, int> OnMoraleStateChanged;            // actor, moraleState
    public static event Action<IntPtr, float> OnHitpointsChanged;            // entity, hitpointsPct
    public static event Action<IntPtr, float, int> OnArmorChanged;           // entity, armorDurability, armor
    public static event Action<IntPtr, int, int> OnActionPointsChanged;      // actor, oldAp, newAp

    // Visibility Events
    public static event Action<IntPtr, IntPtr> OnDiscovered;                 // entity, discoverer
    public static event Action<IntPtr> OnVisibleToPlayer;                    // actor
    public static event Action<IntPtr> OnHiddenToPlayer;                     // actor

    // Movement Events
    public static event Action<IntPtr, IntPtr, IntPtr> OnMovementStarted;    // actor, fromTile, toTile
    public static event Action<IntPtr, IntPtr> OnMovementFinished;                   // actor, tile

    // Skill Events
    public static event Action<IntPtr, IntPtr, IntPtr> OnSkillUsed;          // actor, skill, targetTile
    public static event Action<IntPtr> OnSkillCompleted;                     // skill
    public static event Action<IntPtr, IntPtr> OnSkillAdded;                 // receiver, skill

    // Offmap Events
    public static event Action<IntPtr, IntPtr> OnOffmapAbilityUsed;          // ability, targetTile
    public static event Action<IntPtr> OnOffmapAbilityCanceled;              // ability
    public static event Action OnOffmapAbilityUpdateUsability;               // no args

    // Turn/Round Events
    public static event Action<IntPtr> OnActiveActorChanged;                 // actor
    public static event Action<IntPtr> OnTurnStart;                          // actor
    public static event Action<IntPtr> OnTurnEnd;                            // actor
    public static event Action OnPlayerTurn;                                 // no args
    public static event Action OnAITurn;                                     // no args
    public static event Action<int> OnRoundEnd;                              // roundNumber
    public static event Action<int> OnRoundStart;                            // roundNumber

    // Mission Events
    public static event Action<IntPtr, int, int> OnObjectiveStateChanged;    // objective, oldState, newState
    public static event Action<IntPtr> OnEntitySpawned;                      // entity
    public static event Action<IntPtr> OnElementDeath;                       // element
    public static event Action<IntPtr> OnElementMalfunction;                 // element
    public static event Action OnPreFinished;                                // no args
    public static event Action OnFinished;                                   // no args

    /// <summary>
    /// Initialize tactical event hooks. Call from ModpackLoaderMod after game assembly is loaded.
    /// </summary>
    public static void Initialize(HarmonyLib.Harmony harmony)
    {
        if (_initialized) return;

        try
        {
            const string tacticalManager = "Il2CppMenace.Tactical.TacticalManager";
            var hooks = typeof(TacticalEvents);
            var flags = BindingFlags.Static | BindingFlags.NonPublic;

            int patchCount = 0;

            // Combat Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnDeath", hooks.GetMethod(nameof(OnActorKilled_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnDamageReceived", hooks.GetMethod(nameof(OnDamageReceived_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnAttackMissed", hooks.GetMethod(nameof(OnAttackMissed_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnAttackTileStart", hooks.GetMethod(nameof(OnAttackTileStart_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnBleedingOut", hooks.GetMethod(nameof(OnBleedingOut_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnStabilized", hooks.GetMethod(nameof(OnStabilized_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnSuppressed", hooks.GetMethod(nameof(OnSuppressed_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnSuppressionApplied", hooks.GetMethod(nameof(OnSuppressionApplied_Postfix), flags)) ? 1 : 0;

            // Actor State Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnActorStateChanged", hooks.GetMethod(nameof(OnActorStateChanged_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnMoraleStateChanged", hooks.GetMethod(nameof(OnMoraleStateChanged_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnHitpointsChanged", hooks.GetMethod(nameof(OnHitpointsChanged_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnArmorChanged", hooks.GetMethod(nameof(OnArmorChanged_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnActionPointsChanged", hooks.GetMethod(nameof(OnActionPointsChanged_Postfix), flags)) ? 1 : 0;

            // Visibility Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnDiscovered", hooks.GetMethod(nameof(OnDiscovered_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnVisibleToPlayer", hooks.GetMethod(nameof(OnVisibleToPlayer_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnHiddenToPlayer", hooks.GetMethod(nameof(OnHiddenToPlayer_Postfix), flags)) ? 1 : 0;

            // Movement Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnMovement", hooks.GetMethod(nameof(OnMovementStarted_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnMovementFinished", hooks.GetMethod(nameof(OnMovementFinished_Postfix), flags)) ? 1 : 0;

            // Skill Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnSkillUse", hooks.GetMethod(nameof(OnSkillUsed_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnAfterSkillUse", hooks.GetMethod(nameof(OnSkillCompleted_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnSkillAdded", hooks.GetMethod(nameof(OnSkillAdded_Postfix), flags)) ? 1 : 0;

            // Offmap Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnOffmapAbilityUsed", hooks.GetMethod(nameof(OnOffmapAbilityUsed_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnOffmapAbilityCanceled", hooks.GetMethod(nameof(OnOffmapAbilityCanceled_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnOffmapAbilityRefreshUsability", hooks.GetMethod(nameof(OnOffmapAbilityUpdateUsability_Postfix), flags)) ? 1 : 0;

            // Turn/Round Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "SetActiveActor", hooks.GetMethod(nameof(OnActiveActorChanged_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnTurnEnd", hooks.GetMethod(nameof(OnTurnEnd_Postfix), flags)) ? 1 : 0;
            //patchCount += GamePatch.Prefix(harmony, tacticalManager, "NextRound", hooks.GetMethod(nameof(OnRoundEnd_Prefix), flags)) ? 1 : 0; // Commented out pending GameMethod addition to API
            //patchCount += GamePatch.Postfix(harmony, tacticalManager, "NextRound", hooks.GetMethod(nameof(OnRoundStart_Postfix), flags)) ? 1 : 0; // Commented out pending GameMethod additon to API

            // Mission Events
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnObjectiveStateChanged", hooks.GetMethod(nameof(OnObjectiveStateChanged_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnEntitySpawned", hooks.GetMethod(nameof(OnEntitySpawned_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnElementDeath", hooks.GetMethod(nameof(OnElementDeath_Postfix), flags)) ? 1 : 0;
            patchCount += GamePatch.Postfix(harmony, tacticalManager, "InvokeOnElementMalfunction", hooks.GetMethod(nameof(OnElementMalfunction_Postfix), flags)) ? 1 : 0;

            _initialized = true;
            APILogger.InfoInternal("TacticalEvents", $"Initialized with {patchCount} event hooks");
        }
        catch (Exception ex)
        {
            APILogger.ReportInternal("TacticalEvents", "Failed to initialize", ex);
        }
    }

    // --- Combat Events ---

    private static void OnActorKilled_Postfix(object __instance, object _target, object _killer, int _killerFaction)
    {
        OnActorKilled?.Invoke(Il2CppUtils.GetPointer(_target), Il2CppUtils.GetPointer(_killer), _killerFaction);
    }

    private static void OnDamageReceived_Postfix(object __instance, object _entity, object _attacker, object _skill, object _damageInfo)
    {
        OnDamageReceived?.Invoke(Il2CppUtils.GetPointer(_entity), Il2CppUtils.GetPointer(_attacker), Il2CppUtils.GetPointer(_skill));
    }

    private static void OnAttackMissed_Postfix(object __instance, object _entity, object _attacker, object _skill)
    {
        OnAttackMissed?.Invoke(Il2CppUtils.GetPointer(_entity), Il2CppUtils.GetPointer(_attacker), Il2CppUtils.GetPointer(_skill));
    }

    private static void OnAttackTileStart_Postfix(object __instance, object _actor, object _skill, object _targetTile, float _attackDurationInSec)
    {
        OnAttackTileStart?.Invoke(Il2CppUtils.GetPointer(_actor), Il2CppUtils.GetPointer(_targetTile));
    }

    private static void OnBleedingOut_Postfix(object __instance, object _leader, int _remainingRounds)
    {
        OnBleedingOut?.Invoke(Il2CppUtils.GetPointer(_leader), _remainingRounds);
    }

    private static void OnStabilized_Postfix(object __instance, object _leader, object _savior)
    {
        OnStabilized?.Invoke(Il2CppUtils.GetPointer(_leader), Il2CppUtils.GetPointer(_savior));
    }

    private static void OnSuppressed_Postfix(object __instance, object _actor)
    {
        OnSuppressed?.Invoke(Il2CppUtils.GetPointer(_actor));
    }

    private static void OnSuppressionApplied_Postfix(object __instance, object _actor, float _change, object _suppressor)
    {
        OnSuppressionApplied?.Invoke(Il2CppUtils.GetPointer(_actor), _change, Il2CppUtils.GetPointer(_suppressor));
    }
    // --- Actor State Events ---

    private static void OnActorStateChanged_Postfix(object __instance, object _actor, object _oldState, object _newState)
    {
        OnActorStateChanged?.Invoke(Il2CppUtils.GetPointer(_actor));
    }

    private static void OnMoraleStateChanged_Postfix(object __instance, object _actor, object _moraleState)
    {
        OnMoraleStateChanged?.Invoke(Il2CppUtils.GetPointer(_actor), (int)_moraleState);
    }

    private static void OnHitpointsChanged_Postfix(object __instance, object _entity, float _hitpointsPct, int _animationDurationInMs)
    {
        OnHitpointsChanged?.Invoke(Il2CppUtils.GetPointer(_entity), _hitpointsPct);
    }

    private static void OnArmorChanged_Postfix(object __instance, object _entity, float _armorDurability, int _armor, int _animationDurationInMs)
    {
        OnArmorChanged?.Invoke(Il2CppUtils.GetPointer(_entity), _armorDurability, _armor);
    }

    private static void OnActionPointsChanged_Postfix(object __instance, object _actor, int _oldAP, int _newAP)
    {
        OnActionPointsChanged?.Invoke(Il2CppUtils.GetPointer(_actor), _oldAP, _newAP);
    }

    // --- Visibility Events ---

    private static void OnDiscovered_Postfix(object __instance, object _entity, object _discoverer)
    {
        OnDiscovered?.Invoke(Il2CppUtils.GetPointer(_entity), Il2CppUtils.GetPointer(_discoverer));
    }

    private static void OnVisibleToPlayer_Postfix(object __instance, object _actor)
    {
        OnVisibleToPlayer?.Invoke(Il2CppUtils.GetPointer(_actor));
    }

    private static void OnHiddenToPlayer_Postfix(object __instance, object _actor)
    {
        OnHiddenToPlayer?.Invoke(Il2CppUtils.GetPointer(_actor));
    }

    // --- Movement Events ---

    private static void OnMovementStarted_Postfix(object __instance, object _actor, object _from, object _to, object _action, object _container)
    {
        OnMovementStarted?.Invoke(Il2CppUtils.GetPointer(_actor), Il2CppUtils.GetPointer(_from), Il2CppUtils.GetPointer(_to));
    }

    private static void OnMovementFinished_Postfix(object __instance, object _actor, object _to)
    {
        OnMovementFinished?.Invoke(Il2CppUtils.GetPointer(_actor), Il2CppUtils.GetPointer(_to));
    }

    // --- Skill Events ---

    private static void OnSkillUsed_Postfix(object __instance, object _actor, object _skill, object _targetTile)
    {
        OnSkillUsed?.Invoke(Il2CppUtils.GetPointer(_actor), Il2CppUtils.GetPointer(_skill), Il2CppUtils.GetPointer(_targetTile));
    }

    private static void OnSkillCompleted_Postfix(object __instance, object _skill)
    {
        OnSkillCompleted?.Invoke(Il2CppUtils.GetPointer(_skill));
    }

    private static void OnSkillAdded_Postfix(object __instance, object _receiver, object _skill, object _source, bool _success)
    {
        OnSkillAdded?.Invoke(Il2CppUtils.GetPointer(_receiver), Il2CppUtils.GetPointer(_skill));
    }

    // --- Offmap Events ---

    private static void OnOffmapAbilityUsed_Postfix(object __instance, object _offmapAbility, object _targetTile)
    {
        OnOffmapAbilityUsed?.Invoke(Il2CppUtils.GetPointer(_offmapAbility), Il2CppUtils.GetPointer(_targetTile));
    }

    private static void OnOffmapAbilityCanceled_Postfix(object __instance, object _offmapAbility)
    {
        OnOffmapAbilityCanceled?.Invoke(Il2CppUtils.GetPointer(_offmapAbility));
    }

    private static void OnOffmapAbilityUpdateUsability_Postfix(object __instance)
    {
        OnOffmapAbilityUpdateUsability?.Invoke();
    }

    // --- Turn/Round Events ---

    private static void OnActiveActorChanged_Postfix(object __instance, object _actor, bool _endTurn)
    {
        var actorPtr = Il2CppUtils.GetPointer(_actor);
        if (actorPtr == IntPtr.Zero) return;

        OnActiveActorChanged?.Invoke(actorPtr);

        if (_endTurn)
        {
            OnTurnStart?.Invoke(actorPtr);

            // Derive player/AI turn from game state
            try
            {
                // UNVERIFIED: TacticalManager.Get() and IsPlayerTurn() - verify against dump.cs
                var tm = typeof(Il2CppMenace.Tactical.TacticalManager);
                var get = tm.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                var instance = get?.Invoke(null, null);
                var isPlayerTurn = (bool?)tm.GetMethod("IsPlayerTurn",
                    BindingFlags.Public | BindingFlags.Instance)?.Invoke(instance, null);

                if (isPlayerTurn == true)
                    OnPlayerTurn?.Invoke();
                else
                    OnAITurn?.Invoke();
            }
            catch { }
        }
    }

    private static void OnTurnEnd_Postfix(object __instance, object _actor)
    {
        OnTurnEnd?.Invoke(Il2CppUtils.GetPointer(_actor));
    }

    /* Commented out pending addition of GameMethod class to handle method lookup
    private static void OnRoundEnd_Prefix(object __instance)
    {
        OnRoundEnd?.Invoke(GetCurrentRound());
    }

    private static void OnRoundStart_Postfix(object __instance)
    {
        OnRoundStart?.Invoke(GetCurrentRound());
    }
    */

    // --- Mission Events ---

    private static void OnObjectiveStateChanged_Postfix(object __instance, object _objective, object _oldState, object _newState)
    {
        OnObjectiveStateChanged?.Invoke(Il2CppUtils.GetPointer(_objective), (int)_oldState, (int)_newState);
    }

    private static void OnEntitySpawned_Postfix(object __instance, object _entity)
    {
        OnEntitySpawned?.Invoke(Il2CppUtils.GetPointer(_entity));
    }

    private static void OnElementDeath_Postfix(object __instance, object _entity, object _element, object _attacker, object _damageInfo)
    {
        OnElementDeath?.Invoke(Il2CppUtils.GetPointer(_element));
    }

    private static void OnElementMalfunction_Postfix(object __instance, object _element, object _skill)
    {
        OnElementMalfunction?.Invoke(Il2CppUtils.GetPointer(_element));
    }

    private static void OnPreFinished_Postfix(object __instance, object _finishReason)
    {
        OnPreFinished?.Invoke();
    }

    private static void OnFinished_Postfix(object __instance, object _finishReason)
    {
        OnFinished?.Invoke();
    }
}