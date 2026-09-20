using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.Core
{
    public sealed class GameConfigProvider : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] GameBalanceConfig balance;
        [SerializeField] GameDatabase database;

        public GameConfig Config => config;
        public GameBalanceConfig Balance => balance;
        public GameDatabase Database => database;

        void Awake()
        {
            if (config == null) TGLog.Error("GameConfigProvider: GameConfig is not assigned.");
            if (balance == null) TGLog.Error("GameConfigProvider: GameBalanceConfig is not assigned.");
            if (database == null) TGLog.Error("GameConfigProvider: GameDatabase is not assigned.");
            Services.Register(this);
        }

        void OnDestroy()
        {
            Services.Unregister(this);
        }
    }
}
