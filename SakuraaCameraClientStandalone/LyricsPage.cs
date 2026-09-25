using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed class LyricsPage : BasePage
{
	public override string PageName => "LYRICS";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.Headset;

	private static UtilTab NewTab(string name, Material icon)
	{
		return new UtilTab
		{
			TabIcon = icon,
			TabName = name
		};
	}

	public override void BuildTabs()
	{
		Tabs.Clear();
		if (!StandalonePlugin.LyricsLoaded())
		{
			UtilTab none = NewTab("Lyrics", UtilMenuMain.Instance.Icons.Headset);
			none.Elements.Add(new MenuElement("LYRICS HUD NOT LOADED", delegate
			{
			}));
			Tabs.Add(none);
			return;
		}
		UtilTab main = NewTab("Main", UtilMenuMain.Instance.Icons.Options);
		main.Elements.Add(new MenuElement("SHOW HUD", delegate
		{
			StandalonePlugin.LyricsToggleVisible();
			Refresh();
		}, StandalonePlugin.LyricsGetVisible()));
		main.Elements.Add(BoolToggle("ALBUM ART", "Display", "ShowAlbumArt"));
		main.Elements.Add(BoolToggle("ALBUM NAME", "Display", "ShowAlbumName"));
		main.Elements.Add(BoolToggle("TIME", "Display", "ShowTime"));
		main.Elements.Add(BoolToggle("PROGRESS BAR", "Display", "ShowProgressBar"));
		main.Elements.Add(IntSlider("LYRIC LINES", "Display", "LyricLines", 0, 3));
		Tabs.Add(main);

		UtilTab pos = NewTab("Position", UtilMenuMain.Instance.Icons.MoveArrows);
		pos.Elements.Add(FloatSlider("DISTANCE", "HUD", "Distance", 0.05f, 0.3f, 2f, null));
		pos.Elements.Add(FloatSlider("HEIGHT", "HUD", "HeightBelowEyes", 0.02f, -0.3f, 0.6f, null));
		pos.Elements.Add(FloatSlider("OFFSET X", "HUD", "OffsetX", 0.02f, -0.6f, 0.6f, null));
		pos.Elements.Add(FloatSlider("SCALE", "HUD", "Scale", 0.1f, 0.3f, 3f, null));
		if (StandalonePlugin.LyricsHasEntry("HUD", "TiltDegrees"))
		{
			pos.Elements.Add(FloatSlider("TILT", "HUD", "TiltDegrees", 5f, -45f, 45f, "F0"));
		}
		pos.Elements.Add(new MenuElement("RESET LOOK", delegate
		{
			StandalonePlugin.LyricsReset("HUD", "Distance");
			StandalonePlugin.LyricsReset("HUD", "HeightBelowEyes");
			StandalonePlugin.LyricsReset("HUD", "OffsetX");
			StandalonePlugin.LyricsReset("HUD", "Scale");
			StandalonePlugin.LyricsReset("HUD", "TiltDegrees");
			Refresh();
		}));
		Tabs.Add(pos);

		UtilTab colors = NewTab("Colors", UtilMenuMain.Instance.Icons.Paintbrush);
		colors.Elements.Add(ColorSlider("TEXT", "Look", "TextColor", StandalonePlugin.LyTextNames, StandalonePlugin.LyTextHex, false));
		colors.Elements.Add(ColorSlider("ACCENT", "Look", "AccentColor", StandalonePlugin.LyAccentNames, StandalonePlugin.LyAccentHex, false));
		colors.Elements.Add(ColorSlider("BACKGROUND", "Look", "BackgroundColor", StandalonePlugin.LyBgNames, StandalonePlugin.LyBgHex, true));
		MenuElement opacity = null;
		opacity = new MenuElement("OPACITY", StandalonePlugin.LyricsOpacity() + "%", delegate
		{
			opacity.ValueText = StandalonePlugin.LyricsAdjustOpacity(-10) + "%";
			Refresh();
		}, delegate
		{
			opacity.ValueText = StandalonePlugin.LyricsAdjustOpacity(10) + "%";
			Refresh();
		});
		colors.Elements.Add(opacity);
		colors.Elements.Add(new MenuElement("RESET COLORS", delegate
		{
			StandalonePlugin.LyricsReset("Look", "TextColor");
			StandalonePlugin.LyricsReset("Look", "AccentColor");
			StandalonePlugin.LyricsReset("Look", "BackgroundColor");
			Refresh();
		}));
		Tabs.Add(colors);

		UtilTab media = NewTab("Media", UtilMenuMain.Instance.Icons.Headset);
		media.Elements.Add(new MenuElement("PLAY / PAUSE", delegate
		{
			StandalonePlugin.LyricsSend("playpause");
		}));
		media.Elements.Add(new MenuElement("NEXT", delegate
		{
			StandalonePlugin.LyricsSend("next");
		}));
		media.Elements.Add(new MenuElement("PREVIOUS", delegate
		{
			StandalonePlugin.LyricsSend("previous");
		}));
		if (StandalonePlugin.LyricsHasEntry("Display", "ShowHeader"))
		{
			media.Elements.Add(BoolToggle("SHOW HEADER", "Display", "ShowHeader"));
		}
		if (StandalonePlugin.LyricsHasEntry("Display", "AutoHideWhenIdle"))
		{
			media.Elements.Add(BoolToggle("AUTO HIDE IDLE", "Display", "AutoHideWhenIdle"));
		}
		if (StandalonePlugin.LyricsHasEntry("Display", "ShowOnTrackChangeSeconds"))
		{
			media.Elements.Add(FloatSlider("SHOW ON CHANGE", "Display", "ShowOnTrackChangeSeconds", 1f, 0f, 30f, "F0"));
		}
		Tabs.Add(media);
	}

	public override void RefreshPageUI()
	{
		BuildTabs();
	}

	private static void Refresh()
	{
		if (UtilMenuController.Instance != null)
		{
			UtilMenuController.Instance.RefreshUI();
		}
	}

	private static MenuElement BoolToggle(string label, string section, string key)
	{
		return new MenuElement(label, delegate
		{
			StandalonePlugin.LyricsToggleBool(section, key);
			Refresh();
		}, StandalonePlugin.LyricsGetBool(section, key));
	}

	private static MenuElement FloatSlider(string label, string section, string key, float step, float min, float max, string format)
	{
		string fmt = (format != null) ? format : "F2";
		MenuElement element = null;
		element = new MenuElement(label, StandalonePlugin.LyricsGetFloat(section, key).ToString(fmt), delegate
		{
			element.ValueText = StandalonePlugin.LyricsAdjustFloat(section, key, -step, min, max).ToString(fmt);
			Refresh();
		}, delegate
		{
			element.ValueText = StandalonePlugin.LyricsAdjustFloat(section, key, step, min, max).ToString(fmt);
			Refresh();
		});
		return element;
	}

	private static MenuElement IntSlider(string label, string section, string key, int min, int max)
	{
		MenuElement element = null;
		element = new MenuElement(label, StandalonePlugin.LyricsGetInt(section, key).ToString(), delegate
		{
			element.ValueText = StandalonePlugin.LyricsAdjustInt(section, key, -1, min, max).ToString();
			Refresh();
		}, delegate
		{
			element.ValueText = StandalonePlugin.LyricsAdjustInt(section, key, 1, min, max).ToString();
			Refresh();
		});
		return element;
	}

	private static MenuElement ColorSlider(string label, string section, string key, string[] names, string[] hex, bool keepAlpha)
	{
		MenuElement element = null;
		element = new MenuElement(label, StandalonePlugin.LyricsColorName(section, key, names, hex), delegate
		{
			element.ValueText = StandalonePlugin.LyricsCycleColor(section, key, names, hex, -1, keepAlpha);
			Refresh();
		}, delegate
		{
			element.ValueText = StandalonePlugin.LyricsCycleColor(section, key, names, hex, 1, keepAlpha);
			Refresh();
		});
		return element;
	}
}

