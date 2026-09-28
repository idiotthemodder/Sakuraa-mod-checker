using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using SakuraaCastingMod.Shared.Helpers;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

// tts tabs that work off whoever you picked in the Lobby tab.
// hook it into TtsPage.BuildTabs with:   TtsPlayerTools.AddTabs(Tabs, Refresh);
// everything goes through TtsClient.Say, so it comes out of your mic like the rest of tts.
public static class TtsPlayerTools
{
	private static readonly string[] Friendly =
	{
		"hey {name}", "gg {name}", "nice one {name}", "thanks {name}", "sorry {name}", "welcome {name}"
	};

	private static readonly string[] Callouts =
	{
		"{name} is behind you", "follow me {name}", "come here {name}", "wait for me {name}", "watch out {name}"
	};

	private static readonly string[] Social =
	{
		"wanna party {name}", "add me {name}", "can you join my room {name}"
	};

	private static readonly string[] Compliments =
	{
		"{name} you are so good at this", "{name} nice moves", "{name} that was smooth", "{name} you are actually cracked"
	};

	private static FieldInfo _selectedField;

	public static void AddTabs(List<UtilTab> tabs, Action refresh)
	{
		string name = SelectedName();

		UtilTab say = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = "Player" };
		if (name == null)
		{
			say.Elements.Add(new MenuElement("PICK A PLAYER IN LOBBY", delegate { refresh(); }));
			say.Elements.Add(new MenuElement("THEN TAP HERE TO REFRESH", delegate { refresh(); }));
			tabs.Add(say);
			return;
		}

		say.Elements.Add(new MenuElement("PLAYER: " + Short(name).ToUpper(), delegate { refresh(); }));
		AddLines(say, Friendly);
		AddLines(say, Callouts);
		AddLines(say, Social);
		say.Elements.Add(new MenuElement("RANDOM COMPLIMENT", delegate
		{
			Speak(Compliments[UnityEngine.Random.Range(0, Compliments.Length)]);
		}));
		tabs.Add(say);

		UtilTab talk = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = "Mod Talk" };
		talk.Elements.Add(new MenuElement("SAY MOD COUNT", delegate { SayCounts(); }));
		talk.Elements.Add(new MenuElement("SAY THEIR CHEATS", delegate { SayCheats(); }));
		talk.Elements.Add(new MenuElement("SAY THEIR MODS", delegate { SayMods(); }));
		talk.Elements.Add(new MenuElement("SAY IF CLEAN", delegate { SayClean(); }));
		tabs.Add(talk);
	}

	private static void AddLines(UtilTab tab, string[] lines)
	{
		foreach (string line in lines)
		{
			string l = line;
			tab.Elements.Add(new MenuElement(l.Replace("{name}", "").Trim().ToUpper(), delegate { Speak(l); }));
		}
	}

	// speaks a template, filling {name} with whoever is selected right now
	private static void Speak(string template)
	{
		string name = SelectedName();
		if (name == null)
		{
			return;
		}
		TtsClient.Say(template.Replace("{name}", SpokenName(name)));
	}

	private static void Split(out List<string> cheats, out List<string> mods)
	{
		cheats = new List<string>();
		mods = new List<string>();
		NetPlayer player = SelectedPlayer();
		if (player == null)
		{
			return;
		}
		VRRig rig = PlayerTranslator.GetRigByNetPlayer(player);
		if (rig == null)
		{
			return;
		}
		// same rule the lobby panel uses: red entries are cheats, everything else is a mod
		foreach (string raw in LobbyPage.GetDetectedMods(rig))
		{
			string clean = Regex.Replace(raw, "<[^>]+>", "").Trim().ToLower();
			if (clean == "")
			{
				continue;
			}
			if (raw.Contains("red"))
			{
				cheats.Add(clean);
			}
			else
			{
				mods.Add(clean);
			}
		}
	}

	private static void SayCounts()
	{
		string name = SelectedName();
		if (name == null)
		{
			return;
		}
		Split(out List<string> cheats, out List<string> mods);
		TtsClient.Say(SpokenName(name) + " has " + Count(mods.Count, "mod") + " and " + Count(cheats.Count, "cheat"));
	}

	private static void SayCheats()
	{
		string name = SelectedName();
		if (name == null)
		{
			return;
		}
		Split(out List<string> cheats, out List<string> mods);
		if (cheats.Count == 0)
		{
			TtsClient.Say(SpokenName(name) + " has no cheats");
			return;
		}
		TtsClient.Say(SpokenName(name) + " has " + Count(cheats.Count, "cheat") + ": " + Join(cheats));
	}

	private static void SayMods()
	{
		string name = SelectedName();
		if (name == null)
		{
			return;
		}
		Split(out List<string> cheats, out List<string> mods);
		if (mods.Count == 0)
		{
			TtsClient.Say(SpokenName(name) + " has no mods");
			return;
		}
		TtsClient.Say(SpokenName(name) + " is using " + Join(mods));
	}

	private static void SayClean()
	{
		string name = SelectedName();
		if (name == null)
		{
			return;
		}
		Split(out List<string> cheats, out List<string> mods);
		TtsClient.Say(SpokenName(name) + (cheats.Count == 0 ? " looks clean" : " has cheats"));
	}

	private static string Count(int n, string word)
	{
		return n + " " + word + (n == 1 ? "" : "s");
	}

	// the helper caps text at 200 chars, so only read the first few names
	private static string Join(List<string> items)
	{
		int take = Math.Min(items.Count, 5);
		string s = string.Join(", ", items.GetRange(0, take));
		if (items.Count > take)
		{
			s += " and " + (items.Count - take) + " more";
		}
		return s;
	}

	private static NetPlayer SelectedPlayer()
	{
		try
		{
			BasePage lobby = UtilMenuController.Instance.Pages.Find((BasePage p) => p is LobbyPage);
			if (lobby == null)
			{
				return null;
			}
			if (_selectedField == null)
			{
				_selectedField = typeof(LobbyPage).GetField("_selectedPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
			}
			return _selectedField?.GetValue(lobby) as NetPlayer;
		}
		catch (Exception ex)
		{
			StandalonePlugin.StatusLog("tts player lookup failed: " + ex.Message);
			return null;
		}
	}

	private static string SelectedName()
	{
		NetPlayer p = SelectedPlayer();
		if (p == null || string.IsNullOrEmpty(p.NickName))
		{
			return null;
		}
		return p.NickName;
	}

	// nicknames are things like "STEVE_123", tidy them up so the voice can actually say them
	private static string SpokenName(string nick)
	{
		string s = Regex.Replace(nick, "<[^>]+>", "");
		s = Regex.Replace(s, "[^A-Za-z0-9 ]", " ");
		s = Regex.Replace(s, "\\s+", " ").Trim().ToLower();
		return s == "" ? "player" : s;
	}

	private static string Short(string s)
	{
		return s.Length <= 14 ? s : s.Substring(0, 14);
	}
}
