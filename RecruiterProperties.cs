using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace Recruiter
{
    public class RecruiterProperties
    {
        [SaveableField(1)]
        private MobileParty _party;

        [SaveableField(2)]
        private CultureObject _searchCulture;

        [SaveableField(3)] private String _minorFactionName;
        
        public MobileParty party {
            get { return _party; } set { _party = value; } }
        public CultureObject SearchCulture { get { return _searchCulture; } set { _searchCulture = value; } }
        
        public String MinorFactionName
        {
            get { return _minorFactionName; }
            set { _minorFactionName = value;  }
        }

        public bool IsMercenaryRecruiter()
        {
            return _minorFactionName != null;
        }
        public bool IsCultureRecruiter()
        {
            return _searchCulture != null;
        }
    }
}
