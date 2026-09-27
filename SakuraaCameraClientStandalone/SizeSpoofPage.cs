using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed class SizeSpoofPage : BasePage
{
	public override string PageName => "SIZE";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.MoveArrows;

	private static UtilTab NewTab(string name)
	{
		return new UtilTab { TabIcon = UtilMenuMain.Instance.Icons.MoveArrows, TabName = name };
	}

	public override void BuildTabs()
	{
		Tabs.Clear();

		UtilTab main = NewTab("Auto");
		main.Elements.Add(new MenuElement("AUTO RANDOM", delegate
		{
			StandalonePlugin.SizeSpoofToggle();
			Refresh();
		}, StandalonePlugin.SizeSpoofEnabled));
		main.Elements.Add(FloatSlider("MIN", () => StandalonePlugin.SizeSpoofMin, v => StandalonePlugin.SizeSpoofMin = Clamp(v), 0.1f, 0.1f, 5f));
		main.Elements.Add(FloatSlider("MAX", () => StandalonePlugin.SizeSpoofMax, v => StandalonePlugin.SizeSpoofMax = Clamp(v), 0.1f, 0.1f, 5f));
		main.Elements.Add(FloatSlider("INTERVAL (S)", () => StandalonePlugin.SizeSpoofInterval, v => StandalonePlugin.SizeSpoofInterval = Mathf.Clamp(v, 0.1f, 30f), 0.1f, 0.1f, 30f));
		main.Elements.Add(new MenuElement("CURRENT: " + StandalonePlugin.SizeSpoofLast.ToString("F2"), delegate { }));
		Tabs.Add(main);

		UtilTab manual = NewTab("Manual");
		manual.Elements.Add(new MenuElement("RANDOM ONCE", delegate
		{
			StandalonePlugin.SizeSpoofRandomOnce();
			Refresh();
		}));
		manual.Elements.Add(new MenuElement("TINY (0.3)", delegate
		{
			StandalonePlugin.SizeSpoofSet(0.3f);
			Refresh();
		}));
		manual.Elements.Add(new MenuElement("GIANT (3.0)", delegate
		{
			StandalonePlugin.SizeSpoofSet(3f);
			Refresh();
		}));
		manual.Elements.Add(new MenuElement("RESET (1.0)", delegate
		{
			StandalonePlugin.SizeSpoofSet(1f);
			Refresh();
		}));
		Tabs.Add(manual);
	}

	public override void RefreshPageUI()
	{
		BuildTabs();
	}

	private static void Refresh()
	{
		if (UtilMenuController.Instance != null)
		{
			UtilMenuController.Instance.RefreshUI();
		}
	}

	private static float Clamp(float v) => Mathf.Clamp(v, 0.1f, 5f);

	private static MenuElement FloatSlider(string label, Func<float> get, Action<float> set, float step, float min, float max)
	{
		MenuElement element = null;
		element = new MenuElement(label, get().ToString("F2"), delegate
		{
			set(Mathf.Clamp(get() - step, min, max));
			element.ValueText = get().ToString("F2");
			Refresh();
		}, delegate
		{
			set(Mathf.Clamp(get() + step, min, max));
			element.ValueText = get().ToString("F2");
			Refresh();
		});
		return element;
	}
}

internal sealed class SizeSpoofTicker : MonoBehaviour
{
	private float _next;

	private void Update()
	{
		if (!StandalonePlugin.SizeSpoofEnabled || Time.unscaledTime < _next)
		{
			return;
		}
		_next = Time.unscaledTime + Mathf.Max(0.1f, StandalonePlugin.SizeSpoofInterval);
		StandalonePlugin.SizeSpoofRandomOnce();
	}
}

public sealed partial class StandalonePlugin
{
	private const string SizeSpoofPropKey = "GratePlayerSize";

	internal static bool SizeSpoofEnabled;
	internal static float SizeSpoofMin = 0.5f;
	internal static float SizeSpoofMax = 2f;
	internal static float SizeSpoofInterval = 1f;
	internal static float SizeSpoofLast = 1f;

	private static void StartSizeSpoof()
	{
		try
		{
			GameObject go = new GameObject("SakuraaSizeSpoofTicker");
			UnityEngine.Object.DontDestroyOnLoad(go);
			go.AddComponent<SizeSpoofTicker>();
			StatusLog("size spoof page installed");
		}
		catch (Exception ex)
		{
			StatusLog("size spoof install failed: " + ex.Message);
		}
	}

	internal static void SizeSpoofToggle()
	{
		SizeSpoofEnabled = !SizeSpoofEnabled;
		StatusLog("size spoof auto: " + (SizeSpoofEnabled ? "on" : "off"));
	}

	internal static void SizeSpoofRandomOnce()
	{
		float lo = Mathf.Min(SizeSpoofMin, SizeSpoofMax);
		float hi = Mathf.Max(SizeSpoofMin, SizeSpoofMax);
		SizeSpoofSet(UnityEngine.Random.Range(lo, hi));
	}

	internal static void SizeSpoofSet(float size)
	{
		try
		{
			if (PhotonNetwork.LocalPlayer == null || !PhotonNetwork.InRoom)
			{
				StatusLog("size spoof: not in a room, nothing sent");
				return;
			}
			SizeSpoofLast = size;
			PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
			{
				[SizeSpoofPropKey] = size
			});
		}
		catch (Exception ex)
		{
			StatusLog("size spoof set failed: " + ex.Message);
		}
	}
}
