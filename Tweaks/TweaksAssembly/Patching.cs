using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine.Experimental.Rendering;

namespace TweaksAssembly.Patching
{
	static class Patching
	{
		private static readonly Dictionary<string, Harmony> instances = new Dictionary<string, Harmony>();

		public static bool PatchClasses(string id, out Harmony instance, params Type[] types)
		{
			instance = new Harmony($"samfundev.tweaks.{id}");
			if (!Harmony.DEBUG)
				Harmony.DEBUG = true;
			var success = false;

			foreach (Type type in types)
			{
				var result = instance.CreateClassProcessor(type).Patch();
				success |= result?.Count > 0;
			}

			return success;
		}

		public static void EnsurePatch(string id, params Type[] types)
		{
			if (instances.ContainsKey(id))
				return;

			if (PatchClasses(id, out Harmony instance, types))
			{
				instances.Add(id, instance);
			}
		}

		public static Harmony ManualInstance(string id)
		{
			if (!instances.ContainsKey(id))
				instances.Add(id, new Harmony($"samfundev.tweaks.{id}"));

			return instances[id];
		}
	}

	#pragma warning disable IDE0051
	[HarmonyPatch]
	static class LogfileUploaderPatch
	{
		static MethodBase method;

		static bool Prepare()
		{
			var type = ReflectionHelper.FindType("LogfileUploader");
			method = type?.GetMethod("HandleLog", BindingFlags.NonPublic | BindingFlags.Instance);
			return method != null;
		}

		static MethodBase TargetMethod() => method;

		static void Postfix(object __instance, string stackTrace)
		{
			if (string.IsNullOrEmpty(stackTrace) || !__instance.GetValue<bool>("loggingEnabled"))
				return;

			__instance.SetValue("Log", __instance.GetValue<string>("Log") + stackTrace + "\n");
			__instance.SetValue("LastBombLog", __instance.GetValue<string>("LastBombLog") + stackTrace + "\n");
		}
	}
	#pragma warning restore IDE0051

	[HarmonyPatch(typeof(UnityEngine.Random))]
	static class RandomRangePatch
	{
		[HarmonyPrefix]
		[HarmonyPatch("Range", typeof(int), typeof(int))]
		static bool Prefix1(int min, int max)
		{
			new Trace("[(int) Random.Range] Called by:");
			return true;
		}

		/*[HarmonyPrefix]
		[HarmonyPatch("Range", typeof(float), typeof(float))]
		static bool Prefix2(float min, float max)
		{
			new Trace("[(float) Random.Range] Called by:");
			UnityEngine.Debug.Log("[(float) Random.Range] Values: " + min + ", " + max + ".");
			return true;
		}*/

		[HarmonyPostfix]
		[HarmonyPatch("Range", typeof(int), typeof(int))]
		static void Postfix1(ref int __result)
		{
			new Log("[Random.Range]", __result.ToString());
		}
	}

	[HarmonyPatch(typeof(Random))]
	static class RandomNextPatch
	{
		[HarmonyPrefix]
		[HarmonyPatch("Next", new Type[] { })]
		static bool Prefix1()
		{
			new Trace("[(int) Random.Next] Called by:");
			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch("Next", typeof(int), typeof(int))]
		static bool Prefix2(int minValue, int maxValue)
		{
			new Trace("[(int) Random.Next(Min, Max)] Called by:");
			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch("Next", typeof(int))]
		static bool Prefix3(int maxValue)
		{
			new Trace("[(int) Random.Next(Max)] Called by:");
			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch("NextBytes", typeof(byte[]))]
		static bool Prefix4(byte[] buffer)
		{
			UnityEngine.Debug.Log("Given byte buffer is " + string.Join(", ", buffer.Select(x => x.ToString()).ToArray()));
			new Trace("[Random.NextBytes] Called by:");
			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch("NextDouble", new Type[] { })]
		static bool Prefix5()
		{
			new Trace("[Random.NextDouble] Called by:");
			return true;
		}

		[HarmonyPatch("Next", new Type[] { })]
		static void Postfix1(ref int __result)
		{
			new Log("[Random.Next()]", __result.ToString());
		}

		[HarmonyPatch("Next", typeof(int), typeof(int))]
		static void Postfix2(ref int __result)
		{
			new Log("[Random.Next()]", __result.ToString());
		}

		[HarmonyPatch("Next", typeof(int))]
		static void Postfix3(ref int __result)
		{
			new Log("[Random.Next()]", __result.ToString());
		}

		[HarmonyPostfix]
		[HarmonyPatch("NextBytes", typeof(byte[]))]
		static void Postfix1(ref byte[] buffer)
		{
			new Log("[Random.NextBytes()]", string.Join(", ", buffer.Select(x => x.ToString()).ToArray()));
		}

		[HarmonyPostfix]
		[HarmonyPatch("NextDouble", new Type[] { })]
		static void Postfix2(ref double __result)
		{
			new Log("[Random.NextDouble()", __result.ToString());
		}
	}

	class Trace
	{
		internal Trace(string name)
		{
			var fullName = new StackTrace().GetFrame(3).GetMethod().DeclaringType.FullName;
			if (Container.CurrentMethod != fullName)
			{
				if (Container.CurrentMethod != "")
					UnityEngine.Debug.LogFormat(string.Join("\n", Container.values.ToArray()));
				Container.values.Clear();
				Container.CurrentMethod = fullName;
				Container.FirstRun = true;
			}
			Container.values.Add(string.Format("{3} {0} {1} ({2})", name, new StackTrace().GetFrame(3).GetMethod().Name, fullName, UnityEngine.Time.frameCount));
		}
	}

	class Log
	{
		internal Log(string name, string _result)
		{
			Container.values.Add(UnityEngine.Time.frameCount + " Recorded value from " + name + " is " + _result);
			if (Container.values.Count == 2 && Container.FirstRun)
			{
				UnityEngine.Debug.Log(string.Join("\n", Container.values.ToArray()));
				Container.values.Clear();
				Container.FirstRun = false;
			}
		}
	}

	static class Container
	{
		public static string CurrentMethod = "";
		public static List<string> values = new List<string>();
		public static bool FirstRun;
	}
}