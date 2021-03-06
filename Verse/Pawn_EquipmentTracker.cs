using System.Collections.Generic;
using RimWorld;

namespace Verse
{
	public class Pawn_EquipmentTracker : IThingHolder, IExposable
	{
		public Pawn pawn;

		private ThingOwner<ThingWithComps> equipment;

		public Thing bondedWeapon;

		public ThingWithComps Primary
		{
			get
			{
				for (int i = 0; i < equipment.Count; i++)
				{
					if (equipment[i].def.equipmentType == EquipmentType.Primary)
					{
						return equipment[i];
					}
				}
				return null;
			}
			private set
			{
				if (Primary == value)
				{
					return;
				}
				if (value != null && value.def.equipmentType != EquipmentType.Primary)
				{
					Log.Error("Tried to set non-primary equipment as primary.");
					return;
				}
				if (Primary != null)
				{
					equipment.Remove(Primary);
				}
				if (value != null)
				{
					equipment.TryAdd(value);
				}
				if (pawn.drafter != null)
				{
					pawn.drafter.Notify_PrimaryWeaponChanged();
				}
			}
		}

		public CompEquippable PrimaryEq
		{
			get
			{
				if (Primary == null)
				{
					return null;
				}
				return Primary.GetComp<CompEquippable>();
			}
		}

		public List<ThingWithComps> AllEquipmentListForReading => equipment.InnerListForReading;

		public IEnumerable<Verb> AllEquipmentVerbs
		{
			get
			{
				List<ThingWithComps> list = AllEquipmentListForReading;
				for (int i = 0; i < list.Count; i++)
				{
					ThingWithComps thingWithComps = list[i];
					List<Verb> verbs = thingWithComps.GetComp<CompEquippable>().AllVerbs;
					for (int j = 0; j < verbs.Count; j++)
					{
						yield return verbs[j];
					}
				}
			}
		}

		public IThingHolder ParentHolder => pawn;

		public Pawn_EquipmentTracker(Pawn newPawn)
		{
			pawn = newPawn;
			equipment = new ThingOwner<ThingWithComps>(this);
		}

		public void ExposeData()
		{
			Scribe_Deep.Look(ref equipment, "equipment", this);
			Scribe_References.Look(ref bondedWeapon, "bondedWeapon");
			if (Scribe.mode != LoadSaveMode.PostLoadInit)
			{
				return;
			}
			List<ThingWithComps> allEquipmentListForReading = AllEquipmentListForReading;
			for (int i = 0; i < allEquipmentListForReading.Count; i++)
			{
				foreach (Verb allVerb in allEquipmentListForReading[i].GetComp<CompEquippable>().AllVerbs)
				{
					allVerb.caster = pawn;
				}
			}
		}

		public void EquipmentTrackerTick()
		{
			List<ThingWithComps> allEquipmentListForReading = AllEquipmentListForReading;
			for (int i = 0; i < allEquipmentListForReading.Count; i++)
			{
				allEquipmentListForReading[i].GetComp<CompEquippable>().verbTracker.VerbsTick();
			}
		}

		public void EquipmentTrackerTickRare()
		{
			equipment.ThingOwnerTickRare();
		}

		public bool HasAnything()
		{
			return equipment.Any;
		}

		public void MakeRoomFor(ThingWithComps eq)
		{
			if (eq.def.equipmentType == EquipmentType.Primary && Primary != null)
			{
				if (TryDropEquipment(Primary, out var resultingEq, pawn.Position))
				{
					resultingEq?.SetForbidden(value: false);
				}
				else
				{
					Log.Error(string.Concat(pawn, " couldn't make room for equipment ", eq));
				}
			}
		}

		public void Remove(ThingWithComps eq)
		{
			equipment.Remove(eq);
		}

		public bool TryDropEquipment(ThingWithComps eq, out ThingWithComps resultingEq, IntVec3 pos, bool forbid = true)
		{
			if (!pos.IsValid)
			{
				Log.Error(string.Concat(pawn, " tried to drop ", eq, " at invalid cell."));
				resultingEq = null;
				return false;
			}
			if (equipment.TryDrop(eq, pos, pawn.MapHeld, ThingPlaceMode.Near, out resultingEq))
			{
				if (resultingEq != null)
				{
					resultingEq.SetForbidden(forbid, warnOnFail: false);
				}
				return true;
			}
			return false;
		}

