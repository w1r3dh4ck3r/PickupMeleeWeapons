using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace PickupMeleeWeapons
{
	public class PickupMeleeWeaponsModel : ItemPickupModel
	{
		private readonly ItemPickupModel _model;

		public PickupMeleeWeaponsModel(ItemPickupModel model) => _model = model;

		public override float GetItemScoreForAgent(SpawnedItemEntity item, Agent agent)
		{
			if (PickupMeleeWeaponsHelper.HadSameTypeOfMeleeWeaponOnSpawn(agent, item.WeaponCopy.Item.PrimaryWeapon.WeaponClass))
			{
				// Make agents prefer the same types of weapons they spawned with when picking up melee weapons.
				return 110f;
			}
			else if (item.WeaponCopy.Item.PrimaryWeapon.IsMeleeWeapon)
			{
				return 100f;
			}

			return _model.GetItemScoreForAgent(item, agent);
		}

		public override bool IsAgentEquipmentSuitableForPickUpAvailability(Agent agent)
		{
			if (PickupMeleeWeaponsHelper.HasLostMeleeWeapon(agent))
			{
				return true;
			}

			return _model.IsAgentEquipmentSuitableForPickUpAvailability(agent);
		}

		public override bool IsItemAvailableForAgent(SpawnedItemEntity item, Agent agent, EquipmentIndex slotToPickUp)
		{
			if (item.WeaponCopy.Item.PrimaryWeapon.IsMeleeWeapon)
			{
				// Ensure that agents do not pick up another melee weapon that shares the same type as a weapon they already have.
				return agent.Equipment[slotToPickUp].IsEmpty && PickupMeleeWeaponsHelper.HasLostMeleeWeapon(agent) && !PickupMeleeWeaponsHelper.HasSameTypeOfMeleeWeaponCurrently(agent, item.WeaponCopy.Item.ItemType);
			}

			return _model.IsItemAvailableForAgent(item, agent, slotToPickUp);
		}
	}
}
