using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed class KnownListsWatcher : MonoBehaviour
{
	private float _next;

	private void Update()
	{
		if (Time.unscaledTime < _next)
		{
			return;
		}
		_next = Time.unscaledTime + 2f;
		StandalonePlugin.ListsTick();
	}
}

public sealed partial class StandalonePlugin
{
	private sealed class WildEntry
	{
		public string Fragment;
		public string Label;
		public int List;
	}

	private static readonly string[] ListFiles = new string[3] { "sakuraa_known_mods.txt", "sakuraa_known_cheats.txt", "sakuraa_known_unsure.txt" };
	private static readonly string[] RemoteListFiles = new string[3] { "mods.txt", "cheats.txt", "unsure.txt" };
	private static readonly string[] ListFields = new string[3] { "_webKnownMods", "_webKnownCheats", "_hardcodedUnknownMods" };
	private const string ListsRepoRawBase = "https://raw.githubusercontent.com/idiotthemodder/sakuraa-lists/main/";
	private static readonly System.Net.Http.HttpClient _listsHttp = new System.Net.Http.HttpClient
	{
	    Timeout = TimeSpan.FromSeconds(10)
	};
	private static DateTime _lastGithubSync = DateTime.MinValue;
	private static bool _githubSyncInFlight;
	private static readonly HashSet<string>[] _listAdded = new HashSet<string>[3] { new HashSet<string>(), new HashSet<string>(), new HashSet<string>() };
	private static readonly DateTime[] _listStamp = new DateTime[3];
	private static readonly List<WildEntry> _wildcards = new List<WildEntry>();
	private static Type _kpage;
	private static bool _listsReady;
	private static bool _listsErrLogged;

	internal static readonly string[] HelpCategoryNames = new string[3] { "Mods", "Cheats", "Unsure" };
	internal static int SelectedHelpCategory = -1;
	internal static string SelectedHelpKey;

	internal static void SyncListsFromGithub(bool force = false)
	{
	    if (_githubSyncInFlight)
	    {
	        return;
	    }
	    if (!force && (DateTime.UtcNow - _lastGithubSync) < TimeSpan.FromMinutes(10))
	    {
	        return;
	    }
	    _githubSyncInFlight = true;
	    _lastGithubSync = DateTime.UtcNow;
	    System.Threading.Tasks.Task.Run(async () =>
	    {
	        bool anyChanged = false;
	        try
	        {
	            for (int i = 0; i < ListFiles.Length; i++)
	            {
	                string fileName = ListFiles[i];
	                try
	                {
	                    string url = ListsRepoRawBase + RemoteListFiles[i];
	                    string content = await _listsHttp.GetStringAsync(url);
	                    string localPath = Path.Combine(Paths.ConfigPath, fileName);
	                    string existing = File.Exists(localPath) ? File.ReadAllText(localPath) : null;
	                    if (existing != content)
	                    {
	                        File.WriteAllText(localPath, content);
	                        anyChanged = true;
	                        StatusLog("synced " + fileName + " from github");
	                    }
	                }
	                catch (Exception exFile)
	                {
	                    StatusLog("github sync failed for " + fileName + ": " + exFile.Message);
	                }
	            }
	        }
	        finally
	        {
	            _githubSyncInFlight = false;
	            if (anyChanged)
	            {
	                _listsReady = false; // forces EnsureListsLoaded to re-read on next tick
	            }
	        }
	    });
	}

