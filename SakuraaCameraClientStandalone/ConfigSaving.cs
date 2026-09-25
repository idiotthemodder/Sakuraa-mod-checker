using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using Newtonsoft.Json;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed partial class StandalonePlugin
{
	private static CancellationTokenSource _saveDebounce;
	private static MethodInfo _gatherKeys;
	private static MethodInfo _flushNow;
	private static string _cfgPath;

	private static void PatchConfigSaving(Assembly core)
	{
		Type cfg = core.GetType("SakuraaCastingMod.Core.Configuration", throwOnError: true);
		BindingFlags all = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		_gatherKeys = cfg.GetMethod("GatherCurrentKeys", all);
		_flushNow = cfg.GetMethod("FlushNow", all);
		MethodInfo target = cfg.GetMethod("QueueAutoSave", all);
		if (_gatherKeys == null || target == null)
		{
			throw new MissingMethodException(cfg.FullName, "QueueAutoSave/GatherCurrentKeys");
		}
		string dir = Path.Combine(Paths.ConfigPath, "SakuraaCameraClient");
		Directory.CreateDirectory(dir);
		_cfgPath = Path.Combine(dir, "CamModSettingsSave.json");
		MethodInfo prefix = typeof(StandalonePlugin).GetMethod("QueueAutoSavePrefix", BindingFlags.Static | BindingFlags.NonPublic);
		new HarmonyLib.Harmony("local.sakuraa.configsave").Patch(target, prefix: new HarmonyLib.HarmonyMethod(prefix));
	}

	private static bool QueueAutoSavePrefix()
	{
		try
		{
			_saveDebounce?.Cancel();
			CancellationToken token = (_saveDebounce = new CancellationTokenSource()).Token;
			Task.Delay(1000, token).ContinueWith(delegate(Task t)
			{
				if (!t.IsCanceled)
				{
					WriteLocalSettings();
				}
			});
			return false;
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("[ConfigSaving] " + ex.Message));
			return true;
		}
	}

	private static void WriteLocalSettings()
	{
		try
		{
			object keys = _gatherKeys.Invoke(null, null);
			File.WriteAllText(_cfgPath, JsonConvert.SerializeObject(keys, Formatting.Indented));
			_flushNow?.Invoke(null, null);
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("[ConfigSaving] write failed: " + ex.Message));
		}
	}
}
