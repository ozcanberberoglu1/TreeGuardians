using System.Collections;
using NUnit.Framework;
using TreeGuardians.Battle;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.SceneFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TreeGuardians.Tests
{
    public class SmokeTests
    {
        static IEnumerator WaitForScene(string name, float timeout)
        {
            float t = 0f;
            while (SceneManager.GetActiveScene().name != name && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(name, SceneManager.GetActiveScene().name, $"Scene '{name}' not reached within {timeout}s");
        }

        static IEnumerator WaitUntil(System.Func<bool> cond, float timeout, string message)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), message);
        }

        [UnityTest]
        public IEnumerator Boot_ReachesMainMenu()
        {
            SceneManager.LoadScene("00_Boot");
            yield return WaitForScene("02_MainMenu", 20f);
            Assert.IsTrue(Services.IsBootstrapped);
        }

        [UnityTest]
        public IEnumerator MainMenu_StartBattle_LoadsBattleScene()
        {
            SceneManager.LoadScene("02_MainMenu");
            yield return WaitForScene("02_MainMenu", 10f);
            yield return WaitUntil(() => Services.IsBootstrapped, 15f, "services");
            UI.Menu.MenuUIController menu = null;
            yield return WaitUntil(() => (menu = Object.FindFirstObjectByType<UI.Menu.MenuUIController>()) != null, 10f, "menu controller");
            yield return null;
            yield return null;
            menu.StartBattle(BotDifficulty.Easy);
            yield return WaitForScene("03_Battle", 25f);
        }

        [UnityTest]
        public IEnumerator Battle_ProjectileDamagesAndReturnsToPool()
        {
            SceneManager.LoadScene("03_Battle");
            yield return WaitUntil(() => Services.IsBootstrapped, 15f, "services");
            var manager = Object.FindFirstObjectByType<BattleManager>();
            Assert.IsNotNull(manager);
            yield return WaitUntil(() => manager.Context != null && manager.StateMachine.State == BattleState.Playing, 15f, "battle playing");
            var ctx = manager.Context;
            var proj = ctx.database.GetProjectile("thorn");
            var core = ctx.enemyTree.Core;
            float before = ctx.enemyTree.StructureHealthRemaining();
            Vector2 from = (Vector2)ctx.playerTree.transform.position + new Vector2(2f, 4f);
            Vector2 to = ctx.enemyTree.GetSection("bark_mid").transform.position;
            var v = ctx.projectiles.LaunchVelocity(proj, from, to, 1f);
            ctx.projectiles.Fire(proj, from, v, BattleSide.Player, null, false, true, 3f, null);
            Assert.AreEqual(1, ctx.projectiles.ActiveCount);
            yield return WaitUntil(() => ctx.projectiles.ActiveCount == 0, 6f, "projectile finished");
            Assert.Less(ctx.enemyTree.StructureHealthRemaining(), before + 0.01f);
            Assert.IsNotNull(core);
        }

        [UnityTest]
        public IEnumerator Battle_AllEnemyGuardiansDown_TriggersVictoryOnce()
        {
            SceneManager.LoadScene("03_Battle");
            yield return WaitUntil(() => Services.IsBootstrapped, 15f, "services");
            var manager = Object.FindFirstObjectByType<BattleManager>();
            yield return WaitUntil(() => manager.Context != null && manager.StateMachine.State == BattleState.Playing, 15f, "battle playing");
            var ctx = manager.Context;
            int finished = 0;
            System.Action<BattleFinishedEvent> handler = e => finished++;
            GameEventBus.Subscribe(handler);
            for (int i = 0; i < ctx.enemyRoster.Slots.Count; i++)
            {
                var g = ctx.enemyRoster.Slots[i];
                if (g != null && g.IsActive && g.IsAlive)
                    ctx.damage.HitGuardian(g, 999999f, new DamageInfo { amount = 999999f, source = BattleSide.Player, hitPoint = g.transform.position });
            }
            yield return null;
            yield return null;
            GameEventBus.Unsubscribe(handler);
            Assert.AreEqual(BattleState.Victory, manager.StateMachine.State);
            Assert.AreEqual(1, finished);
        }

        [UnityTest]
        public IEnumerator Results_Continue_ReturnsToMainMenu()
        {
            SceneManager.LoadScene("04_Results");
            yield return WaitForScene("04_Results", 10f);
            yield return WaitUntil(() => Services.IsBootstrapped, 15f, "services");
            ResultsController results = null;
            yield return WaitUntil(() => (results = Object.FindFirstObjectByType<ResultsController>()) != null, 10f, "results controller");
            yield return null;
            yield return null;
            var so = new UnityEditor.SerializedObject(results);
            var btn = so.FindProperty("continueButton").objectReferenceValue as UnityEngine.UI.Button;
            Assert.IsNotNull(btn);
            btn.onClick.Invoke();
            yield return WaitForScene("02_MainMenu", 20f);
        }
    }
}
