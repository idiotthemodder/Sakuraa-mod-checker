using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SakuraaCastingMod.Features.Soundboard;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using SakuraaCastingMod.VR.UtilMenu.Utility;

namespace SakuraaCameraClientStandalone;

// replaces the Lobby page's old "Gun" tab (crosshair icon, it just duplicated the
// "SELECT GUN" button already in the Leaderboard tab) with a TTS tab, in the exact same
// slot. a tab inside another page can't have its own sub-tabs, so this is one flat tab
// with its own tiny menu: top level -> Player / Settings / Premade -> (for Player and
// Premade) a category -> lines. everything renders as buttons in that one tab's Elements
// list, rebuilt in place whenever you tap something.
public static class LobbyGunPatch
{
	private sealed class Entry
	{
		public string Label;

		public Action Run;

		public Entry(string label, Action run)
		{
			Label = label;
			Run = run;
		}
	}

	private sealed class Category
	{
		public string Name;

		public List<Entry> Entries = new List<Entry>();

		public Category(string name)
		{
			Name = name;
		}
	}

	private static readonly string[][] PremadeFriendly =
	{
		new[] { "HELLO", "hello everyone" },
		new[] { "GG", "good game" },
		new[] { "GOOD LUCK", "good luck everyone" },
		new[] { "WELL PLAYED", "well played" },
		new[] { "NICE ONE", "nice one" },
		new[] { "THANK YOU", "thank you" },
		new[] { "SEE YA", "see you later everyone" },
		new[] { "BYE", "goodbye everyone" }
	};

	private static readonly string[][] PremadeGame =
	{
		new[] { "CLOSE ONE", "that was close" },
		new[] { "GOOD TRY", "good try" },
		new[] { "YOU GOT ME", "you got me" },
		new[] { "ONE SECOND", "one second please" },
		new[] { "SORRY", "sorry about that" },
		new[] { "LAGGING", "my game is lagging" },
		new[] { "THAT WAS FUN", "that was fun" },
		new[] { "PLAY AGAIN", "let's play again" }
	};

	private static readonly string[][] PremadeCallouts =
	{
		new[] { "I AM IT", "i am it" },
		new[] { "WHO IS IT", "who is it" },
		new[] { "BEHIND YOU", "someone is behind you" },
		new[] { "FOLLOW ME", "follow me" },
		new[] { "WAIT FOR ME", "wait for me" },
		new[] { "WATCH OUT", "watch out" },
		new[] { "OVER HERE", "i am over here" },
		new[] { "HELP", "help me please" }
	};

	private static readonly string[][] PremadeCompliments =
	{
		new[] { "NICE MOVES", "nice moves" },
		new[] { "SO GOOD", "you are really good" },
		new[] { "SMOOTH", "that was smooth" },
		new[] { "GREAT JOB", "great job everyone" },
		new[] { "COOL STYLE", "i love your style" },
		new[] { "COSMETICS", "nice cosmetics" },
		new[] { "SO FAST", "you are so fast" }
	};

	private static readonly string[][] PremadeInfo =
	{
		new[] { "CAN YOU HEAR ME", "can you hear me" },
		new[] { "USING TTS", "i am using text to speech" },
		new[] { "ABOUT MY MOD", "i made this mod fully by myself, and i coded it with c sharp which is a programming language. it is not cheating, and does not affect gameplay" },
		new[] { "JUST TESTING", "i am just testing this" },
		new[] { "NOT CHEATING", "i am not cheating, this is just my own mod" }
	};

	private static readonly string[][] PlayerFriendly =
	{
		new[] { "HEY", "hey {name}" },
		new[] { "GG", "good game {name}" },
		new[] { "NICE ONE", "nice one {name}" },
		new[] { "THANKS", "thanks {name}" },
		new[] { "SORRY", "sorry {name}" },
		new[] { "WELCOME", "welcome {name}" },
		new[] { "GOOD LUCK", "good luck {name}" },
		new[] { "SEE YA", "see you later {name}" }
	};

