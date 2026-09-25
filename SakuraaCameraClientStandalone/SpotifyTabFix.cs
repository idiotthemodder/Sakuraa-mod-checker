using System;
using System.Reflection;
using HarmonyLib;
using SakuraaCastingMod.VR.UtilMenu.Pages;

namespace SakuraaCameraClientStandalone;

public static class SpotifyTabFix
{
	public static void Install()
	{
		try
		{
			MethodInfo target = typeof(SpotifyPage).GetMethod("BuildTabs");
			MethodInfo postfix = typeof(SpotifyTabFix).GetMethod("BuildTabsPostfix", BindingFlags.Static | BindingFlags.NonPublic);
			new Harmony("local.sakuraa.spotifytabfix").Patch(target, postfix: new HarmonyMethod(postfix));
			StandalonePlugin.StatusLog("spotify tab fix installed");
		}
		catch (Exception ex)
		{
			StandalonePlugin.StatusLog("spotify tab fix failed: " + ex.Message);
		}
	}

	private static void BuildTabsPostfix(SpotifyPage __instance)
	{
		if (__instance.Tabs.Count >= 2)
		{
			SakuraaCastingMod.Shared.Models.UtilTab first = __instance.Tabs[0];
			SakuraaCastingMod.Shared.Models.UtilTab second = __instance.Tabs[1];
			first.Elements.InsertRange(0, second.Elements);
			second.Elements.Clear();
		}
	}
}

public sealed partial class StandalonePlugin
{
	private static void PatchSpotifyTabs()
	{
		SpotifyTabFix.Install();
	}
}
