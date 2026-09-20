using System;
using TreeGuardians.Core;

namespace TreeGuardians.Monetization
{
    public interface IIAPService
    {
        bool IsAvailable { get; }
        string GetLocalizedPrice(string productId);
        void Purchase(string productId, Action<bool> onComplete);
    }

    public interface IRewardedAdService
    {
        bool IsReady { get; }
        void Show(Action<bool> onComplete);
    }

    /// Editor/prototype stand-in: every purchase succeeds after a short delay. No real money moves.
    public sealed class MockIAPService : IIAPService
    {
        public bool IsAvailable => true;
        public string GetLocalizedPrice(string productId) => "TEST";
        public void Purchase(string productId, Action<bool> onComplete)
        {
            TGTween.Delay(0.4f, () => onComplete?.Invoke(true));
        }
    }

    public sealed class MockRewardedAdService : IRewardedAdService
    {
        public bool IsReady => true;
        public void Show(Action<bool> onComplete)
        {
            TGTween.Delay(0.6f, () => onComplete?.Invoke(true));
        }
    }

    public static class MonetizationServices
    {
        public static IIAPService IAP { get; set; } = new MockIAPService();
        public static IRewardedAdService Ads { get; set; } = new MockRewardedAdService();
    }
}
