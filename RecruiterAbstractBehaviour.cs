using Helpers;
using SandBox.GauntletUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
    // This class contains shared behavior across all recruiters
    public abstract class RecruiterAbstractBehaviour : CampaignBehaviorBase
    {
        protected Random rand = new Random();
        protected List<RecruiterProperties> recruiterProperties = new List<RecruiterProperties>();
        protected bool DialogInitialized = false;
        private void OnSessionLaunched(CampaignGameStarter obj)
        {
            this.trackRecruiters();
            try
            {
                this.AddRecruiterMenu(obj);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Something screwed up in adding patrol menu. " + ex.ToString());
            }
            try
            {
                this.AddPatrolDialog(obj);
            }
            catch (Exception ex2)
            {
                MessageBox.Show("Something screwed up in adding patrol dialog. " + ex2.ToString());
            }
        }

        #region PatrolDialogMethods
	    
        protected void conversation_patrol_disband_now_on_consquence()
        {
            PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
            encounteredParty.MobileParty.RemoveParty();
            PlayerEncounter.LeaveEncounter = true;
        }

        protected void conversation_patrol_leave_on_consequence()
        {
            PlayerEncounter.LeaveEncounter = true;
        }

        protected void conversation_patrol_disband_on_consequence()
        {
            PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
            RecruiterProperties props = recruiterProperties.FirstOrDefault(prop => prop.party == encounteredParty.MobileParty);

            if(props != null)
            {
                recruiterProperties.Remove(props);
                encounteredParty.MobileParty.RemoveParty();
                PlayerEncounter.LeaveEncounter = true;
            }
        }

        protected void conversation_patrol_donate_troops_on_consequence()
        {
            PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
            PartyScreenManager.OpenScreenAsDonateTroops(encounteredParty.MobileParty);
        }

        

        #endregion

        private void trackRecruiters()
        {
            foreach (PartyBase party in Hero.MainHero.OwnedParties)
            {
                if(party.Name.ToString().EndsWith("Recruiter"))
                {
                    RecruiterProperties recruiterProps = recruiterProperties.FirstOrDefault(prop => prop.party == party.MobileParty);
                    if(recruiterProps== null)
                    {
                        recruiterProps = new RecruiterProperties();
                        recruiterProps.party = party.MobileParty;
                        recruiterProperties.Add(recruiterProps);
                    }
                }
            }
        }
        
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, new Action(this.RecruiterHourlyAi));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(this.OnDailyAITick));
            CampaignEvents.OnPartyDisbandedEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.DisbandPatrol));
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, new Action<MobileParty, PartyBase>(this.RecruiterDestroyed));
        }
        
        private void RecruiterDestroyed(MobileParty recruiter, PartyBase arg2)
        {
            if (recruiter != null)
            {
                RecruiterProperties props = recruiterProperties.FirstOrDefault(prop => prop.party == recruiter);
                if (props != null && recruiterProperties.Contains(props))
                {
                    InformationManager.DisplayMessage(new InformationMessage("Your recruiter bringing recruits to " + recruiter.Name.ToString().Substring(0, recruiter.Name.ToString().Length - " Recruiter".Length) + " has been killed!", new Color(1f, 0f, 0f)));
                    recruiterProperties.Remove(props);
                    recruiter.RemoveParty();
                }
            }
        }
        
        private void DisbandPatrol(MobileParty recruiter)
        {
            if(recruiter != null)
            {
                RecruiterProperties props = recruiterProperties.FirstOrDefault(prop => prop.party == recruiter);
                if (props != null && recruiterProperties.Contains(props))
                {
                    recruiterProperties.Remove(props);
                    recruiter.RemoveParty();
                }
            }
        }

        protected void game_menu_switch_to_village_menu(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu("castle");
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
            //dataStore.SyncData<List<RecruiterProperties>>("recruiterProperties", ref recruiterProperties);
            //if(recruiterProperties == null)
            //{
            //	recruiterProperties = new List<RecruiterProperties>();
            //}
        }

        public void InitRecruiterParty(MobileParty recruiter, TextObject name, Clan faction, Settlement homeSettlement)
        {
            recruiter.Name = name;
            recruiter.IsMilitia = true;
            recruiter.HomeSettlement = homeSettlement;
            recruiter.Party.Owner = faction.Leader;
            recruiter.SetInititave(0f, 1f, 1E+08f);
            recruiter.Party.Visuals.SetMapIconAsDirty();
            generateFood(recruiter);
        }
 
        protected void generateFood(MobileParty recruiter)
        {
            foreach (ItemObject itemObject in ItemObject.All)
            {
                bool isFood = itemObject.IsFood;
                if (isFood)
                {
                    int num = MBRandom.RoundRandomized((float)recruiter.MemberRoster.TotalManCount * (1f / (float)itemObject.Value) * 1f * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat);
                    bool flag = num > 0;
                    if (flag)
                    {
                        recruiter.ItemRoster.AddToCounts(itemObject, num, true);
                    }
                }
            }
        }
        
        protected bool game_menu_just_add_leave_conditional(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }
        
        protected bool hasSufficientRelationsship(Hero notable, int index)
        {
            switch (index)
            {
                case 0: return true; //TODO: Check
                case 1:
                    return notable.GetRelationWithPlayer() >= 0;
                case 2:
                    return notable.GetRelationWithPlayer() >= 0;
                case 3:
                    return notable.GetRelationWithPlayer() >= 5;
                case 4:
                    return notable.GetRelationWithPlayer() >= 10;
                case 5:
                    return notable.GetRelationWithPlayer() >= 20;
                default:
                    return false;
            }
        }
        
        public abstract void OnDailyAITick();
        protected abstract void RecruiterHourlyAi();
        public abstract void AddRecruiterMenu(CampaignGameStarter obj);
        public abstract void AddPatrolDialog(CampaignGameStarter obj);
        protected abstract bool patrol_talk_start_on_conditional();

        public abstract MobileParty spawnRecruiter(Settlement settlement, int cash, RecruiterProperties props);
    }
}