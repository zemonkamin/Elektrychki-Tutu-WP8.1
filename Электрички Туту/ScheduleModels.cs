using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Электрички_Туту
{
    public class ScheduleResponse
    {
        public RouteInfo route { get; set; }
        public List<ScheduleItem> schedule { get; set; }
    }

    public class RouteInfo
    {
        public string departure_station { get; set; }
        public string arrival_station { get; set; }
    }

    public class ScheduleItem
    {
        public string departure_time { get; set; }
        public string arrival_time { get; set; }
        public string train_type { get; set; }
        public string path { get; set; }
        public string train_departure_station { get; set; }
        public string train_arrival_station { get; set; }
        public string np { get; set; }

        public string travel_duration { get; set; }
        public string time_until_departure_prefix { get; set; }
        public string time_until_departure_value { get; set; }
        public Visibility time_until_departure_visibility { get; set; }
        public SolidColorBrush train_type_color { get; set; }
    }
}