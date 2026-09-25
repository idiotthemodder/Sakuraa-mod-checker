using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed partial class StandalonePlugin
{
	private sealed class TagLabelInfo
	{
		public float Time;
		public string Text = "";
		public int Cat = -1;
	}

	internal static bool TagFaceMe = true;
	internal static bool TagJoinDate;
	internal static bool TagLabel;
	internal static bool TagLabelColor;
	internal static bool TagTimeInLobby;
	internal static bool TagDistScale;

	private static Type _tagRankVisuals;
	private static FieldInfo _tagJoinDatesField;
	private static Transform _tagFaceTarget;
	private static float _tagFaceCheck = -10f;
	private static Dictionary<string, KeyValuePair<string, int>> _tagCatIndex;
	private static float _tagCatTime = -10f;
	private static readonly Dictionary<int, Vector3> _tagBaseScale = new Dictionary<int, Vector3>();
	private static readonly Dictionary<string, float> _tagFirstSeen = new Dictionary<string, float>();
	private static readonly Dictionary<string, string> _tagLastWritten = new Dictionary<string, string>();
	private static readonly Dictionary<string, TagLabelInfo> _tagLabelCache = new Dictionary<string, TagLabelInfo>();
	private static readonly HashSet<string> _tagErrSeen = new HashSet<string>();

	private static string TagSettingsPath
	{
		get
		{
			return Path.Combine(Paths.ConfigPath, "sakuraa_nametags.txt");
		}
	}

	internal static void LoadTagSettings()
	{
		try
		{
			if (!File.Exists(TagSettingsPath))
			{
				return;
			}
			foreach (string raw in File.ReadAllLines(TagSettingsPath))
			{
				int eq = raw.IndexOf('=');
				if (eq <= 0)
				{
					continue;
				}
				string key = raw.Substring(0, eq).Trim();
				bool on = raw.Substring(eq + 1).Trim() == "1";
				if (key == "FaceMe") TagFaceMe = on;
				else if (key == "JoinDate") TagJoinDate = on;
				else if (key == "Label") TagLabel = on;
				else if (key == "LabelColor") TagLabelColor = on;
				else if (key == "TimeInLobby") TagTimeInLobby = on;
				else if (key == "DistScale") TagDistScale = on;
			}
		}
		catch (Exception ex)
		{
			StatusLog("nametag settings load failed: " + ex.Message);
		}
	}

	internal static void SaveTagSettings()
	{
		try
		{
			string[] lines = new string[6]
			{
				"FaceMe=" + (TagFaceMe ? "1" : "0"),
				"JoinDate=" + (TagJoinDate ? "1" : "0"),
				"Label=" + (TagLabel ? "1" : "0"),
				"LabelColor=" + (TagLabelColor ? "1" : "0"),
				"TimeInLobby=" + (TagTimeInLobby ? "1" : "0"),
				"DistScale=" + (TagDistScale ? "1" : "0")
			};
			File.WriteAllLines(TagSettingsPath, lines);
		}
		catch (Exception ex)
		{
			StatusLog("nametag settings save failed: " + ex.Message);
		}
	}

	private static void TagErr(string what, Exception ex)
	{
		if (_tagErrSeen.Add(what))
		{
			StatusLog("nametag " + what + " error: " + ex.Message);
		}
	}

	private static object GetMember(object obj, string name)
	{
		if (obj == null)
		{
			return null;
		}
		BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		Type type = obj.GetType();
		FieldInfo field = type.GetField(name, flags);
		if (field != null)
		{
			return field.GetValue(obj);
		}
		PropertyInfo prop = type.GetProperty(name, flags);
		return (prop != null) ? prop.GetValue(obj, null) : null;
	}

	private static void StartNameTagPlus(Assembly core)
	{
		try
		{
			LoadTagSettings();
			_tagRankVisuals = core.GetType("SakuraaCastingMod.Features.Visuals.RankVisuals");
			if (_tagRankVisuals != null)
			{
				_tagJoinDatesField = _tagRankVisuals.GetField("PlayerJoinDates", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			BindingFlags all = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			Type tags = core.GetType("SakuraaCastingMod.Features.Visuals.NameTags", throwOnError: true);
			MethodInfo updateTransform = tags.GetMethod("UpdateTransform", all);
			MethodInfo updateVisual = tags.GetMethod("UpdateVisualData", all);
			if (updateTransform == null || updateVisual == null)
			{
				throw new MissingMethodException(tags.FullName, "UpdateTransform/UpdateVisualData");
			}
			HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("local.sakuraa.nametagplus");
			harmony.Patch(updateTransform, prefix: new HarmonyLib.HarmonyMethod(typeof(StandalonePlugin).GetMethod("TagFacePrefix", all)), postfix: new HarmonyLib.HarmonyMethod(typeof(StandalonePlugin).GetMethod("TagScalePostfix", all)));
			harmony.Patch(updateVisual, postfix: new HarmonyLib.HarmonyMethod(typeof(StandalonePlugin).GetMethod("TagTextPostfix", all)));
			StatusLog("nametag plus patch installed");
		}
		catch (Exception ex)
		{
			StatusLog("nametag plus patch failed: " + ex.Message);
		}
	}

	private static Transform TagFaceTargetNow()
	{
		if (_tagFaceTarget != null && Time.unscaledTime - _tagFaceCheck < 1f)
		{
			return _tagFaceTarget;
		}
		_tagFaceCheck = Time.unscaledTime;
		Transform found = null;
		try
		{
			Type tagger = null;
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				tagger = assembly.GetType("GorillaTagger");
				if (tagger != null)
				{
					break;
				}
			}
			if (tagger != null)
			{
				BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
				object instance = null;
				PropertyInfo instProp = tagger.GetProperty("Instance", statics);
				if (instProp != null)
				{
					instance = instProp.GetValue(null, null);
				}
				else
				{
					FieldInfo instField = tagger.GetField("Instance", statics);
					if (instField != null)
					{
						instance = instField.GetValue(null);
					}
				}
				Component head = GetMember(instance, "headCollider") as Component;
				if (head != null)
				{
					found = head.transform;
				}
			}
		}
		catch
		{
		}
		if (found == null && Camera.main != null)
		{
			found = Camera.main.transform;
		}
		_tagFaceTarget = found;
		return found;
	}

	private static void TagFacePrefix(ref Transform __2)
	{
		if (!TagFaceMe)
		{
			return;
		}
		try
		{
			Transform target = TagFaceTargetNow();
			if (target != null)
			{
				__2 = target;
			}
		}
		catch (Exception ex)
		{
			TagErr("face", ex);
		}
	}

	private static void TagScalePostfix(object __1)
	{
		try
		{
			Transform tag = GetMember(__1, "MainTransform") as Transform;
			if (tag == null)
			{
				return;
			}
			int id = tag.GetInstanceID();
			Vector3 baseScale;
			if (!TagDistScale)
			{
				if (_tagBaseScale.TryGetValue(id, out baseScale))
				{
					tag.localScale = baseScale;
					_tagBaseScale.Remove(id);
				}
				return;
			}
			Transform head = TagFaceTargetNow();
			if (head == null)
			{
				return;
			}
			if (!_tagBaseScale.TryGetValue(id, out baseScale))
			{
				baseScale = tag.localScale;
				_tagBaseScale[id] = baseScale;
			}
			float factor = Mathf.Clamp(Vector3.Distance(tag.position, head.position) / 3f, 1f, 3f);
			tag.localScale = baseScale * factor;
		}
		catch (Exception ex)
		{
			TagErr("scale", ex);
		}
	}

	private static Dictionary<string, KeyValuePair<string, int>> TagCategoryIndex()
	{
		if (_tagCatIndex != null && Time.unscaledTime - _tagCatTime < 2f)
		{
			return _tagCatIndex;
		}
		Dictionary<string, KeyValuePair<string, int>> index = new Dictionary<string, KeyValuePair<string, int>>();
		if (_kpage != null)
		{
			string[] names = new string[5] { "_hardcodedSafeMods", "_webKnownMods", "_hardcodedUnknownMods", "_hardcodedCheats", "_webKnownCheats" };
			int[] cats = new int[5] { 0, 0, 1, 2, 2 };
			for (int i = 0; i < 5; i++)
			{
				FieldInfo field = _kpage.GetField(names[i], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				Dictionary<string, string> dict = (field != null) ? (field.GetValue(null) as Dictionary<string, string>) : null;
				if (dict == null)
				{
					continue;
				}
				foreach (KeyValuePair<string, string> kv in dict)
				{
					string key = kv.Key.ToLowerInvariant();
					KeyValuePair<string, int> old;
					if (!index.TryGetValue(key, out old) || cats[i] > old.Value)
					{
						index[key] = new KeyValuePair<string, int>(kv.Value.ToUpperInvariant(), cats[i]);
					}
				}
			}
		}
		_tagCatIndex = index;
		_tagCatTime = Time.unscaledTime;
		return index;
	}

	private static TagLabelInfo TagLabelsFor(string userId)
	{
		TagLabelInfo info;
		if (_tagLabelCache.TryGetValue(userId, out info) && Time.unscaledTime - info.Time < 1f)
		{
			return info;
		}
		info = new TagLabelInfo();
		info.Time = Time.unscaledTime;
		try
		{
			Dictionary<string, KeyValuePair<string, int>> index = TagCategoryIndex();
			Player[] players = PhotonNetwork.PlayerList;
			if (players != null)
			{
				foreach (Player p in players)
				{
					if (p == null || p.UserId != userId || p.CustomProperties == null)
					{
						continue;
					}
					List<string> labels = new List<string>();
					string best = "";
					foreach (var entry in p.CustomProperties)
					{
						string key = (entry.Key != null) ? entry.Key.ToString() : null;
						KeyValuePair<string, int> hit;
						if (key == null || !index.TryGetValue(key.ToLowerInvariant(), out hit))
						{
							continue;
						}
						if (!labels.Contains(hit.Key))
						{
							labels.Add(hit.Key);
						}
						if (hit.Value > info.Cat)
						{
							info.Cat = hit.Value;
							best = hit.Key;
						}
					}
					if (labels.Count > 0)
					{
						info.Text = best + ((labels.Count > 1) ? (" +" + (labels.Count - 1)) : "");
					}
					break;
				}
			}
		}
		catch
		{
		}
		_tagLabelCache[userId] = info;
		return info;
	}

	private static void TagTextPostfix(object __0, object __1)
	{
		try
		{
			if (!TagJoinDate && !TagLabel && !TagTimeInLobby)
			{
				return;
			}
			object hz = GetMember(__1, "HzText");
			string userId = GetMember(__0, "UserId") as string;
			if (hz == null || string.IsNullOrEmpty(userId))
			{
				return;
			}
			PropertyInfo textProp = hz.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
			if (textProp == null)
			{
				return;
			}
			string current = textProp.GetValue(hz, null) as string;
			string last;
			if (_tagLastWritten.TryGetValue(userId, out last) && current == last)
			{
				return;
			}
			List<string> parts = new List<string>();
			if (!string.IsNullOrEmpty(current))
			{
				parts.Add(current);
			}
			TagLabelInfo label = null;
			if (TagLabel)
			{
				label = TagLabelsFor(userId);
				if (label.Text.Length > 0)
				{
					parts.Add(label.Text);
				}
			}
			if (TagJoinDate && _tagJoinDatesField != null)
			{
				IDictionary dates = _tagJoinDatesField.GetValue(null) as IDictionary;
				if (dates != null && dates.Contains(userId))
				{
					DateTime created = (DateTime)dates[userId];
					parts.Add(created.ToString("yyyy-MM-dd"));
				}
			}
			if (TagTimeInLobby)
			{
				if (!PhotonNetwork.InRoom)
				{
					_tagFirstSeen.Clear();
				}
				float first;
				if (!_tagFirstSeen.TryGetValue(userId, out first))
				{
					first = Time.unscaledTime;
					_tagFirstSeen[userId] = first;
				}
				int minutes = (int)((Time.unscaledTime - first) / 60f);
				parts.Add((minutes < 1) ? "NEW" : ((minutes >= 60) ? ((minutes / 60) + "h") : (minutes + "m")));
			}
			string composed = string.Join(" | ", parts.ToArray());
			if (composed != current)
			{
				textProp.SetValue(hz, composed, null);
			}
			_tagLastWritten[userId] = composed;
			if (TagLabelColor && label != null && label.Cat >= 0 && label.Text.Length > 0)
			{
				PropertyInfo colorProp = hz.GetType().GetProperty("color", BindingFlags.Instance | BindingFlags.Public);
				if (colorProp != null)
				{
					Color tint = (label.Cat == 2) ? new Color(1f, 0.25f, 0.25f) : ((label.Cat == 1) ? new Color(1f, 0.9f, 0.2f) : new Color(0.3f, 1f, 0.4f));
					colorProp.SetValue(hz, tint, null);
				}
			}
		}
		catch (Exception ex)
		{
			TagErr("text", ex);
		}
	}
}
