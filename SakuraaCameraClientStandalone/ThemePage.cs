using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using SakuraaCastingMod.VR.UtilMenu.Utility;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed class ThemePage : BasePage
{
	public override string PageName => "THEME";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.Paintbrush;

	public override void BuildTabs()
	{
		Tabs.Clear();
		UtilTab dark = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Paintbrush, TabName = "Dark" };
		for (int i = 0; i < StandalonePlugin.ThemeDefs.Length; i++)
		{
			if (!StandalonePlugin.ThemeDefs[i].Light)
			{
				AddThemeButton(dark, i);
			}
		}
		dark.Elements.Add(new MenuElement("SWITCH TO LIGHT", delegate
		{
			StandalonePlugin.ApplyLastForMode(true);
			Refresh();
		}));
		Tabs.Add(dark);

		UtilTab light = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Paintbrush, TabName = "Light" };
		for (int i = 0; i < StandalonePlugin.ThemeDefs.Length; i++)
		{
			if (StandalonePlugin.ThemeDefs[i].Light)
			{
				AddThemeButton(light, i);
			}
		}
		light.Elements.Add(new MenuElement("SWITCH TO DARK", delegate
		{
			StandalonePlugin.ApplyLastForMode(false);
			Refresh();
		}));
		Tabs.Add(light);

		UtilTab extras = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Paintbrush, TabName = "Extras" };
		extras.Elements.Add(new MenuElement("RGB MODE", delegate
		{
			StandalonePlugin.ToggleRgbMode();
			Refresh();
		}, StandalonePlugin.RgbMode));
		extras.Elements.Add(new MenuElement("CURRENT: " + StandalonePlugin.CurrentThemeName, delegate
		{
		}));
		Tabs.Add(extras);
	}

	private static void AddThemeButton(UtilTab tab, int index)
	{
		string name = StandalonePlugin.ThemeDefs[index].Name;
		tab.Elements.Add(new MenuElement(name, delegate
		{
			StandalonePlugin.ApplyTheme(index);
			Refresh();
		}));
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
}

internal sealed class RgbTicker : MonoBehaviour
{
	private float _next;

	private void Update()
	{
		if (!StandalonePlugin.RgbMode || Time.unscaledTime < _next)
		{
			return;
		}
		_next = Time.unscaledTime + 0.05f;
		StandalonePlugin.RgbTick();
	}
}

public sealed partial class StandalonePlugin
{
	internal struct ThemeDef
	{
		public string Name;
		public bool Light;
		public Color Button, Pressed, Inner, Selected, Panel, Text1, Text2;
	}

