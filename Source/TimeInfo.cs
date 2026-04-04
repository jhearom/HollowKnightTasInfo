using System;
using System.Collections.Generic;
using System.Text;
using Assembly_CSharp.TasInfo.mm.Source.Utils;
using UnityEngine;

namespace Assembly_CSharp.TasInfo.mm.Source {
    internal class TimeInfo : BaseTimer {
        public static bool timeStart = false;

        public static void OnPreRender(GameManager gameManager, StringBuilder infoBuilder) {
            timeStart = TimeStart;

            List<string> result = new();
            if (!string.IsNullOrEmpty(gameManager.sceneName) && ConfigManager.ShowSceneName) {
                result.Add(gameManager.sceneName);
            }

            if (InGameTime > 0 && ConfigManager.ShowTime) {
                result.Add(FormattedTime(InGameTime));
            }

            if (ConfigManager.ShowUnscaledTime) {
                var utime = TimeSpan.FromSeconds(Time.unscaledTime);
                var hours = utime.Hours > 0 ? $"{utime.Hours:00}:" : "";
                var minutes = utime.Minutes > 0 ? $"{utime.Minutes:00}:" : "";
                result.Add($"UT {hours}{minutes}{utime.Seconds:00}.{utime.Milliseconds:000}");
            }

            string resultString = StringUtils.Join("  ", result);
            if (!string.IsNullOrEmpty(resultString)) {
                infoBuilder.AppendLine(resultString);
            }

            if (ConfigManager.ShowTimeMinusFixedTime) {
                infoBuilder.AppendLine($"T-FT {1000 * (Time.time - Time.fixedTime):00.0000} ms");
            }
        }
    }
}
