using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using SakuraaCastingMod.Core;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using SakuraaCastingMod.VR.UtilMenu.Utility;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed class ProfilesPage : BasePage
{
	private const int PerTab = 6;

	public override string PageName => "PROFILES";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.Server;

	public override void BuildTabs()
	{
		Tabs.Clear();
		List<StandalonePlugin.ProfileInfo> profiles = StandalonePlugin.ListProfiles();
		if (profiles.Count == 0)
		{
			UtilTab empty = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = "Profiles" };
			empty.Elements.Add(new MenuElement("NO PROFILES SAVED YET", delegate
			{
			}));
			Tabs.Add(empty);
		}
		else
		{
			for (int start = 0; start < profiles.Count; start += PerTab)
			{
				UtilTab tab = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = "Profiles " + (Tabs.Count + 1) };
				int end = Math.Min(start + PerTab, profiles.Count);
				for (int i = start; i < end; i++)
				{
					StandalonePlugin.ProfileInfo p = profiles[i];
					bool selected = StandalonePlugin.SelectedProfile == p.Id;
					string label = (selected ? "> " : "") + p.Name;
					tab.Elements.Add(new MenuElement(label, delegate
					{
						StandalonePlugin.SelectProfile(p.Id);
						Refresh();
					}));
				}
				Tabs.Add(tab);
			}
		}

		UtilTab actions = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = "Actions" };
		string sel = StandalonePlugin.SelectedProfile;
		actions.Elements.Add(new MenuElement((sel == null) ? "NOTHING SELECTED" : "SELECTED: " + StandalonePlugin.SelectedProfileName(), delegate
		{
		}));
		actions.Elements.Add(new MenuElement("SAVE CURRENT AS NEW", delegate
		{
			StandalonePlugin.SaveProfile();
			Refresh();
		}));
		AddAction(actions, "LOAD", "LOAD SELECTED");
		AddAction(actions, "DELETE", "DELETE SELECTED");
		actions.Elements.Add(new MenuElement("CLEAR SELECTION", delegate
		{
			StandalonePlugin.SelectProfile(null);
			Refresh();
		}));
		Tabs.Add(actions);
	}

	private static void AddAction(UtilTab tab, string kind, string label)
	{
		string text = StandalonePlugin.IsProfileArmed(kind) ? ("CONFIRM " + label + "?") : label;
		tab.Elements.Add(new MenuElement(text, delegate
		{
			StandalonePlugin.PressProfileButton(kind);
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

public sealed partial class StandalonePlugin
{
	internal sealed class ProfileInfo
	{
		public string Id;
		public string Name;
	}

	internal static string SelectedProfile;
	private static string _profileArmedKind;
	private static int _profileArmedAt;
	private static List<ProfileInfo> _profileCache;
	private static int _profileCacheTick = -100000;

	private static string ProfilesDir => Path.Combine(Paths.ConfigPath, "SakuraaProfiles");

	private static string SettingsFilePath => Path.Combine(Paths.ConfigPath, "SakuraaCameraClient", "CamModSettingsSave.json");

	internal static List<ProfileInfo> ListProfiles()
	{
		if (_profileCache != null && Environment.TickCount - _profileCacheTick < 1500)
		{
			return _profileCache;
		}
		List<ProfileInfo> result = new List<ProfileInfo>();
		try
		{
			if (Directory.Exists(ProfilesDir))
			{
				string[] dirs = Directory.GetDirectories(ProfilesDir);
				Array.Sort(dirs);
				foreach (string dir in dirs)
				{
					string id = Path.GetFileName(dir);
					string name = id;
					string metaPath = Path.Combine(dir, "meta.txt");
					if (File.Exists(metaPath))
					{
						foreach (string raw in File.ReadAllLines(metaPath))
						{
							if (raw.StartsWith("Name="))
							{
								name = raw.Substring(5).Trim();
							}
						}
					}
					result.Add(new ProfileInfo { Id = id, Name = name });
				}
			}
		}
		catch (Exception ex)
		{
			StatusLog("list profiles failed: " + ex.Message);
		}
		_profileCache = result;
		_profileCacheTick = Environment.TickCount;
		return result;
	}

	internal static void SelectProfile(string id)
	{
		SelectedProfile = id;
		_profileArmedKind = null;
	}

	internal static string SelectedProfileName()
	{
		foreach (ProfileInfo p in ListProfiles())
		{
			if (p.Id == SelectedProfile)
			{
				return p.Name;
			}
		}
		return SelectedProfile;
	}

	internal static bool IsProfileArmed(string kind)
	{
		return _profileArmedKind == kind && Environment.TickCount - _profileArmedAt < 6000;
	}

	internal static void PressProfileButton(string kind)
	{
		if (kind != "LOAD" && kind != "DELETE")
		{
			return;
		}
		if (string.IsNullOrEmpty(SelectedProfile))
		{
			return;
		}
		if (!IsProfileArmed(kind))
		{
			_profileArmedKind = kind;
			_profileArmedAt = Environment.TickCount;
			return;
		}
		_profileArmedKind = null;
		if (kind == "LOAD")
		{
			LoadProfile(SelectedProfile);
		}
		else
		{
			DeleteProfile(SelectedProfile);
		}
	}

	private static void CopyIfExists(string src, string dst)
	{
		if (File.Exists(src))
		{
			File.Copy(src, dst, true);
		}
	}

	internal static void SaveProfile()
	{
		try
		{
			Directory.CreateDirectory(ProfilesDir);
			string id = "profile_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			string dir = Path.Combine(ProfilesDir, id);
			Directory.CreateDirectory(dir);
			int count = ListProfiles().Count + 1;
			CopyIfExists(SettingsFilePath, Path.Combine(dir, "CamModSettingsSave.json"));
			CopyIfExists(Path.Combine(Paths.ConfigPath, "sakuraa_nametags.txt"), Path.Combine(dir, "sakuraa_nametags.txt"));
			CopyIfExists(Path.Combine(Paths.ConfigPath, "sakuraa_theme.txt"), Path.Combine(dir, "sakuraa_theme.txt"));
			File.WriteAllLines(Path.Combine(dir, "meta.txt"), new string[2]
			{
				"Name=PROFILE " + count,
				"Created=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm")
			});
			_profileCacheTick = -100000;
			StatusLog("profile saved: PROFILE " + count);
		}
		catch (Exception ex)
		{
			StatusLog("profile save failed: " + ex.Message);
		}
	}

	internal static void LoadProfile(string id)
	{
		try
		{
			string dir = Path.Combine(ProfilesDir, id);
			if (!Directory.Exists(dir))
			{
				StatusLog("profile load failed: not found " + id);
				return;
			}
			string settingsSrc = Path.Combine(dir, "CamModSettingsSave.json");
			if (File.Exists(settingsSrc))
			{
				string json = File.ReadAllText(settingsSrc);
				Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath));
				File.WriteAllText(SettingsFilePath, json);
				Configuration.LoadFromJson(json);
			}
			string namePath = Path.Combine(dir, "sakuraa_nametags.txt");
			if (File.Exists(namePath))
			{
				File.Copy(namePath, Path.Combine(Paths.ConfigPath, "sakuraa_nametags.txt"), true);
				LoadTagSettings();
			}
			string themePath = Path.Combine(dir, "sakuraa_theme.txt");
			if (File.Exists(themePath))
			{
				File.Copy(themePath, Path.Combine(Paths.ConfigPath, "sakuraa_theme.txt"), true);
				LoadThemeChoice();
			}
			try
			{
				ThemeManager.RefreshTheme();
			}
			catch
			{
			}
			if (UtilMenuController.Instance != null)
			{
				UtilMenuController.Instance.RefreshUI();
			}
			StatusLog("profile loaded: " + id);
		}
		catch (Exception ex)
		{
			StatusLog("profile load failed: " + ex.Message);
		}
	}

	internal static void DeleteProfile(string id)
	{
		try
		{
			string dir = Path.Combine(ProfilesDir, id);
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
			}
			if (SelectedProfile == id)
			{
				SelectedProfile = null;
			}
			_profileCacheTick = -100000;
			StatusLog("profile deleted: " + id);
		}
		catch (Exception ex)
		{
			StatusLog("profile delete failed: " + ex.Message);
		}
	}
}
