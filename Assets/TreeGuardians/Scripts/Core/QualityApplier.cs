using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.Core
{
    public static class QualityApplier
    {
        public static QualityTier CurrentTier { get; private set; } = QualityTier.Medium;
        public static bool ReduceMotion { get; private set; }

        public static void Apply(int tierIndex, bool reduceMotion, GameConfig config)
        {
            var tier = (QualityTier)Mathf.Clamp(tierIndex, 0, 2);
            CurrentTier = tier;
            ReduceMotion = reduceMotion;

            string wanted = tier switch
            {
                QualityTier.Low => "Low",
                QualityTier.High => "High",
                _ => "Medium"
            };
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == wanted)
                {
                    if (QualitySettings.GetQualityLevel() != i) QualitySettings.SetQualityLevel(i, false);
                    break;
                }
            }

            int fps = config != null ? config.targetFrameRate : 60;
            Application.targetFrameRate = tier == QualityTier.Low ? Mathf.Min(30, fps) : fps;
        }

        public static int ParticleBudgetMultiplier => CurrentTier switch
        {
            QualityTier.Low => 0,
            QualityTier.High => 2,
            _ => 1
        };
    }
}
