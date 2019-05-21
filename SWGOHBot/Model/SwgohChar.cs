using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SWGOHBot.Model
{
    public class SwgohChar
    {
        private string _name;
        public string Base_Id { get; set; }
        public string PK { get; set; }
        public string Url { get; set; }
        public string Image { get; set; }
        public string Description { get; set; }
        public string Alignment { get; set; }
        public string[] Categories { get; set; }
        public string Role { get; set; }
        public string NameLow { get; set; }
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                NameLow = value.ToLowerInvariant();
            }
        }
    }
}
