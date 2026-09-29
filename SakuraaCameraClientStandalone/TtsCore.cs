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

// shared tts pieces: talks to the tts helper (127.0.0.1:8770) and reads whoever is picked
// in the Lobby page's Leaderboard tab. used by LobbyGunPatch's TTS tab.
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
