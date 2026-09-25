using System;
using SakuraaCastingMod.Shared.Models;
using SakuraaCastingMod.VR.UtilMenu;
using SakuraaCastingMod.VR.UtilMenu.Pages;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed class NameTagPlusPage : BasePage
{
	public override string PageName => "NAMETAGS+";

	public override Material PageIcon => UtilMenuMain.Instance.Icons.Paintbrush;

	public override void BuildTabs()
	{
		Tabs.Clear();
		UtilTab tab = new UtilTab
		{
			TabIcon = UtilMenuMain.Instance.Icons.Paintbrush,
			TabName = "Tags"
		};
		tab.Elements.Add(new MenuElement("FACE ME", delegate
		{
			StandalonePlugin.TagFaceMe = !StandalonePlugin.TagFaceMe;
			Changed();
		}, StandalonePlugin.TagFaceMe));
		tab.Elements.Add(new MenuElement("JOIN DATE", delegate
		{
			StandalonePlugin.TagJoinDate = !StandalonePlugin.TagJoinDate;
			Changed();
		}, StandalonePlugin.TagJoinDate));
		tab.Elements.Add(new MenuElement("FLAG LABEL", delegate
		{
			StandalonePlugin.TagLabel = !StandalonePlugin.TagLabel;
			Changed();
		}, StandalonePlugin.TagLabel));
		tab.Elements.Add(new MenuElement("LABEL COLOR", delegate
		{
			StandalonePlugin.TagLabelColor = !StandalonePlugin.TagLabelColor;
			Changed();
		}, StandalonePlugin.TagLabelColor));
		tab.Elements.Add(new MenuElement("TIME IN LOBBY", delegate
		{
			StandalonePlugin.TagTimeInLobby = !StandalonePlugin.TagTimeInLobby;
			Changed();
		}, StandalonePlugin.TagTimeInLobby));
		tab.Elements.Add(new MenuElement("DIST SCALE", delegate
		{
			StandalonePlugin.TagDistScale = !StandalonePlugin.TagDistScale;
			Changed();
		}, StandalonePlugin.TagDistScale));
		Tabs.Add(tab);
	}

	public override void RefreshPageUI()
	{
		BuildTabs();
	}

	private static void Changed()
	{
		StandalonePlugin.SaveTagSettings();
		if (UtilMenuController.Instance != null)
		{
			UtilMenuController.Instance.RefreshUI();
		}
	}
}
