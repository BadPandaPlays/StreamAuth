using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamAuth.Models
{
    public class Points
    {
        public class UserPoints
        {
            public string channel { get; set; }
            public string username { get; set; }
            public int points { get; set; }
            public int pointsAlltime { get; set; }
            public int rank { get; set; }
        }
    }
}
