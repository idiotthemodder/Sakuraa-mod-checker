using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SakuraaCastingMod.Features.World;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed class MapLoaderPage : BasePage
{
	private const int PerTab = 6;
	private const int MaxTabs = 4;

	public override string PageName => "MAP LOADER";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.MoveArrows;

	public override void BuildTabs()
	{
		Tabs.Clear();
		List<KeyValuePair<string, object>> maps = StandalonePlugin.GetMapRegions();
		if (maps.Count == 0)
		{
			UtilTab empty = new UtilTab
			{
				TabIcon = UtilMenuMain.Instance.Icons.MoveArrows,
				TabName = "Maps"
			};
			empty.Elements.Add(new MenuElement("NO MAPS FOUND", delegate
			{
			}));
			empty.Elements.Add(new MenuElement("REOPEN THIS PAGE", delegate
			{
			}));
			Tabs.Add(empty);
			return;
		}
		for (int start = 0; start < maps.Count && Tabs.Count < MaxTabs; start += PerTab)
		{
			UtilTab tab = new UtilTab
			{
				TabIcon = UtilMenuMain.Instance.Icons.MoveArrows,
				TabName = "Maps " + (Tabs.Count + 1)
			};
			int end = Math.Min(start + PerTab, maps.Count);
			for (int i = start; i < end; i++)
			{
				string name = maps[i].Key;
				object trigger = maps[i].Value;
				string label = name.ToUpperInvariant();
				if (label.Length > 24)
				{
					label = label.Substring(0, 24);
				}
				tab.Elements.Add(new MenuElement(label, delegate
				{
					StandalonePlugin.LoadMap(name, trigger);
				}));
			}
			Tabs.Add(tab);
		}
		if (maps.Count > PerTab * MaxTabs)
		{
			StandalonePlugin.StatusLog("map page shows " + (PerTab * MaxTabs) + " of " + maps.Count + " maps");
		}
	}

	public override void RefreshPageUI()
	{
		BuildTabs();
	}
}

public sealed partial class StandalonePlugin
{
	private static int _mapsScanTick = -100000;
	private static List<KeyValuePair<string, object>> _mapsCache = new List<KeyValuePair<string, object>>();

	internal static List<KeyValuePair<string, object>> GetMapRegions()
	{
		if (Environment.TickCount - _mapsScanTick < 2000)
		{
			return _mapsCache;
		}
		_mapsScanTick = Environment.TickCount;
		List<KeyValuePair<string, object>> found = new List<KeyValuePair<string, object>>();
		try
		{
			Type world = typeof(WorldManager);
			BindingFlags all = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			MethodInfo check = world.GetMethod("CheckMaps", all);
			if (check != null)
			{
				check.Invoke(null, null);
			}
			FieldInfo regionField = world.GetField("RegionList", all);
			IList regions = (regionField != null) ? (regionField.GetValue(null) as IList) : null;
			if (regions != null)
			{
				foreach (object region in regions)
				{
					Component component = region as Component;
					if (component == null || component.gameObject == null)
					{
						continue;
					}
					string[] parts = component.gameObject.name.Split(new string[1] { "To" }, StringSplitOptions.None);
					if (parts.Length >= 2 && parts[1].Length > 0)
					{
						found.Add(new KeyValuePair<string, object>(parts[1], region));
					}
				}
			}
		}
		catch (Exception ex)
		{
			StatusLog("map scan failed: " + ex.Message);
		}
		_mapsCache = found;
		return found;
	}

	internal static void LoadMap(string name, object trigger)
	{
		try
		{
			MethodInfo box = trigger.GetType().GetMethod("OnBoxTriggered", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (box == null)
			{
				StatusLog("map trigger has no OnBoxTriggered: " + name);
				return;
			}
			box.Invoke(trigger, null);
			GameObject treeRoom = GameObject.Find("Environment Objects/LocalObjects_Prefab/TreeRoom");
			if (treeRoom != null)
			{
				treeRoom.SetActive(true);
			}
			StatusLog("loaded map " + name);
		}
		catch (Exception ex)
		{
			StatusLog("map load failed: " + ex.Message);
		}
	}
}
