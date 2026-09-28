using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using SakuraaCastingMod.Features.Soundboard;
using UnityEngine;

namespace SakuraaCameraClientStandalone;

// The original SoundboardPlayer builds "file:///" + FilePath and loads it with UnityWebRequest.
// On linux/proton that uri comes out wrong ("file:////home/..." or unescaped spaces), the request
// fails, and the tile just does nothing. This patch loads the wav straight from disk and drops the
// finished clip into SoundboardPlayer's own cache, so PlayRoutine finds it and skips the web request.
public static class SoundboardFix
{
	private static FieldInfo _cacheField;

	private static FieldInfo _lruField;

	public static void Install()
	{
		try
		{
			Type player = typeof(SoundboardPlayer);
			_cacheField = player.GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic);
			_lruField = player.GetField("_lru", BindingFlags.Static | BindingFlags.NonPublic);
			if (_cacheField == null || _lruField == null)
			{
				StandalonePlugin.StatusLog("soundboard fix failed: cache fields not found");
				return;
			}
			MethodInfo target = player.GetMethod("Play", BindingFlags.Static | BindingFlags.Public);
			MethodInfo prefix = typeof(SoundboardFix).GetMethod("PlayPrefix", BindingFlags.Static | BindingFlags.NonPublic);
			new Harmony("local.sakuraa.soundboardfix").Patch(target, prefix: new HarmonyMethod(prefix));
			StandalonePlugin.StatusLog("soundboard fix installed");
		}
		catch (Exception ex)
		{
			StandalonePlugin.StatusLog("soundboard fix failed: " + ex.Message);
		}
	}

	// runs before SoundboardPlayer.Play. parameter name has to match the original ("sound").
	private static void PlayPrefix(SoundboardManager.Sound sound)
	{
		if (sound == null)
		{
			return;
		}
		try
		{
			Dictionary<string, AudioClip> cache = (Dictionary<string, AudioClip>)_cacheField.GetValue(null);
			LinkedList<string> lru = (LinkedList<string>)_lruField.GetValue(null);
			if (cache.TryGetValue(sound.Id, out AudioClip existing) && existing != null)
			{
				return;
			}
			string path = ResolvePath(sound.FilePath);
			AudioClip clip = LoadWav(path, sound.Name, out string info);
			if (clip == null)
			{
				StandalonePlugin.StatusLog("soundboard: could not load '" + sound.Name + "' from [" + path + "] " + info);
				return;
			}
			StandalonePlugin.StatusLog("soundboard: loaded '" + sound.Name + "' " + info);
			cache[sound.Id] = clip;
			lru.Remove(sound.Id);
			lru.AddFirst(sound.Id);
			while (lru.Count > 8)
			{
				string last = lru.Last.Value;
				lru.RemoveLast();
				if (cache.TryGetValue(last, out AudioClip old))
				{
					cache.Remove(last);
					if (old != null && last != SoundboardPlayer.CurrentSoundId)
					{
						UnityEngine.Object.Destroy(old);
					}
				}
			}
		}
		catch (Exception ex)
		{
			StandalonePlugin.StatusLog("soundboard prefix error: " + ex.Message);
		}
	}

	private static string ResolvePath(string p)
	{
		if (string.IsNullOrEmpty(p))
		{
			return p;
		}
		p = p.Replace('\\', '/');
		if (File.Exists(p))
		{
			return p;
		}
		// under wine/proton a bare unix path sometimes only resolves through the Z: drive
		if (p.StartsWith("/") && File.Exists("Z:" + p))
		{
			return "Z:" + p;
		}
		return p;
	}

	// minimal riff/wave reader: 8/16/24/32-bit pcm, 32-bit float, and the "extensible" header variant
	private static AudioClip LoadWav(string path, string name, out string info)
	{
		info = "";
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			info = "(file not found)";
			return null;
		}
		byte[] b = File.ReadAllBytes(path);
		if (b.Length < 12 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF" || Encoding.ASCII.GetString(b, 8, 4) != "WAVE")
		{
			info = "(not a RIFF/WAVE file)";
			return null;
		}
		int fmtTag = 0;
		int channels = 0;
		int rate = 0;
		int bits = 0;
		int dataOff = -1;
		int dataLen = 0;
		int p = 12;
		while (p + 8 <= b.Length)
		{
			string id = Encoding.ASCII.GetString(b, p, 4);
			int size = BitConverter.ToInt32(b, p + 4);
			int body = p + 8;
			if (id == "fmt " && body + 16 <= b.Length)
			{
				fmtTag = BitConverter.ToUInt16(b, body);
				channels = BitConverter.ToUInt16(b, body + 2);
				rate = BitConverter.ToInt32(b, body + 4);
				bits = BitConverter.ToUInt16(b, body + 14);
				if (fmtTag == 0xFFFE && size >= 26 && body + 26 <= b.Length)
				{
					fmtTag = BitConverter.ToUInt16(b, body + 24);
				}
			}
			else if (id == "data")
			{
				dataOff = body;
				dataLen = size;
				if (dataLen < 0 || dataOff + dataLen > b.Length)
				{
					dataLen = b.Length - dataOff;
				}
				break;
			}
			if (size < 0)
			{
				break;
			}
			p = body + size + (size & 1);
		}
		info = "(" + channels + "ch " + rate + "hz " + bits + "bit tag " + fmtTag + ")";
		if (dataOff < 0 || channels <= 0 || rate <= 0 || bits <= 0)
		{
			return null;
		}
		bool isFloat = fmtTag == 3;
		if (fmtTag != 1 && !isFloat)
		{
			info += " unsupported encoding";
			return null;
		}
		int bytesPer = bits / 8;
		int total = dataLen / bytesPer;
		int frames = total / channels;
		total = frames * channels;
		if (frames <= 0)
		{
			return null;
		}
		float[] samples = new float[total];
		for (int i = 0; i < total; i++)
		{
			int o = dataOff + i * bytesPer;
			float v;
			if (isFloat && bits == 32)
			{
				v = BitConverter.ToSingle(b, o);
			}
			else if (bits == 8)
			{
				v = (b[o] - 128) / 128f;
			}
			else if (bits == 16)
			{
				v = BitConverter.ToInt16(b, o) / 32768f;
			}
			else if (bits == 24)
			{
				v = ((b[o] << 8) | (b[o + 1] << 16) | (b[o + 2] << 24)) >> 8;
				v /= 8388608f;
			}
			else if (bits == 32)
			{
				v = BitConverter.ToInt32(b, o) / 2147483648f;
			}
			else
			{
				info += " unsupported bit depth";
				return null;
			}
			samples[i] = v;
		}
		AudioClip clip = AudioClip.Create(name, frames, channels, rate, false);
		clip.SetData(samples, 0);
		return clip;
	}
}
