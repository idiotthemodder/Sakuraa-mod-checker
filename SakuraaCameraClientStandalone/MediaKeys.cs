using System;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed partial class StandalonePlugin
{
	private static void PatchMediaKeys(Assembly core)
	{
		Type manager = core.GetType("SakuraaCastingMod.Shared.Integrations.SpotifyManager", throwOnError: true);
		MethodInfo prefix = typeof(StandalonePlugin).GetMethod("MediaKeyPrefix", BindingFlags.Static | BindingFlags.NonPublic);
		HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("local.sakuraa.mediakeys");
		foreach (string name in new string[3] { "NativePlayPause", "NativeNextTrack", "NativePreviousTrack" })
		{
			MethodInfo target = manager.GetMethod(name, BindingFlags.Static | BindingFlags.Public);
			if (target != null)
			{
				harmony.Patch(target, prefix: new HarmonyLib.HarmonyMethod(prefix));
			}
		}
	}

	private static bool MediaKeyPrefix(MethodBase __originalMethod)
	{
		string cmd = null;
		if (__originalMethod.Name == "NativePlayPause")
		{
			cmd = "play-pause";
		}
		else if (__originalMethod.Name == "NativeNextTrack")
		{
			cmd = "next";
		}
		else if (__originalMethod.Name == "NativePreviousTrack")
		{
			cmd = "previous";
		}
		if (cmd == null)
		{
			return true;
		}
		try
		{
			using UdpClient client = new UdpClient();
			byte[] data = Encoding.ASCII.GetBytes(cmd);
			client.Send(data, data.Length, "127.0.0.1", 5001);
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("[MediaKeys] " + ex.Message));
		}
		return false;
	}
}
