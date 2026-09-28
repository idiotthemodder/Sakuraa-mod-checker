using System;
using System.Collections;
using System.IO;
using BepInEx;
using SakuraaCastingMod.Features.Soundboard;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using SakuraaCastingMod.VR.UtilMenu.Utility;
using UnityEngine;
using UnityEngine.Networking;

namespace SakuraaCameraClientStandalone;

// menu page: type a message on the vr keyboard and it gets spoken into voice chat.
// flow: page -> TtsClient.Say -> helper (127.0.0.1:8770) makes a wav -> we hand it to
// SoundboardPlayer.Play, so it goes out the same way a soundboard tile does.
public sealed class TtsPage : BasePage
{
	// the menu has 6 tab slots and 6 rows per tab.
	// slots used: 1 (Speak) + PlayerTabs (Player, Mod Talk, Callouts, Social, in that order) + quick pages.
	// PlayerTabs = 3 gives 2 quick pages (12 lines). 2 gives 3 pages (18 lines). 4 gives 1 page (6 lines).
	private const int MaxTabs = 6;

	private const int PlayerTabs = 3;

	// each entry is { button label, what gets spoken }. keep labels short so they fit on the button.
	// the best lines are first, since only the first few pages actually show.
	private static readonly string[][] Quick =
	{
		// page 1: everyday chat
		new[] { "HELLO", "hello everyone" },
		new[] { "GG", "good game" },
		new[] { "GOOD LUCK", "good luck" },
		new[] { "WELL PLAYED", "well played" },
		new[] { "NICE ONE", "nice one" },
		new[] { "THANK YOU", "thank you" },

		// page 2: talking about yourself and the mod
		new[] { "SORRY", "sorry about that" },
		new[] { "CAN YOU HEAR ME", "can you hear me" },
		new[] { "ONE SECOND", "one second please" },
		new[] { "USING TTS", "i am using text to speech" },
		new[] { "ABOUT MY MOD", "i made this mod fully by myself, and i coded it with c sharp which is a programming language. it is not cheating, and does not affect gameplay" },
		new[] { "BYE", "see you later everyone" },

		// page 3: only shows if PlayerTabs is 2 or less
		new[] { "CLOSE ONE", "that was close" },
		new[] { "GOOD TRY", "good try" },
		new[] { "YOU GOT ME", "you got me" },
		new[] { "LAGGING", "my game is lagging" },
		new[] { "BE NICE", "let's all be nice" },
		new[] { "GOODBYE", "goodbye everyone" }
	};

	private bool _editing;

	private string _input = "";

	public override string PageName => "TTS";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.Server;

	public override void BuildTabs()
	{
		Tabs.Clear();
		UtilTab speak = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = "Speak" };

		string label = !_editing ? "TYPE MESSAGE" : (_input == "" ? "TYPE..." : _input.ToUpper());
		ElementType type = _editing ? ElementType.Input : ElementType.Button;
		speak.Elements.Add(new MenuElement(label, delegate
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

		speak.Elements.Add(new MenuElement(_input == "" ? "SPEAK" : "SPEAK: " + Shorten(_input), delegate
		{
			if (_input != "")
			{
				TtsClient.Say(_input);
			}
		}));
		speak.Elements.Add(new MenuElement("CLEAR", delegate
		{
			_input = "";
			if (_editing && KeyboardController.Instance != null)
			{
				KeyboardController.Instance.currentInput = "";
			}
			Refresh();
		}));
		speak.Elements.Add(new MenuElement("STOP", delegate { SoundboardPlayer.Stop(); }));
		speak.Elements.Add(new MenuElement("HELPER: " + TtsClient.Status, delegate { Refresh(); }));
		Tabs.Add(speak);

		// tabs that work off the player picked in the Lobby tab
		TtsPlayerTools.AddTabs(Tabs, Refresh, PlayerTabs);

		// quick lines fill whatever tab slots are left
		int page = 1;
		for (int i = 0; i < Quick.Length && Tabs.Count < MaxTabs; i += 6)
		{
			UtilTab quick = new UtilTab
			{
				TabIcon = UtilMenuMain.Instance.Icons.Server,
				TabName = "Quick " + page
			};
			int end = Math.Min(i + 6, Quick.Length);
			for (int j = i; j < end; j++)
			{
				string spoken = Quick[j][1];
				quick.Elements.Add(new MenuElement(Quick[j][0], delegate { TtsClient.Say(spoken); }));
			}
			Tabs.Add(quick);
			page++;
		}
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

internal sealed class TtsRunner : MonoBehaviour
{
}

public static class TtsClient
{
	private const string Base = "http://127.0.0.1:8770";

	private static TtsRunner _runner;

	private static string _last = "";

	private static int _counter;

	public static string Status = "UNKNOWN";

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

	public static void Repeat()
	{
		if (_last != "")
		{
			Say(_last);
		}
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
		string url = Base + "/say?text=" + Uri.EscapeDataString(text);
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
