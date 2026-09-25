using System.Collections.Generic;
using System.Linq;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed class HelpPage : BasePage
{
	private const int PerTab = 10;

	public override string PageName => "HELP";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.ClosedBook;

	public override void BuildTabs()
	{
		Tabs.Clear();
		for (int i = 0; i < StandalonePlugin.HelpCategoryNames.Length; i++)
		{
			UtilTab tab = new UtilTab
			{
				TabIcon = UtilMenuMain.Instance.Icons.ClosedBook,
				TabName = StandalonePlugin.HelpCategoryNames[i]
			};
			Dictionary<string, string> dict = StandalonePlugin.GetListDict(i);
			if (dict == null || dict.Count == 0)
			{
				tab.Elements.Add(new MenuElement("NOTHING KNOWN YET", delegate
				{
				}));
			}
			else
			{
				int shown = 0;
				int category = i;
				foreach (KeyValuePair<string, string> kvp in dict.OrderBy((KeyValuePair<string, string> p) => p.Value))
				{
					if (shown >= PerTab)
					{
						tab.Elements.Add(new MenuElement("+" + (dict.Count - shown) + " MORE, USE INFO PAGE LISTS", delegate
						{
						}));
						break;
					}
					string key = kvp.Key;
					bool selected = category == StandalonePlugin.SelectedHelpCategory && key == StandalonePlugin.SelectedHelpKey;
					string label = (selected ? "> " : "") + Shorten(kvp.Value, 30);
					tab.Elements.Add(new MenuElement(label, delegate
					{
						StandalonePlugin.SelectHelp(category, key);
						RefreshMenu();
					}));
					shown++;
				}
			}
			Tabs.Add(tab);
		}
		UtilTab info = new UtilTab
		{
			TabIcon = UtilMenuMain.Instance.Icons.ClosedBook,
			TabName = "Info"
		};
		if (StandalonePlugin.SelectedHelpKey == null)
		{
			info.Elements.Add(new MenuElement("PICK A MOD FIRST", delegate
			{
			}));
		}
		else
		{
			Dictionary<string, string> dict = StandalonePlugin.GetListDict(StandalonePlugin.SelectedHelpCategory);
			string label = (dict != null && dict.TryGetValue(StandalonePlugin.SelectedHelpKey, out string l)) ? l : "?";
			info.Elements.Add(new MenuElement("NAME: " + Shorten(label, 28), delegate
			{
			}));
			info.Elements.Add(new MenuElement("LIST: " + StandalonePlugin.HelpCategoryNames[StandalonePlugin.SelectedHelpCategory], delegate
			{
			}));
			info.Elements.Add(new MenuElement("KEY: " + Shorten(StandalonePlugin.SelectedHelpKey, 28), delegate
			{
			}));
			info.Elements.Add(new MenuElement("CLEAR SELECTION", delegate
			{
				StandalonePlugin.SelectHelp(-1, null);
				RefreshMenu();
			}));
		}
		Tabs.Add(info);
	}

	public override void RefreshPageUI()
	{
		BuildTabs();
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
