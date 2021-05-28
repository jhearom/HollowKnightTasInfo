﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assembly_CSharp.TasInfo.mm.Source.Extensions;
using Assembly_CSharp.TasInfo.mm.Source.Utils;
using GlobalEnums;
using UnityEngine;

namespace Assembly_CSharp.TasInfo.mm.Source {
    internal static class EnemyInfo {
        private static readonly Dictionary<GameObject, EnemyData> EnemyPool = new();

        private static readonly string[] IgnoreObjectNames = {
            "Hornet Barb",
            "Needle Tink",
            "Worm",
            "Laser Turret",
            "Deep Spikes",
        };

        public static void OnInit(GameManager gameManager) {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += (scene, nextScene) => {
                EnemyPool.Clear();

                if (gameManager.IsNonGameplayScene()) {
                    return;
                }

                TryAddEnemy(Resources.FindObjectsOfTypeAll<Transform>().Select(transform => transform.gameObject));
            };
        }

        public static void OnPreRender(GameManager gameManager, StringBuilder infoBuilder) {
            if (gameManager.IsNonGameplayScene()) {
                return;
            }

            string enemyInfo = GetInfo();
            if (!string.IsNullOrEmpty(enemyInfo)) {
                infoBuilder.AppendLine(enemyInfo);
            }
        }

        public static void TryAddEnemy(GameObject gameObject) {
            TryAddEnemy(gameObject.GetComponentsInChildren<Transform>(true).Select(transform => transform.gameObject));
        }

        private static void TryAddEnemy(IEnumerable<GameObject> gameObjects) {
            foreach (GameObject gameObject in gameObjects) {
                if (gameObject == null || EnemyPool.ContainsKey(gameObject)) {
                    continue;
                }

                if (((PhysLayers) gameObject.layer is PhysLayers.ENEMIES or PhysLayers.HERO_ATTACK || gameObject.CompareTag("Boss"))
                    && IgnoreObjectNames.All(name => !gameObject.name.StartsWith(name))) {
                    PlayMakerFSM playMakerFsm = gameObject.LocateMyFSM("health_manager_enemy");
                    if (playMakerFsm == null) {
                        playMakerFsm = gameObject.LocateMyFSM("health_manager");
                    }

                    if (playMakerFsm != null) {
                        EnemyPool.Add(gameObject, new EnemyData(gameObject, playMakerFsm));
                    }
                }
            }
        }

        private static string GetInfo() {
            return StringUtils.Join("|", EnemyPool.Values, "Enemy=");
        }

        private class EnemyData {
            private readonly GameObject gameObject;
            private readonly Rigidbody2D rigidbody2D;
            private readonly PlayMakerFSM fsm;
            private int Hp => fsm.FsmVariables.FindFsmInt("HP").Value;

            public EnemyData(GameObject gameObject, PlayMakerFSM fsm) {
                this.gameObject = gameObject;
                rigidbody2D = gameObject.GetComponent<Rigidbody2D>();
                this.fsm = fsm;
            }

            public override string ToString() {
                if (Camera.main == null || gameObject == null || !gameObject.activeInHierarchy || Hp <= 0) {
                    return string.Empty;
                }

                Vector2 enemyPos = Camera.main.WorldToScreenPoint(gameObject.transform.WorldPosition());
                enemyPos.y = Screen.height - enemyPos.y;

                int x = (int) enemyPos.x;
                int y = (int) enemyPos.y;

                List<string> result = new();

                if (ConfigManager.ShowEnemyHp && ScreenUtils.InsideOfScreen(x, y)) {
                    result.Add($"{x}|{y}|{Hp}");
                    y += 23;
                }

                if (ConfigManager.ShowEnemyPosition && ScreenUtils.InsideOfScreen(x, y)) {
                    result.Add($"{x}|{y}|{gameObject.transform.position.ToSimpleString(ConfigManager.PositionPrecision)}");
                    y += 23;
                }

                if (ConfigManager.ShowEnemyVelocity && rigidbody2D != null && ScreenUtils.InsideOfScreen(x, y)) {
                    result.Add($"{x}|{y}|{rigidbody2D.velocity.ToSimpleString(ConfigManager.VelocityPrecision)}");
                }

                return StringUtils.Join("|", result);
            }
        }
    }
}