	private static readonly string[][] PlayerGame =
	{
		new[] { "PARTY?", "wanna party {name}" },
		new[] { "ADD ME", "add me {name}" },
		new[] { "JOIN MY ROOM", "can you join my room {name}" },
		new[] { "WELL PLAYED", "well played {name}" },
		new[] { "RUN IT BACK", "run it back {name}" },
		new[] { "CLOSE ONE", "that was close {name}" },
		new[] { "YOU GOT ME", "you got me {name}" },
		new[] { "GOOD TRY", "good try {name}" }
	};

	private static readonly string[][] PlayerCallouts =
	{
		new[] { "BEHIND YOU", "{name} is behind you" },
		new[] { "FOLLOW ME", "follow me {name}" },
		new[] { "COME HERE", "come here {name}" },
		new[] { "WAIT FOR ME", "wait for me {name}" },
		new[] { "WATCH OUT", "watch out {name}" },
		new[] { "OVER HERE", "{name} is over here" },
		new[] { "HELP ME", "help me {name}" },
		new[] { "YOU ARE IT", "{name} you are it" }
	};

	private static readonly string[][] PlayerCompliments =
	{
		new[] { "NICE MOVES", "{name} nice moves" },
		new[] { "SO GOOD", "{name} you are really good" },
		new[] { "SMOOTH", "{name} that was smooth" },
		new[] { "COOL STYLE", "{name} i love your style" },
		new[] { "COSMETICS", "{name} nice cosmetics" },
		new[] { "SO FAST", "{name} you are so fast" },
		new[] { "CRACKED", "{name} you are actually cracked" }
	};

	// -1 = top menu (Player/Settings/Premade). 0 = Player, 1 = Settings, 2 = Premade.
	private static int _section = -1;

	private static int _cat = -1;

	private static int _page;

	private static bool _editing;

	private static string _input = "";

	private static UtilTab _tab;

	public static void Install()
	{
		try
		{
			Harmony harmony = new Harmony("local.sakuraa.lobbyttstab");

			MethodInfo buildTabs = typeof(LobbyPage).GetMethod("BuildTabs");
			MethodInfo buildTabsPostfix = typeof(LobbyGunPatch).GetMethod(nameof(BuildTabsPostfix), BindingFlags.Static | BindingFlags.NonPublic);
			harmony.Patch(buildTabs, postfix: new HarmonyMethod(buildTabsPostfix));

			MethodInfo onTabSelected = typeof(LobbyPage).GetMethod("OnTabSelected");
			MethodInfo onTabSelectedPrefix = typeof(LobbyGunPatch).GetMethod(nameof(OnTabSelectedPrefix), BindingFlags.Static | BindingFlags.NonPublic);
			harmony.Patch(onTabSelected, prefix: new HarmonyMethod(onTabSelectedPrefix));

			StandalonePlugin.StatusLog("lobby tts tab installed");
		}
		catch (Exception ex)
		{
			StandalonePlugin.StatusLog("lobby tts tab failed: " + ex.Message);
		}
	}

