using UnityEngine;

namespace TreeGuardians.Core
{
    [CreateAssetMenu(menuName = "Tree Guardians/Core/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Oyunun görünen adı. Tüm UI ve loglar bu alanı kullanır.")]
        public string gameName = "Tree Guardians";
        [Tooltip("Boot ekranında gösterilen stüdyo adı.")]
        public string studioName = "Leke Games";
        [Tooltip("Boot ve ayarlar ekranında gösterilen sürüm etiketi.")]
        public string version = "0.1.0";

        [Header("Scenes")]
        public string bootScene = "00_Boot";
        public string loadingScene = "01_Loading";
        public string mainMenuScene = "02_MainMenu";
        public string battleScene = "03_Battle";
        public string resultsScene = "04_Results";
        public string sandboxScene = "05_Sandbox";
        [Tooltip("Kapalıysa Sandbox sahnesi Build Settings'te devre dışı bırakılır.")]
        public bool includeSandboxInBuild;

        [Header("Display")]
        [Tooltip("Hedef kare hızı. Düşük kalite profilinde otomatik 30'a düşürülebilir.")]
        public int targetFrameRate = 60;
        public Vector2 referenceResolution = new Vector2(2340f, 1080f);
        [Range(0f, 1f)] public float canvasMatchWidthOrHeight = 0.5f;

        [Header("Loading")]
        [Tooltip("Loading ekranının en az kaç saniye görünür kalacağı.")]
        public float minLoadingSeconds = 1.0f;
        [Tooltip("Yükleme çubuğunun gerçek ilerlemeye yaklaşma hızı.")]
        public float loadingBarSmoothing = 6f;
        [Tooltip("İpuçlarının kaç saniyede bir değişeceği.")]
        public float loadingTipIntervalSeconds = 2.5f;
    }
}