public sealed partial class StandalonePlugin
{
	internal static readonly string[] LyTextNames = new string[5] { "WHITE", "CREAM", "MINT", "SKY", "PINK" };
	internal static readonly string[] LyTextHex = new string[5] { "#FFFFFF", "#FFF4D6", "#C8FFE0", "#BEE3FF", "#FFC8E8" };
	internal static readonly string[] LyAccentNames = new string[6] { "PURPLE", "PINK", "MINT", "ORANGE", "RED", "BLUE" };
	internal static readonly string[] LyAccentHex = new string[6] { "#B388FF", "#FF8AC2", "#5EEAD4", "#FFB454", "#FF6B6B", "#6EA8FF" };
	internal static readonly string[] LyBgNames = new string[5] { "DARK", "BLACK", "NAVY", "PLUM", "FOREST" };
	internal static readonly string[] LyBgHex = new string[5] { "#14141C", "#000000", "#0D1B2A", "#2A1033", "#0F2A1D" };

	private static BaseUnityPlugin LyricsInstance()
	{
		try
		{
			BepInEx.PluginInfo info;
			if (Chainloader.PluginInfos.TryGetValue("local.lyricshud", out info))
			{
				return info.Instance;
			}
		}
		catch
		{
		}
		return null;
	}

	internal static bool LyricsLoaded()
	{
		return LyricsInstance() != null;
	}

	private static ConfigEntryBase LyricsEntry(string section, string key)
	{
		try
		{
			BaseUnityPlugin plugin = LyricsInstance();
			return (plugin != null) ? plugin.Config[section, key] : null;
		}
		catch
		{
			return null;
		}
	}

	internal static bool LyricsHasEntry(string section, string key)
	{
		return LyricsEntry(section, key) != null;
	}

	internal static bool LyricsGetBool(string section, string key)
	{
		ConfigEntryBase entry = LyricsEntry(section, key);
		return entry != null && entry.BoxedValue is bool && (bool)entry.BoxedValue;
	}