	// drop the old Gun tab and put our tab in the same spot (index 2), so Lobby ends up
	// with Lobby, Leaderboard, TTS, Friends, same 4 slots as before.
	private static void BuildTabsPostfix(LobbyPage __instance)
	{
		__instance.Tabs.RemoveAll((UtilTab t) => t.TabName == "Gun");
		_tab = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = "TTS" };
		_section = -1;
		_cat = -1;
		_page = 0;
		Rebuild();
		int insertAt = Math.Min(2, __instance.Tabs.Count);
		__instance.Tabs.Insert(insertAt, _tab);
	}

	// tab index 2 used to be Gun, which toggled the pointer. skip that entirely and just
	// redraw our tab instead. returning false here also skips the tail cleanup block the
	// original method runs for every tab except Leaderboard(1) and (what used to be) Gun(2),
	// which is exactly what we want, the selected player should survive a trip to the TTS tab.
	private static bool OnTabSelectedPrefix(int tabIndex)
	{
		if (tabIndex == 2)
		{
			Rebuild();
			return false;
		}
		return true;
	}

	private static void Refresh()
	{
		Rebuild();
		if (UtilMenuController.Instance != null)
		{
			UtilMenuController.Instance.RefreshUI();
		}
	}

	private static void Rebuild()
	{
		if (_tab == null)
		{
			return;
		}
		_tab.Elements.Clear();
		switch (_section)
		{
		case 0:
			BuildPlayerSection();
			break;
		case 1:
			BuildSettingsSection();
			break;
		case 2:
			BuildPremadeSection();
			break;
		default:
			BuildTopMenu();
			break;
		}
	}

	private static void BuildTopMenu()
	{
		string name = TtsPlayers.SelectedName();
		_tab.Elements.Add(new MenuElement(name == null ? "PLAYER (PICK ONE)" : "PLAYER: " + Shorten(name), delegate
		{
			_section = 0;
			_cat = -1;
			_page = 0;
			Refresh();
		}));
		_tab.Elements.Add(new MenuElement("SETTINGS", delegate
		{
			_section = 1;
			Refresh();
		}));
		_tab.Elements.Add(new MenuElement("PREMADE", delegate
		{
			_section = 2;
			_cat = -1;
			_page = 0;
			Refresh();
		}));
	}

	private static void BuildPlayerSection()
	{
		if (TtsPlayers.SelectedName() == null)
		{
			_tab.Elements.Add(new MenuElement("PICK A PLAYER IN LEADERBOARD", delegate { Refresh(); }));
			_tab.Elements.Add(new MenuElement("< BACK", delegate
			{
				_section = -1;
				Refresh();
			}));
			return;
		}
		List<Category> cats = new List<Category>
		{
			FromLines("FRIENDLY", PlayerFriendly, false),
			FromLines("GAME", PlayerGame, false),
			FromLines("CALLOUTS", PlayerCallouts, false),
			FromLines("COMPLIMENTS", PlayerCompliments, true),
			BuildInfoCategory()
		};
		Browse(cats);
	}

	private static Category BuildInfoCategory()
	{
		Category info = new Category("INFO");
		info.Entries.Add(new Entry("MOD COUNT", TtsPlayers.SayCounts));
		info.Entries.Add(new Entry("THEIR CHEATS", TtsPlayers.SayCheats));
		info.Entries.Add(new Entry("THEIR MODS", TtsPlayers.SayMods));
		info.Entries.Add(new Entry("IS CLEAN?", TtsPlayers.SayClean));
		return info;
	}

	private static void BuildPremadeSection()
	{
		List<Category> cats = new List<Category>
		{
			FromLines("FRIENDLY", PremadeFriendly, false),
			FromLines("GAME", PremadeGame, false),
			FromLines("CALLOUTS", PremadeCallouts, false),
			FromLines("COMPLIMENTS", PremadeCompliments, true),
			FromLines("INFO", PremadeInfo, false)
		};
		Browse(cats);
	}

	// type message, speak, clear, speed+status combined, stop, back. exactly 6 rows.
	private static void BuildSettingsSection()
	{
		string label = !_editing ? "TYPE MESSAGE" : (_input == "" ? "TYPE..." : _input.ToUpper());
		ElementType type = _editing ? ElementType.Input : ElementType.Button;
		_tab.Elements.Add(new MenuElement(label, delegate
		{
			if (_editing)
			{
				FinishEditing(false);
				return;
			}
			_editing = true;
			KeyboardController kb = KeyboardController.Instance;
			kb.currentInput = _input;
			kb.OnKeyPressed = delegate(string k)
			{
				_input = k;
				Refresh();
			};
			kb.OnEnterPressed = delegate
			{
				FinishEditing(true);
			};
			Refresh();
			kb.OpenKeyboard();
		}, type));
		_tab.Elements.Add(new MenuElement(_input == "" ? "SPEAK" : "SPEAK: " + Shorten(_input), delegate
		{
			if (_input != "")
			{
				TtsClient.Say(_input);
			}
		}));
		_tab.Elements.Add(new MenuElement("CLEAR", delegate
		{
			_input = "";
			if (_editing && KeyboardController.Instance != null)
			{
				KeyboardController.Instance.currentInput = "";
			}
			Refresh();
		}));
		_tab.Elements.Add(new MenuElement("SPEED: " + TtsClient.Speed.ToUpper() + " (" + TtsClient.Status + ")", delegate
		{
			TtsClient.CycleSpeed();
			Refresh();
			TtsClient.Ping(Refresh);
		}));
		_tab.Elements.Add(new MenuElement("STOP", delegate { SoundboardPlayer.Stop(); }));
		_tab.Elements.Add(new MenuElement("< BACK", delegate
		{
			FinishEditing(false);
			_section = -1;
			Refresh();
		}));
	}

	private static void FinishEditing(bool speakIt)
	{
		_editing = false;
		try
		{
			KeyboardController.Instance.CloseKeyboard();
		}
		catch (Exception)
		{
		}
		if (speakIt && _input.Trim() != "")
		{
			TtsClient.Say(_input);
		}
	}

	// turns a { label, text } table into a category. lines with {name} fill in the picked player.
	private static Category FromLines(string name, string[][] lines, bool withRandom)
	{
		Category cat = new Category(name);
		if (withRandom)
		{
			cat.Entries.Add(new Entry("RANDOM", delegate
			{
				Speak(lines[UnityEngine.Random.Range(0, lines.Length)][1]);
			}));
		}
		foreach (string[] line in lines)
		{
			string spoken = line[1];
			cat.Entries.Add(new Entry(line[0], delegate { Speak(spoken); }));
		}
		return cat;
	}

	private static void Speak(string template)
	{
		if (template.Contains("{name}"))
		{
			string name = TtsPlayers.SelectedName();
			if (name == null)
			{
				return;
			}
			template = template.Replace("{name}", TtsPlayers.SpokenName(name));
		}
		TtsClient.Say(template);
	}

	// category list: exactly 5 categories + BACK = 6 rows, no header needed (the top
	// menu button already said which section you're in).
	// inside a category: up to 4 lines + MORE + BACK when there's more than 5, otherwise
	// up to 5 lines + BACK. always 6 rows or fewer.
	private static void Browse(List<Category> cats)
	{
		if (_cat < 0 || _cat >= cats.Count)
		{
			_cat = -1;
			for (int i = 0; i < cats.Count; i++)
			{
				int idx = i;
				_tab.Elements.Add(new MenuElement(cats[i].Name, delegate
				{
					_cat = idx;
					_page = 0;
					Refresh();
				}));
			}
			_tab.Elements.Add(new MenuElement("< BACK", delegate
			{
				_section = -1;
				Refresh();
			}));
			return;
		}

		Category cat = cats[_cat];
		int total = cat.Entries.Count;
		bool paged = total > 5;
		int size = paged ? 4 : total;
		int pages = size == 0 ? 1 : (total + size - 1) / size;
		if (_page >= pages)
		{
			_page = 0;
		}
		int end = Math.Min(total, (_page + 1) * size);
		for (int i = _page * size; i < end; i++)
		{
			Entry entry = cat.Entries[i];
			_tab.Elements.Add(new MenuElement(entry.Label, delegate { entry.Run(); }));
		}
		if (paged)
		{
			_tab.Elements.Add(new MenuElement("MORE " + (_page + 1) + "/" + pages, delegate
			{
				_page = (_page + 1) % pages;
				Refresh();
			}));
		}
		_tab.Elements.Add(new MenuElement("< BACK", delegate
		{
			_cat = -1;
			_page = 0;
			Refresh();
		}));
	}

	private static string Shorten(string s)
	{
		return s.Length <= 16 ? s.ToUpper() : s.Substring(0, 16).ToUpper() + "...";
	}
}
