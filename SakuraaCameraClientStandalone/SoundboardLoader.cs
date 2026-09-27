using System;
using System.IO;
using BepInEx;
using Newtonsoft.Json.Linq;
using SakuraaCastingMod.Features.Soundboard;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

public sealed partial class StandalonePlugin
{
	private static string SoundboardFolder => Path.Combine(Paths.ConfigPath, "SakuraaSounds");

	internal static void LoadSoundboardFromDisk()
	{
		try
		{
			string folder = SoundboardFolder;
			if (!Directory.Exists(folder))
			{
				Directory.CreateDirectory(folder);
				StatusLog("soundboard folder created at " + folder + " - drop .wav files in, subfolders become pages");
				return;
			}

			JArray soundsArray = new JArray();
			JArray pagesArray = new JArray();

			void AddSound(string filePath)
			{
				soundsArray.Add(new JObject
				{
					["file"] = Path.GetFileName(filePath),
					["name"] = Path.GetFileNameWithoutExtension(filePath).Replace('_', ' '),
					["durationMs"] = 0
				});
			}

			foreach (string file in Directory.GetFiles(folder, "*.wav"))
			{
				AddSound(file);
			}

			int order = 0;
			foreach (string subDir in Directory.GetDirectories(folder))
			{
				JArray pageIds = new JArray();
				foreach (string file in Directory.GetFiles(subDir, "*.wav"))
				{
					AddSound(file);
					pageIds.Add(Path.GetFileNameWithoutExtension(file));
				}
				if (pageIds.Count > 0)
				{
					pagesArray.Add(new JObject
					{
						["name"] = Path.GetFileName(subDir),
						["soundIds"] = pageIds,
						["order"] = order++
					});
				}
			}

			SoundboardManager.HandleManifest(new JObject
			{
				["folder"] = folder,
				["sounds"] = soundsArray,
				["pages"] = pagesArray
			});

			StatusLog("soundboard loaded " + SoundboardManager.SoundCount + " sounds from " + folder);
		}
		catch (Exception ex)
		{
			StatusLog("soundboard load failed: " + ex.Message);
		}
	}
}
