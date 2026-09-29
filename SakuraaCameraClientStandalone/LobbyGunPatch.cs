using System;
using System.Reflection;
using HarmonyLib;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu.Pages;

namespace SakuraaCameraClientStandalone;

// removes the "Gun" tab (crosshair icon) from the Lobby page. it did the exact same thing
// as the "SELECT GUN" button already inside the Leaderboard tab (toggle the pointer, then
// refresh), so it was a pure duplicate. this patches LobbyPage at runtime instead of
// touching the compiled mod's source.
public static class LobbyGunTabPatch
{
	public static void Install()
	{
		try
		{
			Harmony harmony = new Harmony("local.sakuraa.lobbyguntab");

			MethodInfo buildTabs = typeof(LobbyPage).GetMethod("BuildTabs");
			MethodInfo buildTabsPostfix = typeof(LobbyGunTabPatch).GetMethod(nameof(BuildTabsPostfix), BindingFlags.Static | BindingFlags.NonPublic);
			harmony.Patch(buildTabs, postfix: new HarmonyMethod(buildTabsPostfix));

			MethodInfo onTabSelected = typeof(LobbyPage).GetMethod("OnTabSelected");
			MethodInfo onTabSelectedPrefix = typeof(LobbyGunTabPatch).GetMethod(nameof(OnTabSelectedPrefix), BindingFlags.Static | BindingFlags.NonPublic);
			harmony.Patch(onTabSelected, prefix: new HarmonyMethod(onTabSelectedPrefix));

			StandalonePlugin.StatusLog("lobby gun tab patch installed");
		}
		catch (Exception ex)
		{
			StandalonePlugin.StatusLog("lobby gun tab patch failed: " + ex.Message);
		}
	}

	// runs right after LobbyPage builds its tabs (Lobby, Leaderboard, Gun, Friends) and
	// drops the Gun one, leaving Lobby, Leaderboard, Friends at indices 0, 1, 2.
	private static void BuildTabsPostfix(LobbyPage __instance)
	{
		__instance.Tabs.RemoveAll((UtilTab t) => t.TabName == "Gun");
	}

	// Friends used to be tab index 3 (Lobby=0, Leaderboard=1, Gun=2, Friends=3). now that
	// Gun is gone, tapping the third visible tab reports index 2, so this remaps it back
	// to 3 before the original method's switch runs, so it still calls RefreshFriendList
	// instead of falling into the old (now unreachable) Gun case.
	private static void OnTabSelectedPrefix(ref int tabIndex)
	{
		if (tabIndex == 2)
		{
			tabIndex = 3;
		}
	}
}