	internal static readonly ThemeDef[] ThemeDefs = new ThemeDef[10]
	{
		new ThemeDef { Name = "MIDNIGHT VIOLET", Light = false, Button = new Color(0.25f,0.15f,0.45f), Pressed = new Color(0.45f,0.25f,0.75f), Inner = new Color(0.08f,0.06f,0.12f), Selected = new Color(0.55f,0.35f,0.9f), Panel = new Color(0.04f,0.03f,0.07f), Text1 = Color.white, Text2 = new Color(0.75f,0.6f,0.95f) },
		new ThemeDef { Name = "DEEP PLUM", Light = false, Button = new Color(0.3f,0.12f,0.28f), Pressed = new Color(0.5f,0.2f,0.45f), Inner = new Color(0.1f,0.05f,0.1f), Selected = new Color(0.6f,0.3f,0.55f), Panel = new Color(0.06f,0.03f,0.06f), Text1 = Color.white, Text2 = new Color(0.9f,0.65f,0.85f) },
		new ThemeDef { Name = "OBSIDIAN", Light = false, Button = new Color(0.15f,0.15f,0.15f), Pressed = new Color(0.3f,0.3f,0.3f), Inner = new Color(0.05f,0.05f,0.05f), Selected = new Color(0.45f,0.45f,0.45f), Panel = new Color(0.02f,0.02f,0.02f), Text1 = Color.white, Text2 = new Color(0.7f,0.7f,0.7f) },
		new ThemeDef { Name = "GRAPE SODA", Light = false, Button = new Color(0.35f,0.1f,0.4f), Pressed = new Color(0.6f,0.2f,0.55f), Inner = new Color(0.12f,0.04f,0.14f), Selected = new Color(0.85f,0.3f,0.75f), Panel = new Color(0.05f,0.02f,0.06f), Text1 = Color.white, Text2 = new Color(1f,0.55f,0.85f) },
		new ThemeDef { Name = "VOID MINT", Light = false, Button = new Color(0.08f,0.3f,0.28f), Pressed = new Color(0.15f,0.55f,0.5f), Inner = new Color(0.04f,0.08f,0.08f), Selected = new Color(0.25f,0.85f,0.7f), Panel = new Color(0.02f,0.04f,0.04f), Text1 = Color.white, Text2 = new Color(0.6f,1f,0.9f) },
		new ThemeDef { Name = "MOONLIGHT", Light = true, Button = new Color(0.85f,0.8f,0.95f), Pressed = new Color(0.7f,0.6f,0.9f), Inner = new Color(0.95f,0.93f,0.98f), Selected = new Color(0.6f,0.45f,0.85f), Panel = new Color(0.97f,0.96f,1f), Text1 = new Color(0.2f,0.12f,0.3f), Text2 = new Color(0.45f,0.3f,0.6f) },
		new ThemeDef { Name = "COTTON CANDY", Light = true, Button = new Color(0.95f,0.8f,0.9f), Pressed = new Color(0.8f,0.65f,0.95f), Inner = new Color(0.98f,0.92f,0.96f), Selected = new Color(0.65f,0.85f,0.98f), Panel = new Color(0.99f,0.96f,0.98f), Text1 = new Color(0.25f,0.2f,0.3f), Text2 = new Color(0.5f,0.4f,0.6f) },
		new ThemeDef { Name = "VANILLA", Light = true, Button = new Color(0.93f,0.85f,0.7f), Pressed = new Color(0.85f,0.7f,0.5f), Inner = new Color(0.97f,0.92f,0.82f), Selected = new Color(0.7f,0.5f,0.35f), Panel = new Color(0.98f,0.94f,0.86f), Text1 = new Color(0.3f,0.22f,0.15f), Text2 = new Color(0.55f,0.4f,0.25f) },
		new ThemeDef { Name = "PAPER", Light = true, Button = new Color(0.92f,0.92f,0.92f), Pressed = new Color(0.8f,0.8f,0.8f), Inner = new Color(0.97f,0.97f,0.97f), Selected = new Color(0.4f,0.4f,0.4f), Panel = new Color(0.99f,0.99f,0.99f), Text1 = new Color(0.05f,0.05f,0.05f), Text2 = new Color(0.4f,0.4f,0.4f) },
		new ThemeDef { Name = "PEACH FIZZ", Light = true, Button = new Color(0.98f,0.78f,0.65f), Pressed = new Color(0.95f,0.6f,0.45f), Inner = new Color(0.99f,0.9f,0.83f), Selected = new Color(0.95f,0.45f,0.4f), Panel = new Color(0.995f,0.94f,0.9f), Text1 = new Color(0.35f,0.2f,0.15f), Text2 = new Color(0.6f,0.35f,0.25f) }
	};

	private const int ThemeSlot = 0;
	internal static bool RgbMode;
	internal static string CurrentThemeName = "DEFAULT";
	private static int _lastDarkIndex;
	private static int _lastLightIndex = 5;
	private static Type _themeMgrType;
	private static float _rgbHue;

	private static string ThemeSettingsPath => Path.Combine(Paths.ConfigPath, "sakuraa_theme.txt");

	private static void StartThemeExtras()
	{
		try
		{
			_themeMgrType = typeof(ThemeManager);
			LoadThemeChoice();
			GameObject go = new GameObject("SakuraaRgbTicker");
			UnityEngine.Object.DontDestroyOnLoad(go);
			go.AddComponent<RgbTicker>();
			StatusLog("theme extras installed");
		}
		catch (Exception ex)
		{
			StatusLog("theme extras failed: " + ex.Message);
		}
	}

