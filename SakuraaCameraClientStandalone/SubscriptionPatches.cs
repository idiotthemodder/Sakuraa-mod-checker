using System;
using HarmonyLib;
using GorillaTagScripts;

namespace SakuraaCameraClientStandalone
{
    public static class SubscriptionPatches
    {
        public static bool Enabled;

        [HarmonyPatch(typeof(SubscriptionManager), nameof(SubscriptionManager.IsLocalSubscribed))]
        private static class IsLocalSubscribedPatch
        {
            private static bool Prefix(ref bool __result)
            {
                if (!Enabled)
                    return true;

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(SubscriptionManager), nameof(SubscriptionManager.LocalSubscriptionStatus))]
        private static class LocalSubscriptionStatusPatch
        {
            private static bool Prefix(ref SubscriptionManager.SubscriptionStatus __result)
            {
                if (!Enabled)
                    return true;

                __result = SubscriptionManager.SubscriptionStatus.Active;
                return false;
            }
        }

        [HarmonyPatch(typeof(SubscriptionManager), nameof(SubscriptionManager.LocalSubscriptionDetails))]
        private static class LocalSubscriptionDetailsPatch
        {
            private static bool Prefix(ref SubscriptionManager.SubscriptionDetails __result)
            {
                if (!Enabled)
                    return true;

                __result = new SubscriptionManager.SubscriptionDetails
                {
                    active = true,
                    daysAccrued = int.MaxValue,
                    subscriptionFeatureSettings = new[] { true, true },
                    tier = int.MaxValue,
                    subscriptionActiveUntilDate = DateTime.MaxValue,
                    autoRenew = true,
                    autoRenewMonths = int.MaxValue
                };

                return false;
            }
        }
    }
}
