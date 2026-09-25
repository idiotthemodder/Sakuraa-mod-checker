using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed class AboutPage : BasePage
{
	private const int PerTab = 6;

	public override string PageName => "ABOUT";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.Info;

	public override void BuildTabs()
	{
		Tabs.Clear();
		UtilTab about = new UtilTab
		{
			TabIcon = UtilMenuMain.Instance.Icons.Info,
			TabName = "About"
		};
		foreach (string line in StandalonePlugin.AboutLines())
		{
			about.Elements.Add(new MenuElement(line, delegate
			{
			}));
		}
		Tabs.Add(about);
		List<string> shown = StandalonePlugin.ModNames();
		int maxShown = PerTab * 3;
		if (shown.Count > maxShown)
		{
			int hidden = shown.Count - (maxShown - 1);
			shown = shown.GetRange(0, maxShown - 1);
			shown.Add("+" + hidden + " MORE");
		}
		for (int start = 0; start < shown.Count; start += PerTab)
		{
			UtilTab tab = new UtilTab
			{
				TabIcon = UtilMenuMain.Instance.Icons.HorizontalBarList,
				TabName = "Mods " + Tabs.Count
			};
			int end = Math.Min(start + PerTab, shown.Count);
			for (int i = start; i < end; i++)
			{
				tab.Elements.Add(new MenuElement(shown[i], delegate
				{
				}));
			}
			Tabs.Add(tab);
		}
	}

	public override void RefreshPageUI()
	{
		BuildTabs();
	}
}

public sealed partial class StandalonePlugin
{
	private static string DateText(string path)
	{
		try
		{
			return (!string.IsNullOrEmpty(path) && File.Exists(path)) ? File.GetLastWriteTime(path).ToString("yyyy-MM-dd") : "?";
		}
		catch
		{
			return "?";
		}
	}

	internal static string[] AboutLines()
	{
		string gameFile = "?";
		try
		{
			string dir = Path.GetDirectoryName(Application.dataPath);
			gameFile = DateText(Path.Combine(dir, Application.productName + ".exe"));
			if (gameFile == "?")
			{
				gameFile = DateText(Path.Combine(dir, "Gorilla Tag.exe"));
			}
		}
		catch
		{
		}
		string menu = "?";
		try
		{
			Type info = typeof(BasePage).Assembly.GetType("SakuraaCastingMod.Core.PluginInfo");
			FieldInfo field = (info != null) ? info.GetField("Version", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null;
			object value = (field != null) ? field.GetRawConstantValue() : null;
			if (value != null)
			{
				menu = value.ToString();
			}
		}
		catch
		{
		}
		string built = "?";
		int count = 0;
		try
		{
			count = Chainloader.PluginInfos.Count;
			BepInEx.PluginInfo self;
			if (Chainloader.PluginInfos.TryGetValue("com.dusted.sakuraa.standalone", out self))
			{
				built = DateText(self.Location);
			}
		}
		catch
		{
		}
		return new string[6]
		{
			"GAME " + Application.version,
			"GAME FILE " + gameFile,
			"UNITY " + Application.unityVersion,
			"MODS LOADED: " + count,
			"SAKURAA v" + menu,
			"BUILT " + built
		};
	}

	internal static List<string> ModNames()
	{
		List<string> names = new List<string>();
		try
		{
			foreach (BepInEx.PluginInfo plugin in Chainloader.PluginInfos.Values)
			{
				string name = plugin.Metadata.Name;
				if (string.IsNullOrEmpty(name))
				{
					continue;
				}
				string line = (name + " " + plugin.Metadata.Version).ToUpperInvariant();
				names.Add((line.Length > 28) ? line.Substring(0, 28) : line);
			}
			names.Sort(StringComparer.Ordinal);
		}
		catch
		{
		}
		return names;
	}
}