	internal static void LyricsToggleBool(string section, string key)
	{
		ConfigEntryBase entry = LyricsEntry(section, key);
		if (entry != null && entry.BoxedValue is bool)
		{
			entry.BoxedValue = !(bool)entry.BoxedValue;
		}
	}

	internal static float LyricsGetFloat(string section, string key)
	{
		ConfigEntryBase entry = LyricsEntry(section, key);
		return (entry != null && entry.BoxedValue is float) ? (float)entry.BoxedValue : 0f;
	}

	internal static float LyricsAdjustFloat(string section, string key, float delta, float min, float max)
	{
		ConfigEntryBase entry = LyricsEntry(section, key);
		if (entry == null || !(entry.BoxedValue is float))
		{
			return 0f;
		}
		float value = Mathf.Clamp((float)Math.Round((float)entry.BoxedValue + delta, 2), min, max);
		entry.BoxedValue = value;
		return value;
	}

	internal static int LyricsGetInt(string section, string key)
	{
		ConfigEntryBase entry = LyricsEntry(section, key);
		return (entry != null && entry.BoxedValue is int) ? (int)entry.BoxedValue : 0;
	}

	internal static int LyricsAdjustInt(string section, string key, int delta, int min, int max)
	{
		ConfigEntryBase entry = LyricsEntry(section, key);
		if (entry == null || !(entry.BoxedValue is int))
		{
			return 0;
		}
		int value = Mathf.Clamp((int)entry.BoxedValue + delta, min, max);
		entry.BoxedValue = value;
		return value;
	}

	internal static void LyricsReset(string section, string key)
	{
		ConfigEntryBase entry = LyricsEntry(section, key);
		if (entry != null && entry.DefaultValue != null)
		{
			entry.BoxedValue = entry.DefaultValue;
		}
	}

	private static int LyricsColorIndex(string current, string[] hex)
	{
		string rgb = (current != null && current.Length >= 7) ? current.Substring(0, 7).ToUpperInvariant() : "";
		return Array.IndexOf(hex, rgb);
	}

	internal static string LyricsColorName(string section, string key, string[] names, string[] hex)
	{
		ConfigEntryBase entry = LyricsEntry(section, key);
		string current = (entry != null) ? (entry.BoxedValue as string) : null;
		int index = LyricsColorIndex(current, hex);
		return (index >= 0) ? names[index] : "CUSTOM";
	}

	internal static string LyricsCycleColor(string section, string key, string[] names, string[] hex, int step, bool keepAlpha)
	{
		ConfigEntryBase entry = LyricsEntry(section, key);
		if (entry == null)
		{
			return "?";
		}
		string current = (entry.BoxedValue as string) ?? "";
		int index = LyricsColorIndex(current, hex);
		index = (index < 0) ? 0 : ((index + step + hex.Length) % hex.Length);
		string alpha = "";
		if (keepAlpha)
		{
			alpha = (current.Length >= 9) ? current.Substring(7, 2) : "DC";
		}
		entry.BoxedValue = hex[index] + alpha;
		return names[index];
	}

	internal static int LyricsOpacity()
	{
		ConfigEntryBase entry = LyricsEntry("Look", "BackgroundColor");
		string current = (entry != null) ? (entry.BoxedValue as string) : null;
		if (current == null || current.Length < 9)
		{
			return 100;
		}
		int alpha;
		return int.TryParse(current.Substring(7, 2), System.Globalization.NumberStyles.HexNumber, null, out alpha) ? Mathf.RoundToInt(alpha * 100f / 255f) : 100;
	}

	internal static int LyricsAdjustOpacity(int delta)
	{
		ConfigEntryBase entry = LyricsEntry("Look", "BackgroundColor");
		if (entry == null)
		{
			return 0;
		}
		string current = (entry.BoxedValue as string) ?? "#14141C";
		string rgb = (current.Length >= 7) ? current.Substring(0, 7) : "#14141C";
		int percent = Mathf.Clamp(LyricsOpacity() + delta, 0, 100);
		entry.BoxedValue = rgb + Mathf.RoundToInt(percent * 255f / 100f).ToString("X2");
		return percent;
	}

	internal static bool LyricsGetVisible()
	{
		BaseUnityPlugin plugin = LyricsInstance();
		FieldInfo field = (plugin != null) ? plugin.GetType().GetField("visible", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
		return field != null && (bool)field.GetValue(plugin);
	}

	internal static void LyricsToggleVisible()
	{
		BaseUnityPlugin plugin = LyricsInstance();
		FieldInfo field = (plugin != null) ? plugin.GetType().GetField("visible", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
		if (field == null)
		{
			return;
		}
		bool now = !(bool)field.GetValue(plugin);
		field.SetValue(plugin, now);
		ConfigEntryBase start = LyricsEntry("General", "StartVisible");
		if (start != null && start.BoxedValue is bool)
		{
			start.BoxedValue = now;
		}
	}

	internal static void LyricsSend(string command)
	{
		BaseUnityPlugin plugin = LyricsInstance();
		MethodInfo send = (plugin != null) ? plugin.GetType().GetMethod("Send", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
		if (send != null)
		{
			send.Invoke(plugin, new object[] { command });
		}
	}
}
