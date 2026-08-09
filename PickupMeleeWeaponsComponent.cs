using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace PickupMeleeWeapons
{
	[HarmonyPatch(typeof(HumanAIComponent))]
	public class PickupMeleeWeaponsComponent
	{
		private static readonly Stopwatch _clock = Stopwatch.StartNew();
		private static readonly Dictionary<int, long> _lastScanMs = new Dictionary<int, long>();
		private const long ScanCooldownMs = 500L;
		private static Mission _lastMission;

		[HarmonyPatch("DisablePickUpForAgentIfNeeded")]
		public static void Postfix(ref bool ____disablePickUpForAgent, Agent ___Agent)
		{
			Mission mission = Mission.Current;
			if (mission != _lastMission)
			{
				_lastScanMs.Clear();
				_lastMission = mission;
			}

			if (!___Agent.HasMount && PickupMeleeWeaponsHelper.HasLostMeleeWeapon(___Agent))
			{
				int idx = ___Agent.Index;
				long now = _clock.ElapsedMilliseconds;
				if (_lastScanMs.TryGetValue(idx, out long last) && now - last < ScanCooldownMs)
					return;
				____disablePickUpForAgent = false;
				_lastScanMs[idx] = now;
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch("ItemPickupTick")]
		private static IEnumerable<CodeInstruction> Transpiler1(IEnumerable<CodeInstruction> instructions)
		{
			var originalInstructions = instructions.ToList();
			try
			{
				List<CodeInstruction> codes = originalInstructions.ToList();
				int startIndex = 0, endIndex = 0;

				for (int i = 0; i < codes.Count; i++)
				{
					if (codes[i].operand is MethodInfo method)
					{
						if (method == AccessTools.Method(typeof(Agent), "GetTargetAgent"))
						{
							startIndex = Math.Max(0, i - 2);
						}
						else if (method == AccessTools.Method(typeof(Agent), "GetLastTargetVisibilityState"))
						{
							endIndex = i + 2;
						}
					}
				}

				// Remove the checks for target agent.
				if (startIndex < endIndex)
					codes.RemoveRange(startIndex, endIndex - startIndex + 1);

				return codes;
			}
			catch (Exception ex)
			{
				System.IO.File.AppendAllText(
					System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PMW_patch_error.txt"),
					"\n[Transpiler1/ItemPickupTick]\n" + ex.ToString() + "\n");
				return originalInstructions;
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch("SelectPickableItem")]
		private static IEnumerable<CodeInstruction> Transpiler2(IEnumerable<CodeInstruction> instructions, ILGenerator il)
		{
			var originalInstructions = instructions.ToList();
			try
			{
				List<CodeInstruction> codes = originalInstructions.ToList(), codesToInsert = new List<CodeInstruction>();
				Label label = il.DefineLabel(), label2 = il.DefineLabel();
				int index = 0, startIndex = 0, endIndex = 0;

				for (int i = 0; i < codes.Count; i++)
				{
					if (codes[i].operand is MethodInfo method && method == AccessTools.Method(typeof(SpawnedItemEntity), "IsQuiverAndNotEmpty"))
					{
						codes[i + 2].labels.Add(label);
						index = i + 1;
					}
				}

				// Make melee weapons pickable.
				codesToInsert.Add(new CodeInstruction(OpCodes.Brtrue_S, label));
				codesToInsert.Add(new CodeInstruction(OpCodes.Ldloca_S, 9));
				codesToInsert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PickupMeleeWeaponsComponent), "IsMeleeWeapon", new Type[] { typeof(MissionWeapon) })));
				codes.InsertRange(index, codesToInsert);

				startIndex = -1; endIndex = -1;
				for (int i = 0; i < codes.Count; i++)
				{
					if (codes[i].operand is MethodInfo method)
					{
						if (method == AccessTools.PropertyGetter(typeof(Vec3), "Length"))
						{
							startIndex = Math.Max(0, i - 3);
						}
						else if (method == AccessTools.Method(typeof(Agent), "GetMaximumForwardUnlimitedSpeed"))
						{
							endIndex = i + 3;
						}
					}
				}

				// Remove the checks for target agent.
				if (startIndex >= 0 && endIndex > startIndex)
					codes.RemoveRange(startIndex, endIndex - startIndex + 1);

				// Get the first pickable entity instead of the last one (upstream 1602e6f crash fix:
				// branch past the scan loop so only the first candidate is taken, dropping the
				// WeakGameEntity/GetClosestPickableEntity dereference that AVE'd on stale pointers).
				// Guard the hardcoded i-8 offset — a mismatched IL layout would insert Br_S at the
				// wrong spot and produce corrupt IL that does NOT throw; bail to original if unmatched.
				bool retFound = false;
				index = -1;
				for (int i = 0; i < codes.Count; i++)
				{
					if (codes[i].opcode == OpCodes.Ret && i >= 8)
					{
						codes[i - 1].labels.Add(label2);
						index = i - 8;
						retFound = true;
					}
				}

				if (!retFound || index < 0 || index > codes.Count)
				{
					System.IO.File.AppendAllText(
						System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PMW_patch_error.txt"),
						"\n[Transpiler2/SelectPickableItem] Ret-scan guard bailed (retFound=" + retFound + ", index=" + index + ", count=" + codes.Count + ") — returning original IL\n");
					return originalInstructions;
				}

				codes.Insert(index, new CodeInstruction(OpCodes.Br_S, label2));

				return codes;
			}
			catch (Exception ex)
			{
				System.IO.File.AppendAllText(
					System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PMW_patch_error.txt"),
					"\n[Transpiler2/SelectPickableItem]\n" + ex.ToString() + "\n");
				return originalInstructions;
			}
		}

		private static bool IsMeleeWeapon(MissionWeapon weapon) => weapon.Item.PrimaryWeapon.IsMeleeWeapon;
	}
}
