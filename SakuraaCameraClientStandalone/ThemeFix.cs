using System;
using System.Reflection;

namespace SakuraaCameraClientStandalone;

public sealed partial class StandalonePlugin
{
	private static Type _themeManagerType;
	private static MethodInfo _themeReapply;

	private static void StartThemeFix(Assembly core)
	{
		try
		{
			BindingFlags all = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			_themeManagerType = core.GetType("SakuraaCastingMod.VR.UtilMenu.Utility.ThemeManager", throwOnError: true);
			_themeReapply = _themeManagerType.GetMethod("ReapplyCurrentTheme", all);
			Assembly presets = LoadEmbeddedAssembly("SakuraaOfflinePresets");
			Type store = presets.GetType("SakuraaOfflinePresets.OfflinePresetStore", throwOnError: true);
			MethodInfo target = store.GetMethod("ApplyStartupDefaults", all);
			if (target == null)
			{
				throw new MissingMethodException(store.FullName, "ApplyStartupDefaults");
			}
			MethodInfo prefix = typeof(StandalonePlugin).GetMethod("ThemeKeepPrefix", all);
			MethodInfo postfix = typeof(StandalonePlugin).GetMethod("ThemeKeepPostfix", all);
			new HarmonyLib.Harmony("local.sakuraa.themekeep").Patch(target, prefix: new HarmonyLib.HarmonyMethod(prefix), postfix: new HarmonyLib.HarmonyMethod(postfix));
			StatusLog("theme keep patch installed");
		}
		catch (Exception ex)
		{
			StatusLog("theme keep patch failed: " + ex.Message);
		}
	}

	private static object GetThemeField(string name)
	{
		FieldInfo field = _themeManagerType.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		return (field != null) ? field.GetValue(null) : null;
	}

	private static void SetThemeField(string name, object value)
	{
		FieldInfo field = _themeManagerType.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (field != null && value != null)
		{
			field.SetValue(null, value);
		}
	}

	private static void ThemeKeepPrefix(out object[] __state)
	{
		__state = null;
		try
		{
			__state = new object[3]
			{
				GetThemeField("IsCustomThemeEnabled"),
				GetThemeField("CurrentThemeIndex"),
				GetThemeField("ActiveCustomSlot")
			};
		}
		catch (Exception ex)
		{
			StatusLog("theme keep prefix failed: " + ex.Message);
		}
	}

	private static void ThemeKeepPostfix(object[] __state)
	{
		if (__state == null)
		{
			return;
		}
		try
		{
			SetThemeField("IsCustomThemeEnabled", __state[0]);
			SetThemeField("CurrentThemeIndex", __state[1]);
			SetThemeField("ActiveCustomSlot", __state[2]);
			if (_themeReapply != null)
			{
				_themeReapply.Invoke(null, null);
			}
			StatusLog("theme fields kept: custom=" + __state[0] + " idx=" + __state[1] + " slot=" + __state[2]);
		}
		catch (Exception ex)
		{
			StatusLog("theme keep postfix failed: " + ex.Message);
		}
	}
}
