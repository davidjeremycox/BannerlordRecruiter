using Helpers;
using SandBox.GauntletUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Overlay;
using TaleWorlds.CampaignSystem.SandBox.GameComponents.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu;
using TaleWorlds.Core;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace Recruiter
{
	// Original recruiter
    class RecruiterBehaviour : RecruiterAbstractBehaviour
    {
	    public override void OnDailyAITick()
		{
			foreach (RecruiterProperties prop in recruiterProperties)
			{
				if (prop.party.Food <= 3f)
				{
						this.generateFood(prop.party);
				}
			}
		}
	    
	    public override void AddPatrolDialog(CampaignGameStarter obj)
	    {
		    obj.AddDialogLine("mod_recruiter_talk_start", "start", "mod_recruiter_talk", "Hello my lord. What do you need us to do?", new ConversationSentence.OnConditionDelegate(this.patrol_talk_start_on_conditional), null, 100, null);
		    obj.AddPlayerLine("mod_recruiter_donate_troops", "mod_recruiter_talk", "mod_recruiter_after_donate", "Donate Troops", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_patrol_donate_troops_on_consequence), 100, null, null);
		    obj.AddPlayerLine("mod_recruiter_disband", "mod_recruiter_talk", "close_window", "Disband.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_patrol_disband_on_consequence), 100, null, null);
		    obj.AddPlayerLine("mod_recruiter_leave", "mod_recruiter_talk", "close_window", "Carry on, then. Farewell.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_patrol_leave_on_consequence), 100, null, null);
		    obj.AddDialogLine("mod_recruiter_after_donate", "mod_recruiter_after_donate", "mod_recruiter_talk", "Anything else?", null, null, 100, null);
		    //obj.AddPlayerLine("mod_leaderless_party_answer", "disbanding_leaderless_party_start_response", "close_window", "Disband now.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_patrol_disband_now_on_consquence), 100, null, null);
	    }
	    protected override bool patrol_talk_start_on_conditional()
	    {
		    PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
		    bool result;
		    try
		    {
			    bool flag = PlayerEncounter.Current != null && Campaign.Current.CurrentConversationContext == ConversationContext.PartyEncounter && encounteredParty.IsMobile && encounteredParty.Name.Contains("Recruiter") && !encounteredParty.Name.Contains("Mercenary Recruiter") && encounteredParty.IsActive && encounteredParty.MobileParty.HomeSettlement.OwnerClan == Clan.PlayerClan;
			    if (flag)
			    {
				    result = true;
			    }
			    else
			    {
				    result = false;
			    }
		    }
		    catch (Exception e)
		    {
			    MessageBox.Show(e.ToString());
			    result = false;
		    }
		    return result;
	    }
	    protected override void RecruiterHourlyAi()
		{
			List<RecruiterProperties> toBeDeleted = new List<RecruiterProperties>();

			foreach (RecruiterProperties prop in recruiterProperties)
			{
				if (prop.MinorFactionName != null)
				{
					//This means it's a Minor Faction recruiter
					continue;
				}
				MobileParty recruiter = prop.party;
				if (recruiter.HomeSettlement == null)
				{
					toBeDeleted.Add(prop);
					continue;
				}
				if (recruiter.PartyTradeGold < 20)
				{
					recruiter.SetMoveGoToSettlement(recruiter.HomeSettlement);
				}

				if (recruiter.CurrentSettlement == recruiter.HomeSettlement)
				{
					if (!(recruiter.HomeSettlement.IsCastle || recruiter.HomeSettlement.IsTown))
					{
						throw new Exception("Recruiter Home Settelment is not a castle nor a town. How could this happen?");
					}

					recruitAll(recruiter, recruiter.CurrentSettlement);
					Settlement nearestWithRecruits = findNearestSettlementWithRecruitableRecruits(recruiter);
					if (nearestWithRecruits != null)
					{
						recruiter.SetMoveGoToSettlement(nearestWithRecruits);
					}

					else
					{
						bool hasGarisson = false;
						foreach (MobileParty party in recruiter.HomeSettlement.Parties)
						{
							if (party.IsGarrison)
							{
								hasGarisson = true;
								break;
							}
						}
						if (!hasGarisson)
							recruiter.HomeSettlement.AddGarrisonParty();
						MobileParty garrision = recruiter.HomeSettlement.Parties.First(party => party.IsGarrison);

						int soldierCount = recruiter.MemberRoster.TotalManCount;
						foreach (TroopRosterElement rosterElement in recruiter.MemberRoster)
						{
							int healthy = rosterElement.Number - rosterElement.WoundedNumber;
							garrision.MemberRoster.AddToCounts(rosterElement.Character, healthy, false, rosterElement.WoundedNumber);
						}
						InformationManager.DisplayMessage(new InformationMessage("Your recruiter brought " + soldierCount + " soldiers to " + recruiter.HomeSettlement + ".", new Color(0f, 1f, 0f)));
						toBeDeleted.Add(prop);
						recruiter.RemoveParty();
						continue;
					}

				}
				else if (recruiter.CurrentSettlement != null)
				{
					recruitAll(recruiter, recruiter.CurrentSettlement);
					Settlement temp = findNearestSettlementWithRecruitableRecruits(recruiter);
					if (temp != null)
						recruiter.SetMoveGoToSettlement(findNearestSettlementWithRecruitableRecruits(recruiter));
				}
				else
				{
					Settlement closestWithRecruits = findNearestSettlementWithRecruitableRecruits(recruiter);
					if (closestWithRecruits == null)
					{
						recruiter.SetMoveGoToSettlement(recruiter.HomeSettlement);
						continue;
					}
					recruiter.SetMoveGoToSettlement(closestWithRecruits);
				}
			}

			foreach (RecruiterProperties prop in toBeDeleted)
			{
				recruiterProperties.Remove(prop);
			}

		}

		private bool hasSettlementRecruits(Settlement settlement, MobileParty recruiter)
		{
			DefaultPartyWageModel wageModel = new DefaultPartyWageModel();
			if (settlement.IsRaided)
			{
				return false;
			}
			foreach (Hero notable in settlement.Notables)
			{
				for (int i = 0; i < notable.VolunteerTypes.Length; i++)
				{
					CharacterObject character = notable.VolunteerTypes[i];
					if (character == null)
					{
						continue;
					}

					if (wageModel.GetTroopRecruitmentCost(character, Hero.MainHero) > recruiter.PartyTradeGold || !hasSufficientRelationsship(notable, i))
					{
						continue;
					}

					return true;
				}
			}
			return false;
		}

		private Settlement findNearestSettlementWithRecruitableRecruits(MobileParty recruiter)
		{
			RecruiterProperties props = recruiterProperties.First(prop => prop.party == recruiter);
			CultureObject onlyCulture = props.SearchCulture;
			IEnumerable<Settlement> settlementsWithRecruits;
			if (onlyCulture != null)
			{
				settlementsWithRecruits = Settlement.All.
					Where(settlement => settlement.GetNumberOfAvailableRecruits() > 0 && 
					!settlement.OwnerClan.IsAtWarWith(Hero.MainHero.Clan) && 
					settlement.Culture.Name.ToString().Equals(onlyCulture.Name.ToString()));
			}
			else
			{
				settlementsWithRecruits = Settlement.All.
					Where(settlement => settlement.GetNumberOfAvailableRecruits() > 0 
					&& !settlement.OwnerClan.IsAtWarWith(Hero.MainHero.Clan));
			}

			DefaultPartyWageModel wageModel = new DefaultPartyWageModel();

			Settlement nearest = null;
			float shortestDistance = float.MaxValue; 
			
			foreach (Settlement settlement in settlementsWithRecruits)
			{

				if(!hasSettlementRecruits(settlement, recruiter))
				{
					continue;
				}

				Vec2 position = settlement.GatePosition;
				float distance = position.Distance(recruiter.GetPosition2D);
				if (distance < shortestDistance)
				{
					shortestDistance = distance;
					nearest = settlement;
				}
			}

			return nearest;
		}

		private void recruitAll(MobileParty recruiter, Settlement settlement)
		{
			DefaultPartyWageModel wageModel = new DefaultPartyWageModel();
			foreach (Hero notable in settlement.Notables)
			{
				List<CharacterObject> recruitables = HeroHelper.GetVolunteerTroopsOfHeroForRecruitment(notable);
				for (int recruitableIndex = 0; recruitableIndex < recruitables.Count; recruitableIndex++)
				{
					CharacterObject recruitable = recruitables[recruitableIndex];
					if (recruitable != null && hasSufficientRelationsship(notable, recruitableIndex))
					{

						int recruitCost = wageModel.GetTroopRecruitmentCost(recruitable, Hero.MainHero);

						if(recruitCost > recruiter.PartyTradeGold)
						{
							continue;
						}

						recruiter.MemberRoster.AddToCounts(recruitable, 1);
						GiveGoldAction.ApplyForPartyToSettlement(recruiter.Party, settlement, recruitCost);
						for (int i = 0; i < notable.VolunteerTypes.Length; i++)
						{
							if (recruitable == notable.VolunteerTypes[i])
							{
								notable.VolunteerTypes[i] = null;
								break;
							}
						}
					}
				} 
			}
		}
		
		public override void SyncData(IDataStore dataStore)
		{
			//List<MobileParty> allRecruitersLegacy = new List<MobileParty>();
			//dataStore.SyncData<List<MobileParty>>("allRecruiters", ref allRecruitersLegacy);
			//dataStore.SyncData<Dictionary<MobileParty, RecruiterProperties>>("allRecruitersToProperties", ref allRecruitersToProperties);

			//foreach (MobileParty recruiter in allRecruitersLegacy)
			//{
			//	if(!allRecruitersToProperties.ContainsKey(recruiter))
			//	{
			//		allRecruitersToProperties.Add(recruiter, new RecruiterProperties());
			//	}
			//}
			dataStore.SyncData<List<RecruiterProperties>>("recruiterProperties", ref recruiterProperties);
			if(recruiterProperties == null)
			{
				recruiterProperties = new List<RecruiterProperties>();
			}
		}

		public List<CultureObject> getPossibleCultures()
		{
			IEnumerable<Settlement> settlements = Settlement.All;

			List<CultureObject> returnList = new List<CultureObject>();
			foreach (Settlement settlement in settlements)
			{
				if(!returnList.Contains(settlement.Culture))
				{
					returnList.Add(settlement.Culture);
				}
			}
			return returnList;
		}

		public override void AddRecruiterMenu(CampaignGameStarter obj)
		{
			GameMenuOption.OnConditionDelegate hireRecruiterDelegate = delegate (MenuCallbackArgs args)
			{
				args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
				return Settlement.CurrentSettlement.OwnerClan == Clan.PlayerClan;
			};
			GameMenuOption.OnConsequenceDelegate hireRecruiterConsequenceDelegate = delegate (MenuCallbackArgs args)
			{
				GameMenu.SwitchToMenu("recruiter_culture_menu");
			};

			obj.AddGameMenu("recruiter_culture_menu", "The Chamberlain asks you what culture your recruits should be.", null, GameOverlays.MenuOverlayType.None, GameMenu.MenuFlags.none, null);

			RecruiterProperties props = new RecruiterProperties();
			foreach (CultureObject culture in getPossibleCultures())
			{
				obj.AddGameMenuOption("recruiter_culture_menu", "recruiter_" + culture.GetName().ToString(), culture.GetName().ToString(),
					delegate (MenuCallbackArgs args)
				{
					return Settlement.All.Count(settlement => settlement.Culture == culture && 
					settlement.OwnerClan != null && 
					!((settlement.OwnerClan.Kingdom != null && settlement.OwnerClan.Kingdom.IsAtWarWith(Hero.MainHero.Clan)) || 
					settlement.OwnerClan.IsAtWarWith(Hero.MainHero.Clan) ||
					(settlement.OwnerClan.Kingdom != null && Hero.MainHero.Clan.Kingdom != null && settlement.OwnerClan.Kingdom.IsAtWarWith(Hero.MainHero.Clan.Kingdom))
					)
					) > 0;
				},
				delegate (MenuCallbackArgs args)
				{
					props = new RecruiterProperties();
					props.SearchCulture = culture;
					GameMenu.SwitchToMenu("recruiter_pay_menu");
				});

			}

			obj.AddGameMenuOption("town_keep", "recruiter_buy_recruiter", "Hire a Recruiter", hireRecruiterDelegate, hireRecruiterConsequenceDelegate, false, 4, false);
			obj.AddGameMenuOption("castle", "recruiter_buy_recruiter", "Hire a Recruiter", hireRecruiterDelegate, hireRecruiterConsequenceDelegate, false, 4, false);

			obj.AddGameMenu("recruiter_pay_menu", "The Chamberlain asks you for how many denars he should buy recruits.", null, GameOverlays.MenuOverlayType.None, GameMenu.MenuFlags.none, null);
			
			obj.AddGameMenuOption("recruiter_pay_menu", "recruiter_pay_small", "Pay 500.", delegate (MenuCallbackArgs args)
			{
				args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
				string stringId = Settlement.CurrentSettlement.StringId;
				int cost = 500;
				bool flag = cost >= Hero.MainHero.Gold;
				return !flag;
			}, delegate (MenuCallbackArgs args)
			{
				string stringId = Settlement.CurrentSettlement.StringId;
				int cost = 500;
				bool flag = cost <= Hero.MainHero.Gold;
				if (flag)
				{
					GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, Settlement.CurrentSettlement, cost, false);
					MobileParty item = this.spawnRecruiter(Settlement.CurrentSettlement, cost, props);
				}
				GameMenu.SwitchToMenu("castle");
			}, false, -1, false);
			obj.AddGameMenuOption("recruiter_pay_menu", "recruiter_pay_medium", "Pay 1500.", delegate (MenuCallbackArgs args)
			{
				args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
				string stringId = Settlement.CurrentSettlement.StringId;
				int cost = 1500;
				bool flag = cost >= Hero.MainHero.Gold;
				return !flag;
			}, delegate (MenuCallbackArgs args)
			{
				string stringId = Settlement.CurrentSettlement.StringId;
				int cost = 1500;
				bool flag = cost <= Hero.MainHero.Gold;
				if (flag)
				{
					GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, Settlement.CurrentSettlement, cost, false);
					MobileParty item = this.spawnRecruiter(Settlement.CurrentSettlement, cost, props);
				}
				GameMenu.SwitchToMenu("castle");
			}, false, -1, false);
			obj.AddGameMenuOption("recruiter_pay_menu", "recruiter_pay_large", "Pay 3000.", delegate (MenuCallbackArgs args)
			{
				args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
				string stringId = Settlement.CurrentSettlement.StringId;
				int cost = 3000;
				bool flag = cost >= Hero.MainHero.Gold;
				return !flag;
			}, delegate (MenuCallbackArgs args)
			{
				string stringId = Settlement.CurrentSettlement.StringId;
				int cost = 3000;
				bool flag = cost <= Hero.MainHero.Gold;
				if (flag)
				{
					GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, Settlement.CurrentSettlement, cost, false);
					MobileParty item = this.spawnRecruiter(Settlement.CurrentSettlement, cost, props);
				}
				GameMenu.SwitchToMenu("castle");
			}, false, -1, false);
			obj.AddGameMenuOption("recruiter_pay_menu", "recruiter_leave", "Leave", new GameMenuOption.OnConditionDelegate(this.game_menu_just_add_leave_conditional), new GameMenuOption.OnConsequenceDelegate(this.game_menu_switch_to_village_menu), false, -1, false);
		}

		public override MobileParty spawnRecruiter(Settlement settlement, int cash, RecruiterProperties props)
		{
			PartyTemplateObject defaultPartyTemplate = settlement.Culture.DefaultPartyTemplate;
			int numberOfCreated = defaultPartyTemplate.NumberOfCreated;
			defaultPartyTemplate.IncrementNumberOfCreated();
			MobileParty mobileParty = MBObjectManager.Instance.CreateObject<MobileParty>(settlement.OwnerClan.StringId + "_" + numberOfCreated);
			TextObject textObject = new TextObject("{RECRUITER_SETTLEMENT_NAME} Recruiter", null);
			textObject.SetTextVariable("RECRUITER_SETTLEMENT_NAME", settlement.Name);
			mobileParty.InitializeMobileParty(textObject, defaultPartyTemplate, settlement.GatePosition, 0f, 0f, MobileParty.PartyTypeEnum.Default, 1);
			mobileParty.PartyTradeGold = cash;
			this.InitRecruiterParty(mobileParty, textObject, settlement.OwnerClan, settlement);
			mobileParty.Aggressiveness = 0f;
			props.party = mobileParty;
			recruiterProperties.Add(props);
			Settlement best = findNearestSettlementWithRecruitableRecruits(mobileParty);
			if(best != null)
				mobileParty.SetMoveGoToSettlement(best);
			return mobileParty;
		}
		
		public class BannerlordRecruiterSaveDefiner : SaveableTypeDefiner
		{
			public BannerlordRecruiterSaveDefiner() : base(91215129)
			{
			}
			protected override void DefineClassTypes()
			{
				base.AddClassDefinition(typeof(RecruiterProperties), 1);
			}

			protected override void DefineContainerDefinitions()
			{
				base.ConstructContainerDefinition(typeof(List<RecruiterProperties>));
			}
		}
	}
}
