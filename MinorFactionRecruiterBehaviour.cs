using Helpers;
using SandBox.GauntletUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
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
using Recruiter;

namespace Recruiter
{
    // Recruiter for Minor Faction Troops
    class MinorFactionRecruiterBehaviour : RecruiterAbstractBehaviour
    {
	    // Minor Factions
	    const String beniZilal = "Beni Zilal";
	    const String skolder = "Skolderbrotva";
	    private const int costPerTransform = 200;
        protected override void RecruiterHourlyAi()
        {
            List<RecruiterProperties> toBeDeleted = new List<RecruiterProperties>();

            foreach (RecruiterProperties prop in recruiterProperties)
            {
                MobileParty recruiter = prop.party;
                bool done = recruiter.PartyTradeGold < costPerTransform;

                if (recruiter.CurrentSettlement == recruiter.HomeSettlement)
                {
	                if (!(recruiter.HomeSettlement.IsCastle || recruiter.HomeSettlement.IsTown))
	                {
		                throw new Exception("Mercenary Recruiter Home Settlement is not a castle nor a town. How could this happen?");
	                }

	                int numRecruited = recruitMercenaries(prop, recruiter, recruiter.CurrentSettlement);
	                int cost = GetTransformCost(prop, numRecruited);
	                GiveGoldAction.ApplyForPartyToSettlement(recruiter.Party, recruiter.CurrentSettlement, cost);

	                if (done)
	                {
		                MobileParty garrison = GetGarrison(recruiter.CurrentSettlement);
		                if (garrison == null)
		                {
			                recruiter.CurrentSettlement.AddGarrisonParty();
			                garrison = recruiter.CurrentSettlement.Parties.First(party => party.IsGarrison);
		                }

		                int soldierCount = recruiter.MemberRoster.TotalManCount;
		                foreach (TroopRosterElement rosterElement in recruiter.MemberRoster)
		                {
			                int healthy = rosterElement.Number - rosterElement.WoundedNumber;
			                garrison.MemberRoster.AddToCounts(rosterElement.Character, healthy, false, rosterElement.WoundedNumber);
		                }
		                InformationManager.DisplayMessage(new InformationMessage("Your mercenary recruiter brought " + soldierCount + " soldiers to " + recruiter.HomeSettlement + ".", new Color(0f, 1f, 0f)));
		                toBeDeleted.Add(prop);
		                recruiter.RemoveParty();
	                }
                }
            }
            
            foreach (RecruiterProperties prop in toBeDeleted)
            {
	            recruiterProperties.Remove(prop);
            }
        }

