using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed partial class StandalonePlugin
{
	private static readonly string[] AllDictNames = new string[5] { "_hardcodedCheats", "_hardcodedSafeMods", "_hardcodedUnknownMods", "_webKnownMods", "_webKnownCheats" };
	private static readonly Regex VersionRegex = new Regex("\\d+\\.\\d+(\\.\\d+){0,2}");
	private static readonly Dictionary<string, KeyValuePair<float, Dictionary<string, string>>> _versionCache = new Dictionary<string, KeyValuePair<float, Dictionary<string, string>>>();
	private static readonly HashSet<string> _statsSeen = new HashSet<string>();
	private static Dictionary<string, string> _labelIndex;
	private static float _labelIndexTime = -10f;
	private static string _statsPath;
	private static bool _labelsErrLogged;

	private static void PatchLabels(Assembly core)
	{
		Type page = core.GetType("SakuraaCastingMod.VR.UtilMenu.Pages.LobbyPage", throwOnError: true);
		MethodInfo target = page.GetMethod("GetDetectedMods", BindingFlags.Static | BindingFlags.Public);
		if (target == null)
		{
			throw new MissingMethodException(page.FullName, "GetDetectedMods");
		}
		MethodInfo post = typeof(StandalonePlugin).GetMethod("LabelsPostfix", BindingFlags.Static | BindingFlags.NonPublic);
		new HarmonyLib.Harmony("local.sakuraa.labels").Patch(target, postfix: new HarmonyLib.HarmonyMethod(post));
		StatusLog("label cleanup patch installed");
	}

	private static Dictionary<string, string> GetLabelIndex()
	{
		if (_labelIndex != null && Time.unscaledTime - _labelIndexTime < 2f)
		{
			return _labelIndex;
		}
		Dictionary<string, string> index = new Dictionary<string, string>();
		if (_kpage != null)
		{
			foreach (string name in AllDictNames)
			{
				FieldInfo field = _kpage.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				Dictionary<string, string> dict = (field != null) ? (field.GetValue(null) as Dictionary<string, string>) : null;
				if (dict == null)
				{
					continue;
				}
				foreach (KeyValuePair<string, string> kv in dict)
				{
					string k = kv.Key.ToLowerInvariant();
					if (!index.ContainsKey(k))
					{
						index[k] = kv.Value.ToUpperInvariant();
					}
				}
			}
		}
		_labelIndex = index;
		_labelIndexTime = Time.unscaledTime;
		return index;
	}

	private static string GetUserId(object rig)
	{
		BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		Type t = rig.GetType();
		object creator = null;
		PropertyInfo prop = t.GetProperty("Creator", flags);
		if (prop != null)
		{
			creator = prop.GetValue(rig, null);
		}
		else
		{
			FieldInfo fld = t.GetField("Creator", flags);
			if (fld != null)
			{
				creator = fld.GetValue(rig);
			}
		}
		if (creator == null)
		{
			return null;
		}
		PropertyInfo idProp = creator.GetType().GetProperty("UserId", flags);
		return (idProp != null) ? (idProp.GetValue(creator, null) as string) : null;
	}

	private static Dictionary<string, string> GetVersionsFor(object rig)
	{
		string userId = GetUserId(rig);
		if (string.IsNullOrEmpty(userId))
		{
			return null;
		}
		KeyValuePair<float, Dictionary<string, string>> cached;
		if (_versionCache.TryGetValue(userId, out cached) && Time.unscaledTime - cached.Key < 2f)
		{
			return cached.Value;
		}
		if (_versionCache.Count > 200)
		{
			_versionCache.Clear();
		}
		Dictionary<string, string> versions = new Dictionary<string, string>();
		Dictionary<string, string> index = GetLabelIndex();
		Player[] players = PhotonNetwork.PlayerList;
		if (players != null)
		{
			foreach (Player p in players)
			{
				if (p == null || p.UserId != userId || p.CustomProperties == null)
				{
					continue;
				}
				foreach (var entry in p.CustomProperties)
				{
					string key = (entry.Key != null) ? entry.Key.ToString() : null;
					string text = entry.Value as string;
					if (key == null || text == null || text.Length > 60)
					{
						continue;
					}
					string label;
					if (!index.TryGetValue(key.ToLowerInvariant(), out label) || versions.ContainsKey(label))
					{
						continue;
					}
					Match m = VersionRegex.Match(text);
					if (m.Success && m.Value.Length <= 12)
					{
						versions[label] = m.Value;
					}
				}
			}
		}
		_versionCache[userId] = new KeyValuePair<float, Dictionary<string, string>>(Time.unscaledTime, versions);
		return versions;
	}

	private static void LabelsPostfix(object[] __args, ref List<string> __result)
	{
		try
		{
			if (__result == null || __result.Count == 0)
			{
				return;
			}
			List<string> cleaned = new List<string>();
			HashSet<string> seen = new HashSet<string>();
			foreach (string s in __result)
			{
				if (seen.Add(s))
				{
					cleaned.Add(s);
				}
			}
			Dictionary<string, string> versions = (__args != null && __args.Length > 0 && __args[0] != null) ? GetVersionsFor(__args[0]) : null;
			if (versions != null && versions.Count > 0)
			{
				for (int i = 0; i < cleaned.Count; i++)
				{
					string entry = cleaned[i];
					int open = entry.IndexOf('>');
					int close = entry.LastIndexOf("</color>", StringComparison.Ordinal);
					if (entry.StartsWith("<color=") && open > 0 && close > open)
					{
						string inner = entry.Substring(open + 1, close - open - 1);
						string ver;
						if (versions.TryGetValue(inner, out ver))
						{
							cleaned[i] = entry.Substring(0, close) + " " + ver + entry.Substring(close);
						}
					}
				}
			}
			__result = cleaned;
		}
		catch (Exception ex)
		{
			if (!_labelsErrLogged)
			{
				_labelsErrLogged = true;
				StatusLog("labels postfix failed: " + ex.Message);
			}
		}
	}

	internal static void StatsTick()
	{
		if (!PhotonNetwork.InRoom)
		{
			return;
		}
		Player[] players = PhotonNetwork.PlayerList;
		if (players == null)
		{
			return;
		}
		if (_statsPath == null)
		{
			_statsPath = Path.Combine(Paths.ConfigPath, "sakuraa_lobby_stats.csv");
		}
		Dictionary<string, string> index = GetLabelIndex();
		string day = DateTime.Now.ToString("yyyy-MM-dd");
		foreach (Player p in players)
		{
			if (p == null || p.IsLocal || p.CustomProperties == null || string.IsNullOrEmpty(p.UserId))
			{
				continue;
			}
			if (_statsSeen.Add(p.UserId + "|_player"))
			{
				File.AppendAllText(_statsPath, day + ",_PLAYER" + Environment.NewLine);
			}
			HashSet<string> labels = new HashSet<string>();
			foreach (var entry in p.CustomProperties)
			{
				string key = (entry.Key != null) ? entry.Key.ToString() : null;
				string label;
				if (key != null && index.TryGetValue(key.ToLowerInvariant(), out label))
				{
					labels.Add(label);
				}
			}
			foreach (string label in labels)
			{
				if (_statsSeen.Add(p.UserId + "|" + label))
				{
					File.AppendAllText(_statsPath, day + "," + label.Replace(',', ' ') + Environment.NewLine);
				}
			}
		}
	}
}