		public void DropAllEquipment(IntVec3 pos, bool forbid = true)
		{
			for (int num = equipment.Count - 1; num >= 0; num--)
			{
				TryDropEquipment(equipment[num], out var _, pos, forbid);
			}
		}

		public bool TryTransferEquipmentToContainer(ThingWithComps eq, ThingOwner container)
		{
			return equipment.TryTransferToContainer(eq, container);
		}

		public void DestroyEquipment(ThingWithComps eq)
		{
			if (!equipment.Contains(eq))
			{
				Log.Warning(string.Concat("Tried to destroy equipment ", eq, " but it's not here."));
				return;
			}
			Remove(eq);
			eq.Destroy();
		}

		public void DestroyAllEquipment(DestroyMode mode = DestroyMode.Vanish)
		{
			equipment.ClearAndDestroyContents(mode);
		}

		public bool Contains(Thing eq)
		{
			return equipment.Contains(eq);
		}

		internal void Notify_PrimaryDestroyed()
		{
			if (Primary != null)
			{
				Remove(Primary);
			}
			if (pawn.Spawned)
			{
				pawn.stances.CancelBusyStanceSoft();
			}
		}

		public void AddEquipment(ThingWithComps newEq)
		{
			if (newEq.def.equipmentType == EquipmentType.Primary && Primary != null)
			{
				Log.Error(string.Concat("Pawn ", pawn.LabelCap, " got primaryInt equipment ", newEq, " while already having primaryInt equipment ", Primary));
			}
			else
			{
				equipment.TryAdd(newEq);
			}
		}

		public IEnumerable<Gizmo> GetGizmos()
		{
			if (!PawnAttackGizmoUtility.CanShowEquipmentGizmos())
			{
				yield break;
			}
			List<ThingWithComps> list = AllEquipmentListForReading;
			for (int i = 0; i < list.Count; i++)
			{
				ThingWithComps thingWithComps = list[i];
				foreach (Command verbsCommand in thingWithComps.GetComp<CompEquippable>().GetVerbsCommands())
				{
					switch (i)
					{
					case 0:
						verbsCommand.hotKey = KeyBindingDefOf.Misc1;
						break;
					case 1:
						verbsCommand.hotKey = KeyBindingDefOf.Misc2;
						break;
					case 2:
						verbsCommand.hotKey = KeyBindingDefOf.Misc3;
						break;
					}
					yield return verbsCommand;
				}
			}
		}

		public void Notify_EquipmentAdded(ThingWithComps eq)
		{
			foreach (Verb allVerb in eq.GetComp<CompEquippable>().AllVerbs)
			{
				allVerb.caster = pawn;
				allVerb.Notify_PickedUp();
			}
			eq.Notify_Equipped(pawn);
			if (ModsConfig.RoyaltyActive && eq.def.equipmentType == EquipmentType.Primary && bondedWeapon != null && !bondedWeapon.Destroyed)
			{
				bondedWeapon.TryGetComp<CompBladelinkWeapon>()?.Notify_WieldedOtherWeapon();
			}
		}

		public void Notify_EquipmentRemoved(ThingWithComps eq)
		{
			eq.GetComp<CompEquippable>()?.Notify_EquipmentLost();
			if (ModsConfig.RoyaltyActive)
			{
				eq.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(pawn);
			}
		}

		public void Notify_PawnSpawned()
		{
			if (HasAnything() && pawn.Downed && pawn.GetPosture() != PawnPosture.LayingInBed)
			{
				if (pawn.kindDef.destroyGearOnDrop)
				{
					DestroyAllEquipment();
				}
				else
				{
					DropAllEquipment(pawn.Position);
				}
			}
		}

		public void Notify_PawnDied()
		{
			if (ModsConfig.RoyaltyActive && bondedWeapon != null)
			{
				bondedWeapon.TryGetComp<CompBladelinkWeapon>()?.UnBond();
			}
		}

		public void Notify_KilledPawn()
		{
			foreach (ThingWithComps item in equipment)
			{
				item.Notify_KilledPawn(pawn);
			}
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return equipment;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}
	}
}
