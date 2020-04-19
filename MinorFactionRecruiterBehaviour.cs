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
	    private const String ghilman = "Ghilman";
	    // In the files but haven't seen them
	    //private const String galloglass = "";
	    private const String legionOfTheBetrayed = "Legion of the Betrayed";
	    private const String skolder = "Skolderbrotva";
	    private const String boar = "Company of the Boar";
	    private const String beniZilal = "Beni Zilal";
	    private const String wolfskins = "Wolfskins";
	    private const String hiddenHand = "Hidden Hand";
	    private const String lakeRat = "Lake Rats";
	    private const String brotherhoodOfWoods = "Brotherhood of the Woods";
	    // In the files but haven't seen them
	    //private const String guardians = "";
	    private const String embersOfFlame = "Embers of the Flame";
	    private const String jawwal = "Jawwal";
	    private const String karakhuzaits = "Karakhuzait";
	    private const String forestPeople = "Forest People";
	    private const String eleftheroi = "Eleftheroi";
	    // Special Nobles -- Custom Units
	    private const String desertNobles = "Desert Tribes";

	    // Variables
	    private const int costPerTransform = 200;
	    private const int nobleCostPerTransform = 300;
	    private const int maxPerDay = 5;
        protected override void RecruiterHourlyAi()
        {
	        return;
        }
        public override void AddPatrolDialog(CampaignGameStarter obj)
        {
	        obj.AddDialogLine("mod_merc_recruiter_talk_start", "start", "mod_merc_recruiter_talk", "Hello my lord. What do you need us to do?", new ConversationSentence.OnConditionDelegate(this.patrol_talk_start_on_conditional), null, 100, null);
	        obj.AddPlayerLine("mod_merc_recruiter_donate_troops", "mod_merc_recruiter_talk", "mod_merc_recruiter_after_donate", "Donate Troops", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_patrol_donate_troops_on_consequence), 100, null, null);
	        obj.AddPlayerLine("mod_merc_recruiter_disband", "mod_merc_recruiter_talk", "close_window", "Disband.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_patrol_disband_on_consequence), 100, null, null);
	        obj.AddPlayerLine("mod_merc_recruiter_leave", "mod_merc_recruiter_talk", "close_window", "Carry on, then. Farewell.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_patrol_leave_on_consequence), 100, null, null);
	        obj.AddDialogLine("mod_merc_recruiter_after_donate", "mod_merc_recruiter_after_donate", "mod_merc_recruiter_talk", "Anything else?", null, null, 100, null);
	        //obj.AddPlayerLine("mod_leaderless_party_answer", "disbanding_leaderless_party_start_response", "close_window", "Disband now.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_patrol_disband_now_on_consquence), 100, null, null);
        }
        protected override bool patrol_talk_start_on_conditional()
        {
	        PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
	        bool result;
	        try
	        {
		        bool flag = PlayerEncounter.Current != null && Campaign.Current.CurrentConversationContext == ConversationContext.PartyEncounter && encounteredParty.IsMobile && encounteredParty.Name.Contains("Mercenary Recruiter") && encounteredParty.IsActive && encounteredParty.MobileParty.HomeSettlement.OwnerClan == Clan.PlayerClan;
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
        public override void OnDailyAITick()
        {
	        foreach (RecruiterProperties prop in recruiterProperties)
	        {
		        if (prop.party.Food <= 3f)
		        {
			        this.generateFood(prop.party);
		        }
	        }

	        List<RecruiterProperties> toBeDeleted = new List<RecruiterProperties>();

            foreach (RecruiterProperties prop in recruiterProperties)
            {
	            if (prop.SearchCulture != null)
	            {
		            // This means it's a normal Recruiter
		            continue;
	            }
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
		                InformationManager.DisplayMessage(new InformationMessage("Your mercenary recruiter brought " + (soldierCount - 1) + " soldiers to " + recruiter.HomeSettlement + ".", new Color(0f, 1f, 0f)));
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
		        case ghilman:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("ghilman_tier_1");
		        case legionOfTheBetrayed:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("legion_of_the_betrayed_tier_1");
		        case skolder:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("skolderbrotva_tier_1");
		        case boar:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("company_of_the_boar_tier_1");
		        case beniZilal:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("beni_zilal_tier_1");
		        case wolfskins:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("wolfskins_tier_1");
		        case hiddenHand:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("hidden_hand_tier_1");
		        case lakeRat:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("lakepike_tier_1");
		        case brotherhoodOfWoods:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("brotherhood_of_woods_tier_1");
		        case embersOfFlame:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("embers_of_flame_tier_1");
		        case jawwal:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("jawwal_tier_1");
		        case karakhuzaits:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("karakhuzaits_tier_1");
		        case forestPeople:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("forest_people_tier_1");
		        case eleftheroi:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("eleftheroi_tier_1");
		        case desertNobles:
			        return MBObjectManager.Instance.GetObject<CharacterObject>("desert_noble_tier_0");

	        }
	        throw new Exception("Failed to find the recruit character for " + minorFaction);
        }

        private bool isNoble(String minorFactionName)
        {
	        return minorFactionName == desertNobles;
        }
        private bool IsEligible(RecruiterProperties prop, CharacterObject rosterElementCharacter)
        {
	        if (isNoble(prop.MinorFactionName))
	        {
		        // Nobles have a noble tier troop.
		        return rosterElementCharacter == rosterElementCharacter.Culture.EliteBasicTroop;
	        }
	        // For now we allow any recruit to be transformed into any Minor Faction recruit
	        return rosterElementCharacter == rosterElementCharacter.Culture.BasicTroop;
        }
        
        private int GetTransformCost(RecruiterProperties prop, int numRecruited)
        {
	        // Nobles are nobleman
	        if (isNoble(prop.MinorFactionName))
	        {
		        return nobleCostPerTransform * numRecruited;
	        }
	        // Flat for other recruits
	        return costPerTransform * numRecruited;
        }

        private List<String> getPossibleMinorFactions()
        {
	        List<String> returnList = new List<String>();
	        
	        returnList.Add(ghilman);
	        returnList.Add(legionOfTheBetrayed);
	        returnList.Add(skolder);
	        returnList.Add(boar);
	        returnList.Add(wolfskins);
	        returnList.Add(hiddenHand);
	        returnList.Add(lakeRat);
	        returnList.Add(brotherhoodOfWoods);
	        returnList.Add(embersOfFlame);
	        returnList.Add(jawwal);
	        returnList.Add(karakhuzaits);
	        returnList.Add(forestPeople);
	        returnList.Add(eleftheroi);
	        returnList.Add(beniZilal);
	        if (MBObjectManager.Instance.GetObject<CharacterObject>("desert_noble_tier_0") != null)
	        {
		        // This uses a modded troop so only include if the modded troop is available.
		        returnList.Add(desertNobles);
	        }
	        
	        return returnList;
        }
        
        public override void AddRecruiterMenu(CampaignGameStarter obj)
        {

			String menuName = "recruiter_minor_faction_menu";
			String buyName = "recruiter_minor_faction_buy_recruiter";
			String payName = "recruiter_minor_faction_pay_menu";

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
					GameMenu.SwitchToMenu(payName);
				});

			}

			obj.AddGameMenuOption("town_keep", buyName, "Hire a Mercenary Recruiter", hireRecruiterDelegate, hireRecruiterConsequenceDelegate, false, 4, false);
			obj.AddGameMenuOption("castle", buyName, "Hire a Mercenary Recruiter", hireRecruiterDelegate, hireRecruiterConsequenceDelegate, false, 4, false);

			obj.AddGameMenu(payName, "The Chamberlain asks you for how many denars he should buy mercenary recruits.", null, GameOverlays.MenuOverlayType.None, GameMenu.MenuFlags.none, null);
			
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
             // protected override void DefineClassTypes()
             // {
             //     base.AddClassDefinition(typeof(RecruiterProperties), 2);
             // }

             // Token: 0x06000045 RID: 69 RVA: 0x00003515 File Offset: 0x00001715
             // protected override void DefineContainerDefinitions()
             // {
             //     base.ConstructContainerDefinition(typeof(List<RecruiterProperties>));
             // }
         }
    }
    
    
}