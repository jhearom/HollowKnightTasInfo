using GlobalEnums;
using System.Collections.Generic;
using UnityEngine;

namespace Assembly_CSharp.TasInfo.mm.Source {

    internal class HollowKnightStoredData {

        /// <summary>
        /// Tracks a value of type T so that each time it updates, the previous value is stored
        /// </summary>
        private class Tracked<T> {
            public T current;
            public T previous;
            private int lastFrame = -1;

            public Tracked(T initialValue) {
                previous = current = initialValue;
            }

            public void Sample(T val) {
                if (lastFrame == Time.frameCount) {
                    return;
                }

                previous = current;
                current = val;
                lastFrame = Time.frameCount;
            }
        }

        private readonly Dictionary<Offset, Tracked<int>> pdInts = new();
        private readonly Dictionary<Offset, Tracked<bool>> pdBools = new();
        public bool TraitorLordDeadOnEntry { get; private set; }
        /// <summary>
        /// Returns true if the knight is currently in a transition and has already split there
        /// </summary>
        public bool SplitThisTransition { get; set; }
        public int GladeEssence { get; set; }

        // Colosseum enemies
        public int killsColShieldStart { get; set; }
        public int killsColRollerStart { get; set; }
        public int killsColMinerStart { get; set; }
        public int killsSpitterStart { get; set; }
        public int killsBuzzerStart { get; set; }
        public int killsBigBuzzerStart { get; set; }
        public int killsBurstingBouncerStart { get; set; }
        public int killsBigFlyStart { get; set; }
        public int killsColWormStart { get; set; }
        public int killsColFlyingSentryStart { get; set; }
        public int killsColMosquitoStart { get; set; }
        public int killsCeilingDropperStart { get; set; }
        public int killsColHopperStart { get; set; }
        public int killsGiantHopperStart { get; set; }
        public int killsGrubMimicStart { get; set; }
        public int killsBlobbleStart { get; set; }
        public int killsOblobbleStart { get; set; }
        public int killsAngryBuzzerStart { get; set; }
        public int killsHeavyMantisStart { get; set; }
        public int killsHeavyMantisFlyerStart { get; set; }
        public int killsMageKnightStart { get; set; }
        public int killsMageStart { get; set; }
        public int killsElectricMageStart { get; set; }
        public int killsLesserMawlekStart { get; set; }
        public int killsMawlekStart { get; set; }
        public int killsLobsterLancerStart { get; set; }

        private readonly GameManager gameManager;

        /// <summary>
        /// Reset the stored data's memory
        /// </summary>
        public void Reset() {
            pdInts.Clear();
            pdBools.Clear();
            TraitorLordDeadOnEntry = false;
            SplitThisTransition = false;
            GladeEssence = 0;
            ResetKills();
        }

        public void ResetKills() {
            if (gameManager.playerData == null) {
                return;
            }

            killsColShieldStart = gameManager.playerData.killsColShield;
            killsColRollerStart = gameManager.playerData.killsColRoller;
            killsColMinerStart = gameManager.playerData.killsColMiner;
            killsSpitterStart = gameManager.playerData.killsSpitter;
            killsBuzzerStart = gameManager.playerData.killsBuzzer;
            killsBigBuzzerStart = gameManager.playerData.killsBigBuzzer;
            killsBurstingBouncerStart = gameManager.playerData.killsBurstingBouncer;
            killsBigFlyStart = gameManager.playerData.killsBigFly;
            killsColWormStart = gameManager.playerData.killsColWorm;
            killsColFlyingSentryStart = gameManager.playerData.killsColFlyingSentry;
            killsColMosquitoStart = gameManager.playerData.killsColMosquito;
            killsCeilingDropperStart = gameManager.playerData.killsCeilingDropper;
            killsColHopperStart = gameManager.playerData.killsColHopper;
            killsGiantHopperStart = gameManager.playerData.killsGiantHopper;
            killsGrubMimicStart = gameManager.playerData.killsGrubMimic;
            killsBlobbleStart = gameManager.playerData.killsBlobble;
            killsOblobbleStart = gameManager.playerData.killsOblobble;
            killsAngryBuzzerStart = gameManager.playerData.killsAngryBuzzer;
            killsHeavyMantisStart = gameManager.playerData.killsHeavyMantis;
            killsHeavyMantisFlyerStart = gameManager.playerData.killsMantisHeavyFlyer;
            killsMageKnightStart = gameManager.playerData.killsMageKnight;
            killsMageStart = gameManager.playerData.killsMage;
            killsElectricMageStart = gameManager.playerData.killsElectricMage;
            killsLesserMawlekStart = gameManager.playerData.killsLesserMawlek;
            killsMawlekStart = gameManager.playerData.killsMawlek;
            killsLobsterLancerStart = gameManager.playerData.killsLobsterLancer;
        }
        private Tracked<int> GetValue(Offset offset, int PD) {
            if (!pdInts.ContainsKey(offset)) {
                pdInts[offset] = new Tracked<int>(PD);
            }

            pdInts[offset].Sample(PD);
            return pdInts[offset];
        }

        private Tracked<bool> GetBoolValue(Offset offset, bool PD) {
            if (!pdBools.ContainsKey(offset)) {
                pdBools[offset] = new Tracked<bool>(PD);
            }

            pdBools[offset].Sample(PD);
            return pdBools[offset];
        }

        /// <summary>
        /// Checks if the PD int given by offset has increased by value (i.e. new - old = value) since the last update
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool CheckIncreasedBy(Offset offset, int value, int PD) {
            Tracked<int> tracked = GetValue(offset, PD);
            return tracked.current - tracked.previous == value;
        }
        /// <summary>
        /// Checks if the PD int given by offset has increased by 1 since the last update
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public bool CheckIncremented(Offset offset, int PD) {
            return CheckIncreasedBy(offset, 1, PD);
        }
        /// <summary>
        /// Checks if the PD int given by offset has increased since the last update
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public bool CheckIncreased(Offset offset, int PD) {
            Tracked<int> tracked = GetValue(offset, PD);
            return tracked.current > tracked.previous;
        }

        /// <summary>
        /// Checks if the PD bool given by offset has toggled from True to False since the last update
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public bool CheckToggledFalse(Offset offset, bool PD) {
            Tracked<bool> tracked = GetBoolValue(offset, PD);
            return tracked.previous && !tracked.current;
        }

        public void SetTraitorLordDeadOnEntry(bool isDead) {
            TraitorLordDeadOnEntry = isDead;
        }

        public HollowKnightStoredData(GameManager gameManager) {
            this.gameManager = gameManager;
        }
    }
}
