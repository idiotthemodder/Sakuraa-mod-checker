using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx;
using Photon.Pun;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using UnityEngine;
using UnityEngine.Networking;

namespace SakuraaCameraClientStandalone;

// PRESENCE page: shows your room on your discord profile.
// the mod only reports the room to a local helper (sakuraa-presence.py on 127.0.0.1:8771),
// and the helper talks to discord. nothing here touches discord directly.
public sealed class PresencePage : BasePage
{
	public override string PageName => "PRESENCE";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.Server;

	public override void BuildTabs()
	{
		// building the page is what starts the reporter, so it runs from the moment the menu loads
		PresenceReporter.EnsureStarted();
		Tabs.Clear();
		UtilTab tab = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = "Discord" };
		tab.Elements.Add(new MenuElement("HELPER: " + PresenceReporter.Status, delegate
		{
			PresenceReporter.CheckNow(Refresh);
		}));
		tab.Elements.Add(new MenuElement("PRESENCE: " + (PresenceReporter.Enabled ? "ON" : "OFF"), delegate
		{
			PresenceReporter.Enabled = !PresenceReporter.Enabled;
			PresenceReporter.Save();
			PresenceReporter.PushNow();
			Refresh();
		}));
		tab.Elements.Add(new MenuElement("ROOM CODE: " + (PresenceReporter.ShowCode ? "SHOWN" : "HIDDEN"), delegate
		{
			PresenceReporter.ShowCode = !PresenceReporter.ShowCode;
			PresenceReporter.Save();
			PresenceReporter.PushNow();
			Refresh();
		}));
		tab.Elements.Add(new MenuElement("UPDATE NOW", delegate { PresenceReporter.PushNow(); }));
		Tabs.Add(tab);
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

internal sealed class PresenceRunner : MonoBehaviour
{
}

public static class PresenceReporter
{
	private const string Base = "http://127.0.0.1:8771";

	private static readonly string[] Modes =
	{
		"INFECTION", "CASUAL", "HUNT", "PAINTBRAWL", "AMBUSH", "FREEZE", "GHOST", "BATTLE", "GUARDIAN", "SUPER"
	};

	public static string Status = "UNKNOWN";

	public static bool Enabled = true;

	// off by default: a room code on your profile lets anyone who can see it join you
	public static bool ShowCode = false;

	private static PresenceRunner _runner;

	private static bool _loaded;

	private static string _lastSent = "";

	private static float _lastSentTime;

	private static string _lastRaw = "";

	private static string ConfigFile => Path.Combine(Paths.ConfigPath, "sakuraa_presence.txt");

	public static void EnsureStarted()
	{
		Load();
		if (_runner != null)
		{
			return;
		}
		GameObject go = new GameObject("SakPresenceRunner");
		UnityEngine.Object.DontDestroyOnLoad(go);
		_runner = go.AddComponent<PresenceRunner>();
		_runner.StartCoroutine(Loop());
	}

	public static void PushNow()
	{
		if (_runner != null)
		{
			_runner.StartCoroutine(Push(true));
		}
	}

	public static void CheckNow(Action done)
	{
		if (_runner != null)
		{
			_runner.StartCoroutine(Check(done));
		}
	}

	private static void Load()
	{
		if (_loaded)
		{
			return;
		}
		_loaded = true;
		try
		{
			if (File.Exists(ConfigFile))
			{
				foreach (string line in File.ReadAllLines(ConfigFile))
				{
					if (line.StartsWith("enabled="))
					{
						Enabled = line.Substring(8).Trim() == "1";
					}
					else if (line.StartsWith("showcode="))
					{
						ShowCode = line.Substring(9).Trim() == "1";
					}
				}
			}
		}
		catch (Exception ex)
		{
			StandalonePlugin.StatusLog("presence config load failed: " + ex.Message);
		}
	}

	public static void Save()
	{
		try
		{
			File.WriteAllText(ConfigFile, "enabled=" + (Enabled ? "1" : "0") + "\nshowcode=" + (ShowCode ? "1" : "0") + "\n");
		}
		catch (Exception ex)
		{
			StandalonePlugin.StatusLog("presence config save failed: " + ex.Message);
		}
	}

	private static IEnumerator Loop()
	{
		while (true)
		{
			yield return new WaitForSecondsRealtime(5f);
			yield return Push(false);
		}
	}

	private static IEnumerator Check(Action done)
	{
		using (UnityWebRequest req = UnityWebRequest.Get(Base + "/health"))
		{
			req.timeout = 3;
			yield return req.SendWebRequest();
			if (req.isNetworkError || req.isHttpError)
			{
				Status = "OFFLINE";
			}
			else
			{
				string body = req.downloadHandler.text;
				Status = (body.Contains("connected") || body.Contains("idle")) ? "ONLINE" : "NO DISCORD";
			}
		}
		done?.Invoke();
	}

	private static IEnumerator Push(bool force)
	{
		string query = BuildQuery();
		// resend the same thing every 30s as a heartbeat, so the helper can tell when the game has closed
		bool heartbeat = Time.realtimeSinceStartup - _lastSentTime > 30f;
		if (!force && !heartbeat && query == _lastSent)
		{
			yield break;
		}
		using (UnityWebRequest req = UnityWebRequest.Get(Base + query))
		{
			req.timeout = 3;
			yield return req.SendWebRequest();
			if (req.isNetworkError || req.isHttpError)
			{
				Status = "OFFLINE";
				_lastSent = "";
				yield break;
			}
			Status = "ONLINE";
			_lastSent = query;
			_lastSentTime = Time.realtimeSinceStartup;
		}
	}

	private static string BuildQuery()
	{
		if (!Enabled)
		{
			return "/clear";
		}
		var room = PhotonNetwork.CurrentRoom;
		if (room == null)
		{
			return "/set?inroom=0";
		}
		string raw = "";
		try
		{
			var props = room.CustomProperties;
			if (props != null && props.ContainsKey("gameMode"))
			{
				raw = props["gameMode"] as string ?? "";
			}
		}
		catch (Exception)
		{
		}
		if (raw != _lastRaw)
		{
			_lastRaw = raw;
			StandalonePlugin.StatusLog("presence gameMode string: " + raw);
		}
		string up = raw.ToUpper();
		string zone = Regex.Match(raw, "^[a-z]+").Value;
		string mode = "";
		foreach (string m in Modes)
		{
			if (up.Contains(m))
			{
				mode = m;
				break;
			}
		}
		string q = "/set?inroom=1&players=" + room.PlayerCount + "&max=" + room.MaxPlayers
			+ "&modded=" + (up.Contains("MODDED") ? "1" : "0")
			+ "&zone=" + Uri.EscapeDataString(zone)
			+ "&mode=" + Uri.EscapeDataString(mode);
		if (ShowCode && !string.IsNullOrEmpty(room.Name))
		{
			q += "&code=" + Uri.EscapeDataString(room.Name);
		}
		return q;
	}
}
