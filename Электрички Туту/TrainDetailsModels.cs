using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Электрички_Туту
{
    public class TrainDetailsResponse
    {
        public string np { get; set; }
        public string train_number { get; set; }
        public string train_name { get; set; }
        public string route { get; set; }
        public string date { get; set; }
        public string carrier { get; set; }
        public string movement_mode { get; set; }
        public List<StationInfo> stations { get; set; }
    }

    public class StationInfo
    {
        public string station { get; set; }
        public string actual_time { get; set; }
        public string scheduled_time { get; set; }
    }
}