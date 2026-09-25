using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed class UnknownPage : BasePage
{
	private const int PerTab = 6;

	public override string PageName => "UNKNOWN";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.MagnifyingGlass;

	public override void BuildTabs()
	{
		Tabs.Clear();
		List<StandalonePlugin.UnknownItem> items = StandalonePlugin.ReadUnknownItems();
		int tabCount = 0;
		for (int start = 0; start < items.Count && tabCount < 2; start += PerTab)
		{
			UtilTab tab = new UtilTab
			{
				TabIcon = UtilMenuMain.Instance.Icons.MagnifyingGlass,
				TabName = (tabCount == 0) ? "Newest" : "Older"
			};
			int end = Math.Min(start + PerTab, items.Count);
			for (int i = start; i < end; i++)
			{
				StandalonePlugin.UnknownItem item = items[i];
				bool selected = StandalonePlugin.SelectedUnknown == item.Key;
				string label = (selected ? "> " : "") + Shorten(item.Key, 30);
				tab.Elements.Add(new MenuElement(label, delegate
				{
					StandalonePlugin.SelectUnknown(item.Key);
					RefreshMenu();
				}));
			}
			Tabs.Add(tab);
			tabCount++;
		}
		if (items.Count == 0)
		{
			UtilTab empty = new UtilTab
			{
				TabIcon = UtilMenuMain.Instance.Icons.MagnifyingGlass,
				TabName = "Newest"
			};
			empty.Elements.Add(new MenuElement("NOTHING NEW YET", delegate
			{
			}));
			Tabs.Add(empty);
		}
		UtilTab file = new UtilTab
		{
			TabIcon = UtilMenuMain.Instance.Icons.MagnifyingGlass,
			TabName = "File"
		};
		string sel = StandalonePlugin.SelectedUnknown;
		file.Elements.Add(new MenuElement((sel == null) ? "PICK A PROPERTY FIRST" : "SEL: " + Shorten(sel, 28), delegate
		{
		}));
		AddAction(file, "MOD");
		AddAction(file, "CHEAT");
		AddAction(file, "UNSURE");
		file.Elements.Add(new MenuElement("CLEAR SELECTION", delegate
		{
			StandalonePlugin.SelectUnknown(null);
			RefreshMenu();
		}));
		Tabs.Add(file);
	}

	public override void RefreshPageUI()
	{
		BuildTabs();
	}

	private static void AddAction(UtilTab tab, string kind)
	{
		string text = StandalonePlugin.IsArmed(kind) ? ("CONFIRM " + kind + "?") : kind;
		tab.Elements.Add(new MenuElement(text, delegate
		{
			StandalonePlugin.PressFileButton(kind);
			RefreshMenu();
		}));
	}

	private static string Shorten(string s, int max)
	{
		if (s == null)
		{
			return "";
		}
		return (s.Length > max) ? (s.Substring(0, max - 3) + "...") : s;
	}

	private static void RefreshMenu()
	{
		if (UtilMenuController.Instance != null)
		{
			UtilMenuController.Instance.RefreshUI();
		}
	}
}

public sealed partial class StandalonePlugin
{
	internal sealed class UnknownItem
	{
		public string Key;
		public string Seen;
		public string Value;
	}

	internal static string SelectedUnknown;
	private static string _armedKind;
	private static int _armedAt;
	private static List<UnknownItem> _unknownCache;
	private static int _unknownCacheTick = -100000;

	internal static List<UnknownItem> ReadUnknownItems()
	{
		if (_unknownCache != null && Environment.TickCount - _unknownCacheTick < 1500)
		{
			return _unknownCache;
		}
		List<UnknownItem> items = new List<UnknownItem>();
		try
		{
			string path = Path.Combine(Paths.ConfigPath, "sakuraa_unknown_props.txt");
			if (File.Exists(path))
			{
				string seen = "?";
				string value = "";
				foreach (string raw in File.ReadAllLines(path))
				{
					string line = raw.TrimEnd();
					if (line.StartsWith("# first seen"))
					{
						int bar = line.IndexOf(" | value: ");
						seen = (bar > 12) ? line.Substring(12, bar - 12).Trim() : "?";
						value = (bar > 0) ? line.Substring(bar + 10) : "";
						continue;
					}
					if (line.Length == 0 || line.StartsWith("#"))
					{
						continue;
					}
					int eq = line.LastIndexOf('=');
					if (eq > 0)
					{
						items.Add(new UnknownItem { Key = line.Substring(0, eq), Seen = seen, Value = value });
					}
				}
			}
		}
		catch
		{
		}
		items.Reverse();
		_unknownCache = items;
		_unknownCacheTick = Environment.TickCount;
		return items;
	}

	internal static void SelectUnknown(string key)
	{
		SelectedUnknown = key;
		_armedKind = null;
	}

	internal static bool IsArmed(string kind)
	{
		return _armedKind == kind && Environment.TickCount - _armedAt < 6000;
	}

	internal static void PressFileButton(string kind)
	{
		if (string.IsNullOrEmpty(SelectedUnknown))
		{
			return;
		}
		if (!IsArmed(kind))
		{
			_armedKind = kind;
			_armedAt = Environment.TickCount;
			return;
		}
		_armedKind = null;
		FileUnknown(kind);
	}

	private static void FileUnknown(string kind)
	{
		string key = SelectedUnknown;
		try
		{
			if (key.Contains("=") || key.Contains(" #") || key.Contains("\\n") || key.Contains("\\r"))
			{
				StatusLog("cannot file this key (has = or # or a line break): " + key);
				return;
			}
			string fileName = (kind == "MOD") ? "sakuraa_known_mods.txt" : ((kind == "CHEAT") ? "sakuraa_known_cheats.txt" : "sakuraa_known_unsure.txt");
			string path = Path.Combine(Paths.ConfigPath, fileName);
			string label = key.ToUpperInvariant();
			if (label.Length > 24)
			{
				label = label.Substring(0, 24);
			}
			string seen = "?";
			foreach (UnknownItem item in ReadUnknownItems())
			{
				if (item.Key == key)
				{
					seen = item.Seen;
					break;
				}
			}
			string existing = File.Exists(path) ? File.ReadAllText(path) : "";
			string prefix = (existing.Length > 0 && !existing.EndsWith("\n")) ? Environment.NewLine : "";
			File.AppendAllText(path, prefix + key + "=" + label + " # first seen " + seen + Environment.NewLine);
			RemoveUnknown(key);
			StatusLog("filed " + key + " as " + kind);
			SelectedUnknown = null;
			_unknownCacheTick = -100000;
		}
		catch (Exception ex)
		{
			StatusLog("filing failed: " + ex.Message);
		}
	}

	private static void RemoveUnknown(string key)
	{
		string path = Path.Combine(Paths.ConfigPath, "sakuraa_unknown_props.txt");
		if (!File.Exists(path))
		{
			return;
		}
		string[] lines = File.ReadAllLines(path);
		List<string> kept = new List<string>();
		bool removed = false;
		int i = 0;
		while (i < lines.Length)
		{
			if (!removed && lines[i].StartsWith("# first seen") && i + 1 < lines.Length)
			{
				string next = lines[i + 1].TrimEnd();
				int eq = next.LastIndexOf('=');
				if (eq > 0 && next.Substring(0, eq) == key)
				{
					removed = true;
					i += 2;
					continue;
				}
			}
			kept.Add(lines[i]);
			i++;
		}
		if (removed)
		{
			File.WriteAllLines(path, kept.ToArray());
		}
	}
}
