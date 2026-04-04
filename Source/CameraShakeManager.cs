using System;
using System.Collections.Generic;
using Assembly_CSharp.TasInfo.mm.Source.Utils;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace Assembly_CSharp.TasInfo.mm.Source {
    internal static class CameraShakeManager {
#if V1432
        private static readonly Dictionary<ShakePosition, Vector3> OriginalExtents = new();
#endif

        public static void Init() {
#if V1432
            HookUtils.HookEnter<ShakePosition, Action<ShakePosition>>("UpdateShaking", OnUpdateShakingEnter);
            HookUtils.HookExit<ShakePosition, Action<ShakePosition>>("UpdateShaking", OnUpdateShakingExit);
#endif
        }

#if V1432
        private static void OnUpdateShakingEnter(ShakePosition action) {
            if (action?.extents == null || OriginalExtents.ContainsKey(action)) {
                return;
            }

            float multiplier = ConfigManager.CameraShakeMultiplier;
            if (Math.Abs(multiplier - 1f) < 0.0001f || !IsCameraShakeAction(action)) {
                return;
            }

            Vector3 originalExtents = action.extents.Value;
            OriginalExtents[action] = originalExtents;
            action.extents.Value = originalExtents * multiplier;
        }

        private static void OnUpdateShakingExit(ShakePosition action) {
            if (action?.extents == null) {
                return;
            }

            if (OriginalExtents.TryGetValue(action, out Vector3 originalExtents)) {
                action.extents.Value = originalExtents;
                OriginalExtents.Remove(action);
            }
        }

        private static bool IsCameraShakeAction(ShakePosition action) {
            return action.Fsm != null &&
                   string.Equals(action.Fsm.Name, "CameraShake", StringComparison.Ordinal) &&
                   string.Equals(action.Fsm.GameObjectName, "CameraParent", StringComparison.Ordinal);
        }
#endif
    }
}
