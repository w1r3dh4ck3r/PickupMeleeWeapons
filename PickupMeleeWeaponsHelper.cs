using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace PickupMeleeWeapons
{
	public static class PickupMeleeWeaponsHelper
	{
		public static bool HadSameTypeOfMeleeWeaponOnSpawn(Agent agent, WeaponClass weaponClass)
		{
			for (EquipmentIndex index = EquipmentIndex.WeaponItemBeginSlot; index < EquipmentIndex.ExtraWeaponSlot; index++)
			{
				EquipmentElement weapon = agent.SpawnEquipment[index];

				if (!weapon.IsEmpty && weapon.Item.PrimaryWeapon.IsMeleeWeapon && weapon.Item.PrimaryWeapon.WeaponClass == weaponClass)
				{
					return true;
				}
			}

			return false;
		}

		public static bool HasSameTypeOfMeleeWeaponCurrently(Agent agent, ItemObject.ItemTypeEnum itemType)
		{
			for (EquipmentIndex index = EquipmentIndex.WeaponItemBeginSlot; index < EquipmentIndex.ExtraWeaponSlot; index++)
			{
				MissionWeapon weapon = agent.Equipment[index];

				if (!weapon.IsEmpty && weapon.Item.PrimaryWeapon.IsMeleeWeapon && weapon.Item.ItemType == itemType)
				{
					return true;
				}
			}

			return false;
		}

		public static bool HasLostMeleeWeapon(Agent agent)
		{
			int difference = 0;

			for (EquipmentIndex index = EquipmentIndex.WeaponItemBeginSlot; index < EquipmentIndex.ExtraWeaponSlot; index++)
			{
				if (!agent.SpawnEquipment[index].IsEmpty && agent.SpawnEquipment[index].Item.PrimaryWeapon.IsMeleeWeapon)
				{
					difference++;
				}

				if (!agent.Equipment[index].IsEmpty && agent.Equipment[index].Item.PrimaryWeapon.IsMeleeWeapon)
				{
					difference--;
				}
			}

			return difference > 0;
		}
	}
}
