using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Security;
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
		[HarmonyPatch("DisablePickUpForAgentIfNeeded")]
		public static void Postfix(ref bool ____disablePickUpForAgent, Agent ___Agent)
		{
			if (!___Agent.HasMount && PickupMeleeWeaponsHelper.HasLostMeleeWeapon(___Agent))
			{
				____disablePickUpForAgent = false;
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch("ItemPickupTick")]
		private static IEnumerable<CodeInstruction> Transpiler1(IEnumerable<CodeInstruction> instructions)
		{
			List<CodeInstruction> codes = instructions.ToList();
			int startIndex = 0, endIndex = 0;

			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].operand is MethodInfo method)
				{
					if (method == AccessTools.Method(typeof(Agent), "GetTargetAgent"))
					{
						startIndex = i - 2;
					}
					else if (method == AccessTools.Method(typeof(Agent), "GetLastTargetVisibilityState"))
					{
						endIndex = i + 2;
					}
				}
			}

			// Remove the checks for target agent.
			codes.RemoveRange(startIndex, endIndex - startIndex + 1);

			return codes;
		}

		[HarmonyTranspiler]
		[HarmonyPatch("SelectPickableItem")]
		private static IEnumerable<CodeInstruction> Transpiler2(IEnumerable<CodeInstruction> instructions, ILGenerator il)
		{
			List<CodeInstruction> codes = instructions.ToList(), codesToInsert = new List<CodeInstruction>();
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

			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].operand is MethodInfo method)
				{
					if (method == AccessTools.PropertyGetter(typeof(Vec3), "Length"))
					{
						startIndex = i - 3;
					}
					else if (method == AccessTools.Method(typeof(Agent), "GetMaximumForwardUnlimitedSpeed"))
					{
						endIndex = i + 3;
					}
				}
			}

			// Remove the checks for target agent.
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

		[HandleProcessCorruptedStateExceptions]
		[SecurityCritical]
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
