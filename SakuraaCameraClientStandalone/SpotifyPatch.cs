using System;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed partial class StandalonePlugin
{
	private static void PatchSpotifyClientId(Assembly core)
	{
		Type manager = core.GetType("SakuraaCastingMod.Shared.Integrations.SpotifyManager", throwOnError: true);
		MethodInfo target = manager.GetMethod("LoadClientId", BindingFlags.Instance | BindingFlags.NonPublic);
		if (target == null)
		{
			throw new MissingMethodException(manager.FullName, "LoadClientId");
		}
		MethodInfo prefix = typeof(StandalonePlugin).GetMethod("LoadClientIdPrefix", BindingFlags.Static | BindingFlags.NonPublic);
		new HarmonyLib.Harmony("local.sakuraa.spotifyid").Patch(target, prefix: new HarmonyLib.HarmonyMethod(prefix));
	}

	private static bool LoadClientIdPrefix(object __instance)
	{
		try
		{
			string path = Path.Combine(Paths.ConfigPath, "sakuraa_spotify_client_id.txt");
			if (File.Exists(path))
			{
				string id = File.ReadAllText(path).Trim();
				if (id.Length > 0)
				{
					FieldInfo field = __instance.GetType().GetField("<ClientId>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
					if (field != null)
					{
						field.SetValue(null, id);
						return false;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("[SpotifyPatch] " + ex.Message));
		}
		return true;
	}
}
