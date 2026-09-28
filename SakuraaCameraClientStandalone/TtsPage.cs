using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using SakuraaCastingMod.Features.Soundboard;
using SakuraaCastingMod.Shared.Helpers;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using SakuraaCastingMod.VR.UtilMenu.Utility;
using UnityEngine;
using UnityEngine.Networking;

namespace SakuraaCameraClientStandalone;

// TTS page. four tabs: SPEAK, QUICK, PLAYER, SETTINGS.
// QUICK and PLAYER expand: tap a category (Friendly, Game, Callouts, Compliments, Info) to see its lines,
// MORE flips through extra lines, BACK returns to the category list.
// PLAYER works off whoever you picked in the Lobby tab, and fills {name} into each line.
// flow: page -> TtsClient.Say -> helper (127.0.0.1:8770) makes a wav -> SoundboardPlayer.Play,
// so it goes out the same way a soundboard tile does.
public sealed class TtsPage : BasePage
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

	private sealed class Nav
	{
		public int Cat = -1;

		public int Page;
	}

	// { button label, what gets spoken }. player lines use {name}.
	private static readonly string[][] QuickFriendly =
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

	private static readonly string[][] QuickGame =
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

	private static readonly string[][] QuickCallouts =
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

	private static readonly string[][] QuickCompliments =
	{
		new[] { "NICE MOVES", "nice moves" },
		new[] { "SO GOOD", "you are really good" },
		new[] { "SMOOTH", "that was smooth" },
		new[] { "GREAT JOB", "great job everyone" },
		new[] { "COOL STYLE", "i love your style" },
		new[] { "COSMETICS", "nice cosmetics" },
		new[] { "SO FAST", "you are so fast" }
	};

	private static readonly string[][] QuickInfo =
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

	private readonly Nav _quickNav = new Nav();

	private readonly Nav _playerNav = new Nav();

	private bool _editing;

	private string _input = "";

	public override string PageName => "TTS";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.Server;

	public override void BuildTabs()
	{
		Tabs.Clear();
		Tabs.Add(BuildSpeakTab());
		Tabs.Add(BuildQuickTab());
		Tabs.Add(BuildPlayerTab());
		Tabs.Add(BuildSettingsTab());
	}

	private UtilTab NewTab(string title)
	{
		return new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = title };
	}

	// ---------- SPEAK ----------

	private UtilTab BuildSpeakTab()
	{
		UtilTab tab = NewTab("Speak");
		string label = !_editing ? "TYPE MESSAGE" : (_input == "" ? "TYPE..." : _input.ToUpper());
		ElementType type = _editing ? ElementType.Input : ElementType.Button;
		tab.Elements.Add(new MenuElement(label, delegate
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
		tab.Elements.Add(new MenuElement(_input == "" ? "SPEAK" : "SPEAK: " + Shorten(_input), delegate
		{
			if (_input != "")
			{
				TtsClient.Say(_input);
			}
		}));
		tab.Elements.Add(new MenuElement("CLEAR", delegate
		{
			_input = "";
			if (_editing && KeyboardController.Instance != null)
			{
				KeyboardController.Instance.currentInput = "";
			}
			Refresh();
		}));
		tab.Elements.Add(new MenuElement("REPEAT LAST", delegate { TtsClient.Repeat(); }));
		tab.Elements.Add(new MenuElement("STOP", delegate { SoundboardPlayer.Stop(); }));
		return tab;
	}

	// ---------- QUICK ----------

	private UtilTab BuildQuickTab()
	{
		UtilTab tab = NewTab("Quick");
		List<Category> cats = new List<Category>
		{
			FromLines("FRIENDLY", QuickFriendly, false),
			FromLines("GAME", QuickGame, false),
			FromLines("CALLOUTS", QuickCallouts, false),
			FromLines("COMPLIMENTS", QuickCompliments, true),
			FromLines("INFO", QuickInfo, false)
		};
		Browse(tab, cats, _quickNav, null);
		return tab;
	}

	// ---------- PLAYER ----------

	private UtilTab BuildPlayerTab()
	{
		UtilTab tab = NewTab("Player");
		string name = TtsPlayers.SelectedName();
		if (name == null)
		{
			_playerNav.Cat = -1;
			tab.Elements.Add(new MenuElement("PICK A PLAYER IN LOBBY", delegate { Refresh(); }));
			tab.Elements.Add(new MenuElement("THEN TAP HERE", delegate { Refresh(); }));
			return tab;
		}
		List<Category> cats = new List<Category>
		{
			FromLines("FRIENDLY", PlayerFriendly, false),
			FromLines("GAME", PlayerGame, false),
			FromLines("CALLOUTS", PlayerCallouts, false),
			FromLines("COMPLIMENTS", PlayerCompliments, true),
			BuildInfoCategory()
		};
		Browse(tab, cats, _playerNav, "PLAYER: " + Shorten(name));
		return tab;
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

	// ---------- SETTINGS ----------

	private UtilTab BuildSettingsTab()
	{
		UtilTab tab = NewTab("Settings");
		tab.Elements.Add(new MenuElement("HELPER: " + TtsClient.Status, delegate
		{
			TtsClient.Ping(Refresh);
		}));
		tab.Elements.Add(new MenuElement("SPEED: " + TtsClient.Speed.ToUpper(), delegate
		{
			TtsClient.CycleSpeed();
			Refresh();
		}));
		tab.Elements.Add(new MenuElement("TEST VOICE", delegate { TtsClient.Say("this is a test of the voice"); }));
		tab.Elements.Add(new MenuElement("STOP", delegate { SoundboardPlayer.Stop(); }));
		return tab;
	}

	// ---------- expandable browser ----------

	// turns a { label, text } table into a category. lines that contain {name} fill in the picked player.
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

	// the menu shows 6 rows per tab, so this never adds more than 6:
	// category list = header (optional) + up to 5 categories.
	// inside a category = up to 4 lines + MORE + BACK when there are more than 5 lines, otherwise up to 5 lines + BACK.
	private void Browse(UtilTab tab, List<Category> cats, Nav nav, string header)
	{
		if (nav.Cat < 0 || nav.Cat >= cats.Count)
		{
			nav.Cat = -1;
			if (header != null)
			{
				tab.Elements.Add(new MenuElement(header, delegate { Refresh(); }));
			}
			for (int i = 0; i < cats.Count; i++)
			{
				int idx = i;
				tab.Elements.Add(new MenuElement(cats[i].Name, delegate
				{
					nav.Cat = idx;
					nav.Page = 0;
					Refresh();
				}));
			}
			return;
		}

		Category cat = cats[nav.Cat];
		int total = cat.Entries.Count;
		bool paged = total > 5;
		int size = paged ? 4 : total;
		int pages = size == 0 ? 1 : (total + size - 1) / size;
		if (nav.Page >= pages)
		{
			nav.Page = 0;
		}
		int end = Math.Min(total, (nav.Page + 1) * size);
		for (int i = nav.Page * size; i < end; i++)
		{
			Entry entry = cat.Entries[i];
			tab.Elements.Add(new MenuElement(entry.Label, delegate { entry.Run(); }));
		}
		if (paged)
		{
			tab.Elements.Add(new MenuElement("MORE " + (nav.Page + 1) + "/" + pages, delegate
			{
				nav.Page = (nav.Page + 1) % pages;
				Refresh();
			}));
		}
		tab.Elements.Add(new MenuElement("< BACK", delegate
		{
			nav.Cat = -1;
			nav.Page = 0;
			Refresh();
		}));
	}

	private void FinishEditing(bool speakIt)
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
		Refresh();
	}

	private static string Shorten(string s)
	{
		return s.Length <= 18 ? s.ToUpper() : s.Substring(0, 18).ToUpper() + "...";
	}

	private void Refresh()
	{
		BuildTabs();
		if (UtilMenuController.Instance != null)
		{
			UtilMenuController.Instance.RefreshUI();
		}
	}

	public override void RefreshPageUI()
	{
		BuildTabs();
	}
}

// reads whoever is picked in the Lobby tab and speaks mod/cheat info about them
public static class TtsPlayers
{
	private static FieldInfo _selectedField;

	public static NetPlayer SelectedPlayer()
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

	public static string SelectedName()
	{
		NetPlayer p = SelectedPlayer();
		if (p == null || string.IsNullOrEmpty(p.NickName))
		{
			return null;
		}
		return p.NickName;
	}

	// nicknames look like "STEVE_123", tidy them so the voice can say them
	public static string SpokenName(string nick)
	{
		string s = Regex.Replace(nick, "<[^>]+>", "");
		s = Regex.Replace(s, "[^A-Za-z0-9 ]", " ");
		s = Regex.Replace(s, "\\s+", " ").Trim().ToLower();
		return s == "" ? "player" : s;
	}

	// same rule the lobby panel uses: red entries are cheats, everything else is a mod
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

	public static void SayCounts()
	{
		string name = SelectedName();
		if (name == null)
		{
			return;
		}
		Split(out List<string> cheats, out List<string> mods);
		TtsClient.Say(SpokenName(name) + " has " + Count(mods.Count, "mod") + " and " + Count(cheats.Count, "cheat"));
	}

	public static void SayCheats()
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

	public static void SayMods()
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

	public static void SayClean()
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
}

internal sealed class TtsRunner : MonoBehaviour
{
}

public static class TtsClient
{
	private const string Base = "http://127.0.0.1:8770";

	private static readonly string[] Speeds = { "slow", "normal", "fast" };

	private static TtsRunner _runner;

	private static string _last = "";

	private static int _counter;

	public static string Status = "UNKNOWN";

	public static string Speed = "normal";

	private static string OutDir => Path.Combine(Paths.ConfigPath, "SakuraaTTS");

	private static TtsRunner Runner
	{
		get
		{
			if (_runner == null)
			{
				GameObject go = new GameObject("SakTtsRunner");
				UnityEngine.Object.DontDestroyOnLoad(go);
				_runner = go.AddComponent<TtsRunner>();
			}
			return _runner;
		}
	}

	public static void CycleSpeed()
	{
		int i = Array.IndexOf(Speeds, Speed);
		Speed = Speeds[(i + 1) % Speeds.Length];
	}

	public static void Repeat()
	{
		if (_last != "")
		{
			Say(_last);
		}
	}

	public static void Ping(Action done)
	{
		Runner.StartCoroutine(PingRoutine(done));
	}

	private static IEnumerator PingRoutine(Action done)
	{
		using (UnityWebRequest req = UnityWebRequest.Get(Base + "/health"))
		{
			req.timeout = 3;
			yield return req.SendWebRequest();
			Status = (req.isNetworkError || req.isHttpError) ? "OFFLINE" : "ONLINE";
		}
		done?.Invoke();
	}

	public static void Say(string text)
	{
		text = (text ?? "").Trim();
		if (text == "")
		{
			return;
		}
		_last = text;
		Runner.StartCoroutine(SayRoutine(text));
	}

	private static IEnumerator SayRoutine(string text)
	{
		string url = Base + "/say?speed=" + Speed + "&text=" + Uri.EscapeDataString(text);
		using (UnityWebRequest req = UnityWebRequest.Get(url))
		{
			req.timeout = 20;
			yield return req.SendWebRequest();
			if (req.isNetworkError || req.isHttpError)
			{
				Status = req.isNetworkError ? "OFFLINE" : "ERROR";
				StandalonePlugin.StatusLog("tts failed: " + req.error + " " + req.downloadHandler.text);
				yield break;
			}
			Status = "ONLINE";
			string name = req.downloadHandler.text.Trim();
			string path = Path.Combine(OutDir, name);
			if (!File.Exists(path))
			{
				StandalonePlugin.StatusLog("tts: helper said " + name + " but no file at " + path);
				yield break;
			}
			_counter++;
			SoundboardManager.Sound sound = new SoundboardManager.Sound
			{
				Id = "tts:" + _counter,
				Name = "tts",
				FilePath = path
			};
			StandalonePlugin.StatusLog("tts: playing '" + text + "'");
			SoundboardPlayer.Play(sound);
		}
	}
}
