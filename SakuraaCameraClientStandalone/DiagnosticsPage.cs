using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using Photon.Pun;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using UnityEngine;
using UnityEngine.Networking;

namespace SakuraaCameraClientStandalone;

public sealed class DiagnosticsPage : BasePage
{
	public override string PageName => "DIAGNOSTICS";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.Server;

	public override void BuildTabs()
	{
		Tabs.Clear();
		UtilTab health = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = "Health" };
		foreach (string line in StandalonePlugin.HealthLines())
		{
			health.Elements.Add(new MenuElement(line, delegate { }));
		}
		Tabs.Add(health);

		UtilTab perf = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = "Perf" };
		foreach (string line in StandalonePlugin.PerfLines())
		{
			perf.Elements.Add(new MenuElement(line, delegate { }));
		}
		Tabs.Add(perf);

		UtilTab helpers = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = "Helpers" };
		foreach (string line in StandalonePlugin.HelperLines())
		{
			helpers.Elements.Add(new MenuElement(line, delegate { }));
		}
		Tabs.Add(helpers);

		UtilTab errors = new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.Server, TabName = "Errors" };
		string[] errs = StandalonePlugin.RecentErrorLines();
		if (errs.Length == 0)
		{
			errors.Elements.Add(new MenuElement("NO RECENT ERRORS", delegate { }));
		}
		else
		{
			foreach (string line in errs)
			{
				errors.Elements.Add(new MenuElement(line, delegate { }));
			}
		}
		Tabs.Add(errors);
	}

	public override void RefreshPageUI()
	{
		BuildTabs();
	}
}

internal sealed class DiagnosticsTicker : MonoBehaviour
{
	private float _next;

	private void Update()
	{
		if (Time.unscaledTime < _next)
		{
			return;
		}
		_next = Time.unscaledTime + 0.5f;
		StandalonePlugin.PerfTick();
	}

	private void Start()
	{
		StartCoroutine(HelperPingLoop());
	}

	private IEnumerator HelperPingLoop()
	{
		while (true)
		{
			using (UnityWebRequest req = UnityWebRequest.Get("http://127.0.0.1:8765/state"))
			{
				req.timeout = 2;
				yield return req.SendWebRequest();
				StandalonePlugin.LyricsHelperOnline = !req.isNetworkError && !req.isHttpError;
			}
			yield return new WaitForSecondsRealtime(4f);
		}
	}
}

public sealed partial class StandalonePlugin
{
	private static DateTime _sessionStart = DateTime.Now;
	private static int _diagFps;
	private static float _diagFrameMs;
	private static int _diagPing = -1;
	private static MethodInfo _pingMethod;
	internal static bool LyricsHelperOnline;
	private static float _fpsAccum;
	private static int _fpsFrames;
	private static float _fpsTimer;

	private static void StartDiagnostics()
	{
		try
		{
			_sessionStart = DateTime.Now;
			_pingMethod = typeof(PhotonNetwork).GetMethod("GetPing", BindingFlags.Static | BindingFlags.Public);
			GameObject go = new GameObject("SakuraaDiagnosticsTicker");
			UnityEngine.Object.DontDestroyOnLoad(go);
			go.AddComponent<DiagnosticsTicker>();
			StatusLog("diagnostics page installed");
		}
		catch (Exception ex)
		{
			StatusLog("diagnostics failed: " + ex.Message);
		}
	}

	internal static void PerfTick()
	{
		_fpsFrames++;
		_fpsTimer += Time.unscaledDeltaTime;
		if (_fpsTimer >= 0.5f)
		{
			_diagFps = Mathf.RoundToInt(_fpsFrames / _fpsTimer);
			_diagFrameMs = (_fpsTimer / _fpsFrames) * 1000f;
			_fpsFrames = 0;
			_fpsTimer = 0f;
		}
		try
		{
			_diagPing = (_pingMethod != null) ? (int)_pingMethod.Invoke(null, null) : -1;
		}
		catch
		{
			_diagPing = -1;
		}
	}

	internal static string[] HealthLines()
	{
		return new string[8]
		{
			"KNOWN LISTS: " + (_listsReady ? "OK" : "NOT LOADED"),
			"SETTINGS LOADED: " + (_settingsLoaded ? "OK" : "NOT YET"),
			"CONFIG SAVING: " + ((_cfgPath != null) ? "OK" : "FAILED"),
			"LABEL CLEANUP: " + ((_kpage != null) ? "OK" : "FAILED"),
			"THEME KEEP: " + ((_themeReapply != null) ? "OK" : "FAILED"),
			"THEME EXTRAS: " + ((_themeMgrType != null) ? "OK" : "FAILED"),
			"NAMETAG PLUS: " + ((_tagRankVisuals != null) ? "OK" : "PARTIAL/FAILED"),
			"LYRICS BRIDGE: " + (LyricsLoaded() ? "MOD LOADED" : "NOT LOADED")
		};
	}

	internal static string[] PerfLines()
	{
		TimeSpan up = DateTime.Now - _sessionStart;
		string pingText = (_diagPing >= 0) ? (_diagPing + " ms") : "n/a";
		return new string[5]
		{
			"FPS: " + _diagFps,
			"FRAME TIME: " + _diagFrameMs.ToString("F1") + " ms",
			"PING: " + pingText,
			"MEMORY: " + (GC.GetTotalMemory(false) / 1048576f).ToString("F0") + " MB",
			"UPTIME: " + up.Hours + "h " + up.Minutes + "m " + up.Seconds + "s"
		};
	}

	internal static string[] HelperLines()
	{
		string saveStatus = "not saved yet";
		try
		{
			string path = Path.Combine(Paths.ConfigPath, "SakuraaCameraClient", "CamModSettingsSave.json");
			if (File.Exists(path))
			{
				saveStatus = File.GetLastWriteTime(path).ToString("HH:mm:ss");
			}
		}
		catch
		{
		}
		return new string[3]
		{
			"LYRICS HELPER (8765): " + (LyricsHelperOnline ? "ONLINE" : "OFFLINE"),
			"MEDIA HELPER (5001): UDP, CAN'T VERIFY",
			"LAST SETTINGS SAVE: " + saveStatus
		};
	}

	internal static string[] RecentErrorLines()
	{
		try
		{
			string path = Path.Combine(Paths.ConfigPath, "sakuraa_status.txt");
			if (!File.Exists(path))
			{
				return new string[0];
			}
			string[] lines = File.ReadAllLines(path);
			System.Collections.Generic.List<string> hits = new System.Collections.Generic.List<string>();
			for (int i = lines.Length - 1; i >= 0 && hits.Count < 6; i--)
			{
				string lower = lines[i].ToLowerInvariant();
				if (lower.Contains("fail") || lower.Contains("error"))
				{
					string line = lines[i];
					if (line.Length > 34)
					{
						line = line.Substring(0, 34);
					}
					hits.Add(line);
				}
			}
			return hits.ToArray();
		}
		catch
		{
			return new string[1] { "error reading log" };
		}
	}
}