	internal static Dictionary<string, string> GetListDict(int index)
	{
		if (_kpage == null || index < 0 || index >= ListFields.Length)
		{
			return null;
		}
		FieldInfo field = _kpage.GetField(ListFields[index], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		return (field != null) ? (field.GetValue(null) as Dictionary<string, string>) : null;
	}

	internal static void SelectHelp(int category, string key)
	{
		SelectedHelpCategory = category;
		SelectedHelpKey = key;
	}

	internal static void StatusLog(string text)
	{
		try
		{
			File.AppendAllText(Path.Combine(Paths.ConfigPath, "sakuraa_status.txt"), DateTime.Now.ToString("HH:mm:ss") + " " + text + Environment.NewLine);
		}
		catch
		{
		}
	}

	private static void PatchKnownLists(Assembly core)
	{
		_kpage = core.GetType("SakuraaCastingMod.VR.UtilMenu.Pages.LobbyPage", throwOnError: true);
		MethodInfo target = _kpage.GetMethod("GetDetectedMods", BindingFlags.Static | BindingFlags.Public);
		if (target == null)
		{
			throw new MissingMethodException(_kpage.FullName, "GetDetectedMods");
		}
		MethodInfo prefix = typeof(StandalonePlugin).GetMethod("KnownListsPrefix", BindingFlags.Static | BindingFlags.NonPublic);
		new HarmonyLib.Harmony("local.sakuraa.knownlists").Patch(target, prefix: new HarmonyLib.HarmonyMethod(prefix));
		GameObject go = new GameObject("SakuraaKnownListsWatcher");
		UnityEngine.Object.DontDestroyOnLoad(go);
		go.AddComponent<KnownListsWatcher>();
		StatusLog("known lists patch installed");
	}

	private static void KnownListsPrefix()
	{
		if (!_listsReady)
		{
			EnsureListsLoaded();
		}
	}

	private static void EnsureListsLoaded()
	{
		try
		{
			LoadAllLists();
			_listsReady = true;
		}
		catch (Exception ex)
		{
			if (!_listsErrLogged)
			{
				_listsErrLogged = true;
				StatusLog("lists load failed: " + ex.Message);
			}
		}
	}

	private static void LoadAllLists()
	{
		_wildcards.Clear();
		int[] exact = new int[3];
		int[] wild = new int[3];
		for (int i = 0; i < 3; i++)
		{
			FieldInfo field = _kpage.GetField(ListFields[i], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			Dictionary<string, string> dict = (field != null) ? (field.GetValue(null) as Dictionary<string, string>) : null;
			if (dict == null)
			{
				StatusLog("could not find " + ListFields[i]);
				continue;
			}
			foreach (string old in _listAdded[i])
			{
				dict.Remove(old);
			}
			_listAdded[i].Clear();
			string path = Path.Combine(Paths.ConfigPath, ListFiles[i]);
			if (!File.Exists(path))
			{
				_listStamp[i] = DateTime.MinValue;
				continue;
			}
			_listStamp[i] = File.GetLastWriteTimeUtc(path);
			foreach (string raw in File.ReadAllLines(path))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line.StartsWith("#"))
				{
					continue;
				}
				int eq = line.IndexOf('=');
				if (eq <= 0)
				{
					continue;
				}
				string key = line.Substring(0, eq).Trim();
				string label = line.Substring(eq + 1).Trim();
				int hash = label.IndexOf(" #");
				if (hash >= 0)
				{
					label = label.Substring(0, hash).Trim();
				}
				if (key.Length == 0 || label.Length == 0)
				{
					continue;
				}
				if (key.StartsWith("~"))
				{
					string fragment = key.Substring(1).Trim().ToLowerInvariant();
					if (fragment.Length < 4)
					{
						StatusLog("wildcard too short, skipped: " + key);
						continue;
					}
					_wildcards.Add(new WildEntry { Fragment = fragment, Label = label, List = i });
					wild[i]++;
				}
				else if (!dict.ContainsKey(key))
				{
					dict[key] = label;
					_listAdded[i].Add(key);
					exact[i]++;
				}
			}
		}
		_listsLoadedAt = DateTime.Now;
		StatusLog("lists loaded: mods " + exact[0] + "+" + wild[0] + "w, cheats " + exact[1] + "+" + wild[1] + "w, unsure " + exact[2] + "+" + wild[2] + "w");
	}

	internal static void ListsTick()
	{
		try
		{
			if (_kpage == null)
			{
				return;
			}
			SyncListsFromGithub();
			if (!_listsReady)
			{
				if (!PhotonNetwork.InRoom)
				{
					return;
				}
				EnsureListsLoaded();
				if (!_listsReady)
				{
					return;
				}
			}
			bool changed = false;
			for (int i = 0; i < 3; i++)
			{
				string path = Path.Combine(Paths.ConfigPath, ListFiles[i]);
				DateTime stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
				if (stamp != _listStamp[i])
				{
					changed = true;
				}
			}
			if (changed)
			{
				StatusLog("list file changed, reloading");
				LoadAllLists();
			}
			ResolveWildcards();
			StatsTick();
		}
		catch (Exception ex)
		{
			if (!_listsErrLogged)
			{
				_listsErrLogged = true;
				StatusLog("lists tick failed: " + ex.Message);
			}
		}
	}

	private static void ResolveWildcards()
	{
		if (_wildcards.Count == 0 || !PhotonNetwork.InRoom)
		{
			return;
		}
		Player[] players = PhotonNetwork.PlayerList;
		if (players == null)
		{
			return;
		}
		foreach (Player p in players)
		{
			if (p == null || p.IsLocal || p.CustomProperties == null)
			{
				continue;
			}
			foreach (var entry in p.CustomProperties)
			{
				string key = (entry.Key != null) ? entry.Key.ToString() : null;
				if (string.IsNullOrEmpty(key))
				{
					continue;
				}
				string lower = key.ToLowerInvariant();
				foreach (WildEntry w in _wildcards)
				{
					if (!lower.Contains(w.Fragment))
					{
						continue;
					}
					FieldInfo field = _kpage.GetField(ListFields[w.List], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					Dictionary<string, string> dict = (field != null) ? (field.GetValue(null) as Dictionary<string, string>) : null;
					if (dict != null && !dict.ContainsKey(key))
					{
						dict[key] = w.Label;
						_listAdded[w.List].Add(key);
						StatusLog("wildcard ~" + w.Fragment + " matched new key: " + EscapeText(key) + " -> " + w.Label);
					}
				}
			}
		}
	}
}