        private MobileParty GetGarrison(Settlement settlement)
        {
	        foreach (MobileParty party in settlement.Parties)
	        {
		        if (party.IsGarrison)
		        {
			        return party;
		        }
	        }

	        return null;
        }
        private int recruitMercenaries(RecruiterProperties prop, MobileParty recruiter, Settlement currentSettlment)
        {
	        MobileParty garrison = GetGarrison(currentSettlment);
	        int maxPerDay = 5;
	        int numToRecruit = maxPerDay;

	        foreach (TroopRosterElement rosterElement in garrison.MemberRoster)
	        {
		        if (!IsEligible(prop, rosterElement.Character)) continue;
		        int count = rosterElement.Number;
		        if (count > numToRecruit)
		        {
			        garrison.MemberRoster.AddToCounts(rosterElement.Character, -numToRecruit);
			        recruiter.MemberRoster.AddToCounts(GetRecruited(prop), numToRecruit);
			        return maxPerDay;
		        }
		        garrison.MemberRoster.AddToCounts(rosterElement.Character, -count);
		        recruiter.MemberRoster.AddToCounts(GetRecruited(prop), count);
		        numToRecruit -= count;
	        }

	        if (numToRecruit > 0)
	        {
		        DefaultPartyWageModel wageModel = new DefaultPartyWageModel();
		        // Try and recruit from notables
		        foreach (Hero notable in currentSettlment.Notables)
		        {
			        List<CharacterObject> recruitables = HeroHelper.GetVolunteerTroopsOfHeroForRecruitment(notable);
			        for (int recruitableIndex = 0; recruitableIndex < recruitables.Count; recruitableIndex++)
			        {
				        CharacterObject recruitable = recruitables[recruitableIndex];
				        if (recruitable != null && hasSufficientRelationsship(notable, recruitableIndex)
				                                && IsEligible(prop, recruitable)
				                                && numToRecruit > 0)
				        {
					        int recruitCost = wageModel.GetTroopRecruitmentCost(recruitable, Hero.MainHero);

					        if (recruitCost > recruiter.PartyTradeGold)
					        {
						        continue;
					        }

					        recruiter.MemberRoster.AddToCounts(GetRecruited(prop), 1);
					        numToRecruit -= 1;

					        GiveGoldAction.ApplyForPartyToSettlement(recruiter.Party, currentSettlment, recruitCost);
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

	        return maxPerDay - numToRecruit;
        }

        private CharacterObject GetRecruited(RecruiterProperties prop)
        {
	        String minorFaction = prop.MinorFactionName;
	        switch (minorFaction)
	        {
		        case beniZilal:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("beni_zilal_tier_1");
		        case skolder:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("skolderbrotva_tier_1");
	        }
	        throw new Exception("Failed to find the recruit character for " + minorFaction);
        }

        private bool IsEligible(RecruiterProperties prop, CharacterObject rosterElementCharacter)
        {
	        // For now we allow any recruit to be transformed into any Minor Faction recruit
	        return rosterElementCharacter == rosterElementCharacter.Culture.BasicTroop;
        }
        
        private int GetTransformCost(RecruiterProperties prop, int numRecruited)
        {
	        // For now flat
	        return costPerTransform * numRecruited;
        }

        private List<String> getPossibleMinorFactions()
        {
	        List<String> returnList = new List<String>();

	        returnList.Add(beniZilal);
	        returnList.Add(skolder);
	        
	        return returnList;
        }
        
        public override void AddRecruiterMenu(CampaignGameStarter obj)
        {

			String menuName = "recruiter_minor_faction_menu";
			String buyName = "recruiter_minor_faction_buy_recruiter";
			String payName = "recuirter_minor_faction_pay_menu";

			GameMenuOption.OnConditionDelegate hireRecruiterDelegate = delegate (MenuCallbackArgs args)
			{
				args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
				return Settlement.CurrentSettlement.OwnerClan == Clan.PlayerClan;
			};
			GameMenuOption.OnConsequenceDelegate hireRecruiterConsequenceDelegate = delegate (MenuCallbackArgs args)
			{
				GameMenu.SwitchToMenu(menuName);
			};

			obj.AddGameMenu(menuName, "The Chamberlain asks you from what faction your recruits should be.", null, GameOverlays.MenuOverlayType.None, GameMenu.MenuFlags.none, null);

			RecruiterProperties props = new RecruiterProperties();
			foreach (String minorFactionName in getPossibleMinorFactions())
			{
				obj.AddGameMenuOption(menuName, "recruiter_" + minorFactionName, minorFactionName,
					delegate (MenuCallbackArgs args)
				{
					return true;
				},
				delegate (MenuCallbackArgs args)
				{
					props = new RecruiterProperties();
					props.MinorFactionName = minorFactionName;
					GameMenu.SwitchToMenu(buyName);
				});

			}

			obj.AddGameMenuOption("town_keep", buyName, "Hire a Mercenary Recruiter", hireRecruiterDelegate, hireRecruiterConsequenceDelegate, false, 4, false);
			obj.AddGameMenuOption("castle", buyName, "Hire a Mercenary Recruiter", hireRecruiterDelegate, hireRecruiterConsequenceDelegate, false, 4, false);

			obj.AddGameMenu(payName, "The Chamberlain asks you for how many denars he should buy recruits.", null, GameOverlays.MenuOverlayType.None, GameMenu.MenuFlags.none, null);
			
			obj.AddGameMenuOption(payName, "minor_recruiter_pay_small", "Pay 500.", delegate (MenuCallbackArgs args)
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
			obj.AddGameMenuOption(payName, "minor_recruiter_pay_medium", "Pay 2500.", delegate (MenuCallbackArgs args)
			{
				args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
				string stringId = Settlement.CurrentSettlement.StringId;
				int cost = 2500;
				bool flag = cost >= Hero.MainHero.Gold;
				return !flag;
			}, delegate (MenuCallbackArgs args)
			{
				string stringId = Settlement.CurrentSettlement.StringId;
				int cost = 2500;
				bool flag = cost <= Hero.MainHero.Gold;
				if (flag)
				{
					GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, Settlement.CurrentSettlement, cost, false);
					MobileParty item = this.spawnRecruiter(Settlement.CurrentSettlement, cost, props);
				}
				GameMenu.SwitchToMenu("castle");
			}, false, -1, false);
			obj.AddGameMenuOption(payName, "minor_recruiter_pay_large", "Pay 5000.", delegate (MenuCallbackArgs args)
			{
				args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
				string stringId = Settlement.CurrentSettlement.StringId;
				int cost = 5000;
				bool flag = cost >= Hero.MainHero.Gold;
				return !flag;
			}, delegate (MenuCallbackArgs args)
			{
				string stringId = Settlement.CurrentSettlement.StringId;
				int cost = 5000;
				bool flag = cost <= Hero.MainHero.Gold;
				if (flag)
				{
					GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, Settlement.CurrentSettlement, cost, false);
					MobileParty item = this.spawnRecruiter(Settlement.CurrentSettlement, cost, props);
				}
				GameMenu.SwitchToMenu("castle");
			}, false, -1, false);
			obj.AddGameMenuOption(payName, "minor_recruiter_leave", "Leave", new GameMenuOption.OnConditionDelegate(this.game_menu_just_add_leave_conditional), new GameMenuOption.OnConsequenceDelegate(this.game_menu_switch_to_village_menu), false, -1, false);
		
        }

        public override MobileParty spawnRecruiter(Settlement settlement, int cash, RecruiterProperties props)
        {
	        PartyTemplateObject defaultPartyTemplate = settlement.Culture.DefaultPartyTemplate;
	        int numberOfCreated = defaultPartyTemplate.NumberOfCreated;
	        defaultPartyTemplate.IncrementNumberOfCreated();
	        MobileParty mobileParty = MBObjectManager.Instance.CreateObject<MobileParty>(settlement.OwnerClan.StringId + "_m_" + numberOfCreated);
	        TextObject textObject = new TextObject("{RECRUITER_SETTLEMENT_NAME} Mercenary Recruiter", null);
	        textObject.SetTextVariable("RECRUITER_SETTLEMENT_NAME", settlement.Name);
	        mobileParty.InitializeMobileParty(textObject, defaultPartyTemplate, settlement.GatePosition, 0f, 0f, MobileParty.PartyTypeEnum.Default, 1);
	        mobileParty.PartyTradeGold = cash;
	        this.InitRecruiterParty(mobileParty, textObject, settlement.OwnerClan, settlement);
	        mobileParty.Aggressiveness = 0f;
	        props.party = mobileParty;
	        recruiterProperties.Add(props);
	        // Instead of wandering around will stay home and convert / recruit mercenaries
	        mobileParty.SetMoveGoToSettlement(settlement);
	        return mobileParty;
        }
        
        public class BannerlordMinorFactionRecruiterSaveDefiner : SaveableTypeDefiner
        {
            // Token: 0x06000043 RID: 67 RVA: 0x000034F1 File Offset: 0x000016F1
            public BannerlordMinorFactionRecruiterSaveDefiner() : base(91215130)
            {
            }

            // Token: 0x06000044 RID: 68 RVA: 0x00003500 File Offset: 0x00001700
            protected override void DefineClassTypes()
            {
                base.AddClassDefinition(typeof(RecruiterProperties), 1);
            }

            // Token: 0x06000045 RID: 69 RVA: 0x00003515 File Offset: 0x00001715
            protected override void DefineContainerDefinitions()
            {
                base.ConstructContainerDefinition(typeof(List<RecruiterProperties>));
            }
        }
    }
    
    
}