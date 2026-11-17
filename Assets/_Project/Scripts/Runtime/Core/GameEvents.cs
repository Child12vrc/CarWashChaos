using System;
using UnityEngine;

namespace CarWash.Core
{
    /// <summary>
    /// Cross-system static events (TECH §2). Payload types that live in other areas are passed as
    /// Component/primitive to keep Core free of upward dependencies.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<Component> VehicleArrived;
        public static event Action<Component, int> VehiclePaid;
        public static event Action<Component> CritterSpawned;
        public static event Action<Component, ulong> CritterCaptured;
        public static event Action<int> DayStarted;
        public static event Action<object> DayEnded;
        public static event Action<ulong, string> ToolEquipped;
        public static event Action<ulong> PlayerKnocked;
        public static event Action<GameState> StateChanged;

        public static void RaiseVehicleArrived(Component v) => VehicleArrived?.Invoke(v);
        public static void RaiseVehiclePaid(Component v, int amount) => VehiclePaid?.Invoke(v, amount);
        public static void RaiseCritterSpawned(Component c) => CritterSpawned?.Invoke(c);
        public static void RaiseCritterCaptured(Component c, ulong by) => CritterCaptured?.Invoke(c, by);
        public static void RaiseDayStarted(int day) => DayStarted?.Invoke(day);
        public static void RaiseDayEnded(object summary) => DayEnded?.Invoke(summary);
        public static void RaiseToolEquipped(ulong client, string toolId) => ToolEquipped?.Invoke(client, toolId);
        public static void RaisePlayerKnocked(ulong client) => PlayerKnocked?.Invoke(client);
        public static void RaiseStateChanged(GameState s) => StateChanged?.Invoke(s);

        /// <summary>Clears all subscribers (fast enter-play-mode has domain reload disabled).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetAll()
        {
            VehicleArrived = null; VehiclePaid = null; CritterSpawned = null; CritterCaptured = null;
            DayStarted = null; DayEnded = null; ToolEquipped = null; PlayerKnocked = null; StateChanged = null;
        }
    }
}
