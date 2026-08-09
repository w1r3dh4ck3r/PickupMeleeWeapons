using System;
using System.Linq;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace PickupMeleeWeapons
{
	// This mod makes troops pick up dropped melee weapons.
	public class PickupMeleeWeaponsSubModule : MBSubModuleBase
	{
		private Harmony _harmony;
		private Type _typeofStanceLogic;

		protected override void OnSubModuleLoad()
		{
			_harmony = new Harmony("mod.bannerlord.pickupmeleeweapons");
			var log = new System.Text.StringBuilder();
			PatchSafe(log, typeof(HumanAIComponent), "DisablePickUpForAgentIfNeeded",
				postfix: new HarmonyMethod(AccessTools.Method(typeof(PickupMeleeWeaponsComponent), "Postfix")));
			PatchSafe(log, typeof(HumanAIComponent), "ItemPickupTick",
				transpiler: new HarmonyMethod(AccessTools.Method(typeof(PickupMeleeWeaponsComponent), "Transpiler1")));
			PatchSafe(log, typeof(HumanAIComponent), "SelectPickableItem",
				transpiler: new HarmonyMethod(AccessTools.Method(typeof(PickupMeleeWeaponsComponent), "Transpiler2")));
			if (log.Length > 0)
				System.IO.File.WriteAllText(
					System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PMW_patch_error.txt"),
					log.ToString());
		}

		private void PatchSafe(System.Text.StringBuilder log, Type type, string methodName,
			HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null)
		{
			try
			{
				var target = AccessTools.Method(type, methodName);
				if (target == null) { log.AppendLine($"[PMW] NULL target: {type.Name}.{methodName}"); return; }
				_harmony.Patch(target, prefix: prefix, postfix: postfix, transpiler: transpiler);
				log.AppendLine($"[PMW] OK: {type.Name}.{methodName}");
			}
			catch (Exception ex)
			{
				log.AppendLine($"[PMW] FAIL: {type.Name}.{methodName}\n{ex}");
			}
		}

		protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
		{
			var log = new System.Text.StringBuilder();

			try
			{
				gameStarterObject.AddModel(new PickupMeleeWeaponsModel((ItemPickupModel)gameStarterObject.Models.Last(model => model is ItemPickupModel)));
				log.AppendLine("[PMW] OK: AddModel(PickupMeleeWeaponsModel)");
			}
			catch (Exception ex)
			{
				log.AppendLine("[PMW] FAIL: AddModel(PickupMeleeWeaponsModel)\n" + ex);
			}

			_typeofStanceLogic = AccessTools.TypeByName("RBMAI.StanceLogic");

			// Check whether RBM is loaded.
			if (_typeofStanceLogic != null)
			{
				try
				{
					_harmony.Patch(AccessTools.Method(_typeofStanceLogic, "forceTiredAnimation"), prefix: new HarmonyMethod(AccessTools.Method(typeof(PickupMeleeWeaponsStanceLogic), "Prefix")));
					log.AppendLine("[PMW] OK: RBM StanceLogic.forceTiredAnimation");
				}
				catch (Exception ex)
				{
					log.AppendLine("[PMW] FAIL: RBM StanceLogic.forceTiredAnimation\n" + ex);
				}

				try
				{
					_harmony.Patch(AccessTools.Method(AccessTools.Inner(_typeofStanceLogic, "CreateMeleeBlowPatch"), "TryToDropWeapon"), transpiler: new HarmonyMethod(AccessTools.Method(typeof(PickupMeleeWeaponsStanceLogic), "Transpiler")));
					log.AppendLine("[PMW] OK: RBM CreateMeleeBlowPatch.TryToDropWeapon");
				}
				catch (Exception ex)
				{
					log.AppendLine("[PMW] FAIL: RBM CreateMeleeBlowPatch.TryToDropWeapon\n" + ex);
				}
			}
			else
			{
				log.AppendLine("[PMW] SKIP: RBM not loaded (RBMAI.StanceLogic type not found)");
			}

			if (log.Length > 0)
				System.IO.File.AppendAllText(
					System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PMW_patch_error.txt"),
					log.ToString());
		}

		public override void OnGameEnd(Game game)
		{
			if (_typeofStanceLogic != null)
			{
				_harmony.Unpatch(AccessTools.Method(_typeofStanceLogic, "forceTiredAnimation"), AccessTools.Method(typeof(PickupMeleeWeaponsStanceLogic), "Prefix"));
				_harmony.Unpatch(AccessTools.Method(AccessTools.Inner(_typeofStanceLogic, "CreateMeleeBlowPatch"), "TryToDropWeapon"), AccessTools.Method(typeof(PickupMeleeWeaponsStanceLogic), "Transpiler"));
			}
		}
	}
}