	private static void LoadThemeChoice()
	{
		try
		{
			if (!File.Exists(ThemeSettingsPath))
			{
				return;
			}
			foreach (string raw in File.ReadAllLines(ThemeSettingsPath))
			{
				int eq = raw.IndexOf('=');
				if (eq <= 0)
				{
					continue;
				}
				string key = raw.Substring(0, eq).Trim();
				string val = raw.Substring(eq + 1).Trim();
				int parsed;
				if (key == "LastDark" && int.TryParse(val, out parsed))
				{
					_lastDarkIndex = parsed;
				}
				else if (key == "LastLight" && int.TryParse(val, out parsed))
				{
					_lastLightIndex = parsed;
				}
				else if (key == "Current")
				{
					CurrentThemeName = val;
				}
			}
		}
		catch (Exception ex)
		{
			StatusLog("theme choice load failed: " + ex.Message);
		}
	}

	private static void SaveThemeChoice()
	{
		try
		{
			File.WriteAllLines(ThemeSettingsPath, new string[3]
			{
				"LastDark=" + _lastDarkIndex,
				"LastLight=" + _lastLightIndex,
				"Current=" + CurrentThemeName
			});
		}
		catch (Exception ex)
		{
			StatusLog("theme choice save failed: " + ex.Message);
		}
	}

	internal static void ApplyTheme(int index)
	{
		try
		{
			ThemeDef t = ThemeDefs[index];
			ThemeManager.ActiveCustomSlot = ThemeSlot;
			ThemeManager.IsCustomThemeEnabled = true;
			ThemeManager.UpdateColor("BUTTON", t.Button);
			ThemeManager.UpdateColor("PRESSED", t.Pressed);
			ThemeManager.UpdateColor("INNER", t.Inner);
			ThemeManager.UpdateColor("SELECTED", t.Selected);
			ThemeManager.UpdateColor("PANEL", t.Panel);
			ThemeManager.UpdateColor("TEXT 1", t.Text1);
			ThemeManager.UpdateColor("TEXT 2", t.Text2);
			ThemeManager.CurrentThemeIndex = ThemeManager.CustomSlotThemeIndex(ThemeSlot);
			ThemeManager.RefreshTheme();
			CurrentThemeName = t.Name;
			if (t.Light)
			{
				_lastLightIndex = index;
			}
			else
			{
				_lastDarkIndex = index;
			}
			SaveThemeChoice();
			StatusLog("theme applied: " + t.Name);
		}
		catch (Exception ex)
		{
			StatusLog("theme apply failed: " + ex.Message);
		}
	}

	internal static void ApplyLastForMode(bool light)
	{
		ApplyTheme(light ? _lastLightIndex : _lastDarkIndex);
	}

	internal static void ToggleRgbMode()
	{
		RgbMode = !RgbMode;
		if (!RgbMode)
		{
			try
			{
				ThemeManager.RefreshTheme();
			}
			catch
			{
			}
		}
	}

	internal static void RgbTick()
	{
		try
		{
			BindingFlags all = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			FieldInfo matsField = _themeMgrType.GetField("_materials", all);
			Dictionary<string, Material> mats = (matsField != null) ? (matsField.GetValue(null) as Dictionary<string, Material>) : null;
			if (mats == null)
			{
				return;
			}
			_rgbHue = (_rgbHue + 0.01f) % 1f;
			string[] keys = new string[4] { "BUTTON", "PANEL", "SELECTED", "PRESSED" };
			for (int i = 0; i < keys.Length; i++)
			{
				Material m;
				if (mats.TryGetValue(keys[i], out m) && m != null)
				{
					float hue = (_rgbHue + i * 0.08f) % 1f;
					m.color = Color.HSVToRGB(hue, 0.85f, (keys[i] == "PANEL") ? 0.35f : 1f);
				}
			}
		}
		catch
		{
		}
	}
}
