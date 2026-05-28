using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
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
				Label label = il.DefineLabel();
				int index = 0, startIndex = 0, endIndex = 0;

				for (int i = 0; i < codes.Count; i++)
				{
					if (codes[i].operand is Type type && type == typeof(WeakGameEntity))
					{
						startIndex = i - 1;
						endIndex = i;
						index = i + 1;
					}
				}

				// Get the closest pickable entity to the agent instead of the last pickable entity.
				codesToInsert.Add(new CodeInstruction(OpCodes.Ldarg_0));
				codesToInsert.Add(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(AgentComponent), "Agent")));
				codesToInsert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PickupMeleeWeaponsComponent), "GetClosestPickableEntity", new Type[] { typeof(WeakGameEntity[]), typeof(Agent) })));
				codes.InsertRange(index, codesToInsert);
				codes.RemoveRange(startIndex, endIndex - startIndex + 1);

				for (int i = 0; i < codes.Count; i++)
				{
					if (codes[i].operand is MethodInfo method && method == AccessTools.Method(typeof(SpawnedItemEntity), "IsQuiverAndNotEmpty"))
					{
						codes[i + 2].labels.Add(label);
						index = i + 1;
					}
				}

				// Make melee weapons pickable.
				codesToInsert.Clear();
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

				for (int i = 0; i < codes.Count; i++)
				{
					if (codes[i].opcode == OpCodes.Blt)
					{
						// Make the for loop run only once.
						codes[i - 1].opcode = OpCodes.Ldc_I4_1;
					}
				}

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

		private static WeakGameEntity GetClosestPickableEntity(WeakGameEntity[] entities, Agent agent)
		{
			if (entities == null) return default(WeakGameEntity);

			WeakGameEntity best = default(WeakGameEntity);
			float bestDistSq = float.MaxValue;

			foreach (WeakGameEntity entity in entities)
			{
				if (!entity.IsValid) continue;
				try
				{
					float distSq = agent.Position.DistanceSquared(entity.GlobalPosition);
					if (distSq < bestDistSq)
					{
						bestDistSq = distSq;
						best = entity;
					}
				}
				catch (Exception) { }
			}

			return best;
		}

		private static bool IsMeleeWeapon(MissionWeapon weapon) => weapon.Item.PrimaryWeapon.IsMeleeWeapon;
	}
}
