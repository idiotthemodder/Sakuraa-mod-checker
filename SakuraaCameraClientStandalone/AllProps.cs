using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed class AllPropsDumper : MonoBehaviour
{
	private float _next;

	private void Update()
	{
		if (Time.unscaledTime < _next)
		{
			return;
		}
		_next = Time.unscaledTime + 5f;
		StandalonePlugin.AllPropsTick();
	}
}

public sealed partial class StandalonePlugin
{
	private static string _allPropsPath;
	private static bool _allPropsErrLogged;

	private static void StartAllPropsDump()
	{
		_allPropsPath = Path.Combine(Paths.ConfigPath, "sakuraa_all_props.txt");
		GameObject go = new GameObject("SakuraaAllPropsDumper");
		UnityEngine.Object.DontDestroyOnLoad(go);
		go.AddComponent<AllPropsDumper>();
	}

	internal static void AllPropsTick()
	{
		try
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
			HashSet<string> known = BuildKnownKeys();
			Dictionary<string, int> counts = new Dictionary<string, int>();
			Dictionary<string, string> samples = new Dictionary<string, string>();
			int others = 0;
			foreach (Player p in players)
			{
				if (p == null || p.IsLocal || p.CustomProperties == null)
				{
					continue;
				}
				others++;
				foreach (var entry in p.CustomProperties)
				{
					string key = (entry.Key != null) ? entry.Key.ToString() : null;
					if (string.IsNullOrEmpty(key))
					{
						continue;
					}
					string escaped = EscapeText(key);
					int current;
					counts.TryGetValue(escaped, out current);
					counts[escaped] = current + 1;
					if (!samples.ContainsKey(escaped))
					{
						string value = (entry.Value != null) ? EscapeText(entry.Value.ToString()) : "null";
						Guid parsed;
						if (Guid.TryParse(value, out parsed))
						{
							value = "<id>";
						}
						if (value.Length > 40)
						{
							value = value.Substring(0, 40) + "...";
						}
						samples[escaped] = value;
					}
				}
			}
			List<KeyValuePair<string, int>> sorted = new List<KeyValuePair<string, int>>(counts);
			sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("# snapshot " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ", " + others + " other players");
			sb.AppendLine("# key | players with it | known or NEW | sample value");
			foreach (KeyValuePair<string, int> kv in sorted)
			{
				string status = known.Contains(kv.Key) ? "known" : "NEW";
				sb.AppendLine(kv.Key + " | " + kv.Value + "/" + others + " | " + status + " | " + samples[kv.Key]);
			}
			File.WriteAllText(_allPropsPath, sb.ToString());
		}
		catch (Exception ex)
		{
			if (!_allPropsErrLogged)
			{
				_allPropsErrLogged = true;
				Debug.LogError((object)("[AllProps] " + ex.Message));
			}
		}
	}
}
