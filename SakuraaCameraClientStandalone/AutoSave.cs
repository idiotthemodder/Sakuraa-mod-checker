using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed class SettingsAutoSaver : MonoBehaviour
{
	private float _next;

	private void Update()
	{
		if (Time.unscaledTime < _next)
		{
			return;
		}
		_next = Time.unscaledTime + 5f;
		StandalonePlugin.AutoSaveTick();
	}

	private void OnApplicationQuit()
	{
		StandalonePlugin.AutoSaveTick();
	}
}

public sealed partial class StandalonePlugin
{
	private static bool _settingsLoaded;
	private static string _lastSaved;

	private static void StartAutoSaver(Assembly core)
	{
		Type cfg = core.GetType("SakuraaCastingMod.Core.Configuration", throwOnError: true);
		MethodInfo load = cfg.GetMethod("InitialSettingsLoad", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (load != null)
		{
			MethodInfo post = typeof(StandalonePlugin).GetMethod("InitialLoadPostfix", BindingFlags.Static | BindingFlags.NonPublic);
			new HarmonyLib.Harmony("local.sakuraa.autosave").Patch(load, postfix: new HarmonyLib.HarmonyMethod(post));
		}
		GameObject go = new GameObject("SakuraaSettingsAutoSaver");
		UnityEngine.Object.DontDestroyOnLoad(go);
		go.AddComponent<SettingsAutoSaver>();
	}

	private static void InitialLoadPostfix()
	{
		_settingsLoaded = true;
	}

	internal static void AutoSaveTick()
	{
		if (!_settingsLoaded || _gatherKeys == null)
		{
			return;
		}
		try
		{
			string json = JsonConvert.SerializeObject(_gatherKeys.Invoke(null, null), Formatting.Indented);
			if (json == _lastSaved)
			{
				return;
			}
			File.WriteAllText(_cfgPath, json);
			_lastSaved = json;
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("[AutoSave] " + ex.Message));
		}
	}
}
