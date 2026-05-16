using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Globalization;
using Windows.Phone.UI.Input;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;


namespace Электрички_Туту
{
    public sealed partial class rasp : Page
    {
        private string fromStationId;
        private string toStationId;
        private string fromStationName;
        private string toStationName;
        private ObservableCollection<ScheduleDisplayItem> todayScheduleItems;
        private ObservableCollection<ScheduleDisplayItem> tomorrowScheduleItems;
        private Dictionary<string, string> trainRoutesByKey = new Dictionary<string, string>();

        public rasp()
        {
            this.InitializeComponent();
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;

            todayScheduleItems = new ObservableCollection<ScheduleDisplayItem>();
            tomorrowScheduleItems = new ObservableCollection<ScheduleDisplayItem>();
            TodayScheduleListView.ItemsSource = todayScheduleItems;
            TomorrowScheduleListView.ItemsSource = tomorrowScheduleItems;

            TodayScheduleListView.ItemClick += ScheduleListView_ItemClick;
            TomorrowScheduleListView.ItemClick += ScheduleListView_ItemClick;
            TodayScheduleListView.IsItemClickEnabled = true;
            TomorrowScheduleListView.IsItemClickEnabled = true;
        }

        void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
                e.Handled = true;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Dictionary<string, string> parameters = e.Parameter as Dictionary<string, string>;
            if (parameters != null)
            {
                fromStationId = parameters.GetValueOrDefault("fromId");
                toStationId = parameters.GetValueOrDefault("toId");
                fromStationName = parameters.GetValueOrDefault("fromName");
                toStationName = parameters.GetValueOrDefault("toName");
            }

            if (!string.IsNullOrEmpty(fromStationName) && !string.IsNullOrEmpty(toStationName))
            {
                TodayStationsTextBlock.Text = string.Format(CultureInfo.InvariantCulture, "{0} → {1}", fromStationName, toStationName);
                TomorrowStationsTextBlock.Text = string.Format(CultureInfo.InvariantCulture, "{0} → {1}", fromStationName, toStationName);
            }

            LoadTodayScheduleData();
            LoadTomorrowScheduleData();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
            base.OnNavigatedFrom(e);
        }

        private void ScheduleListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ScheduleDisplayItem item = e.ClickedItem as ScheduleDisplayItem;
            if (item != null && !string.IsNullOrEmpty(item.np))
            {
                var parameters = new Dictionary<string, string> { { "np", item.np } };
                Frame.Navigate(typeof(stations), parameters);
            }
        }

        private void DatePivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private async void LoadTodayScheduleData()
        {
            TodayLoadingProgressRing.IsActive = true;
            TodayLoadingProgressRing.Visibility = Visibility.Visible;
            TodayScheduleScrollViewer.Visibility = Visibility.Collapsed;

            try
            {
                string url = BuildScheduleUrl(DateTime.Now);

                using (HttpClient client = CreateTutuHttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string html = await response.Content.ReadAsStringAsync();
                        ScheduleResponse data = ParseTrainSchedule(html);

                        TimeSpan currentTime = DateTime.Now.TimeOfDay;
                        List<ScheduleItem> filtered = data.schedule.Where(delegate(ScheduleItem s)
                        {
                            TimeSpan dep;
                            if (TimeSpan.TryParse(s.departure_time, out dep))
                                return dep > currentTime;
                            return true;
                        }).ToList();

                        ProcessScheduleItems(filtered, currentTime, true);

                        todayScheduleItems.Clear();
                        foreach (ScheduleItem item in filtered)
                            todayScheduleItems.Add(CreateDisplayItem(item));

                        TodayNoScheduleTextBlock.Visibility = todayScheduleItems.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
                    }
                    else
                    {
                        TodayNoScheduleTextBlock.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception)
            {
                TodayNoScheduleTextBlock.Visibility = Visibility.Visible;
            }
            finally
            {
                TodayLoadingProgressRing.IsActive = false;
                TodayLoadingProgressRing.Visibility = Visibility.Collapsed;
                TodayScheduleScrollViewer.Visibility = Visibility.Visible;
            }
        }

        private async void LoadTomorrowScheduleData()
        {
            TomorrowLoadingProgressRing.IsActive = true;
            TomorrowLoadingProgressRing.Visibility = Visibility.Visible;
            TomorrowScheduleScrollViewer.Visibility = Visibility.Collapsed;

            try
            {

                string url = BuildScheduleUrl("tomorrow");
                ScheduleResponse data = null;

                using (HttpClient client = CreateTutuHttpClient())
                {
                    data = await LoadScheduleByUrl(client, url);

                    if (data == null || data.schedule == null || data.schedule.Count == 0)
                    {
                        string fallbackUrl = BuildScheduleUrl(DateTime.Now.AddDays(1));
                        if (!string.Equals(url, fallbackUrl, StringComparison.OrdinalIgnoreCase))
                            data = await LoadScheduleByUrl(client, fallbackUrl);
                    }

                    if (data != null && data.schedule != null)
                    {
                        ProcessScheduleItems(data.schedule, null, false);

                        tomorrowScheduleItems.Clear();
                        foreach (ScheduleItem item in data.schedule)
                            tomorrowScheduleItems.Add(CreateDisplayItem(item));

                        TomorrowNoScheduleTextBlock.Visibility = tomorrowScheduleItems.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
                    }
                    else
                    {
                        TomorrowNoScheduleTextBlock.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception)
            {
                TomorrowNoScheduleTextBlock.Visibility = Visibility.Visible;
            }
            finally
            {
                TomorrowLoadingProgressRing.IsActive = false;
                TomorrowLoadingProgressRing.Visibility = Visibility.Collapsed;
                TomorrowScheduleScrollViewer.Visibility = Visibility.Visible;
            }
        }

        private async Task<ScheduleResponse> LoadScheduleByUrl(HttpClient client, string url)
        {
            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            string html = await response.Content.ReadAsStringAsync();
            return ParseTrainSchedule(html);
        }

        private string BuildScheduleUrl(DateTime date)
        {
            string dateValue = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return BuildScheduleUrl(dateValue);
        }

        private string BuildScheduleUrl(string dateValue)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "https://www.tutu.ru/rasp.php?st1={0}&st2={1}&date={2}",
                Uri.EscapeDataString(fromStationId ?? string.Empty),
                Uri.EscapeDataString(toStationId ?? string.Empty),
                Uri.EscapeDataString(dateValue ?? string.Empty));
        }

        private HttpClient CreateTutuHttpClient()
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9,en;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.tutu.ru/");
            return client;
        }

        private void ProcessScheduleItems(List<ScheduleItem> items, TimeSpan? currentTime, bool isToday)
        {
            for (int i = 0; i < items.Count; i++)
            {
                ScheduleItem item = items[i];

                TimeSpan dep;
                TimeSpan arr;
                if (TimeSpan.TryParse(item.departure_time, out dep) &&
                    TimeSpan.TryParse(item.arrival_time, out arr))
                {
                    TimeSpan duration = arr >= dep ? arr - dep : (TimeSpan.FromDays(1) - dep) + arr;
                    item.travel_duration = duration.TotalHours >= 1
                        ? string.Format(CultureInfo.InvariantCulture, "{0} ч {1} мин", (int)duration.TotalHours, duration.Minutes)
                        : string.Format(CultureInfo.InvariantCulture, "{0} мин", (int)duration.TotalMinutes);
                }
                else
                {
                    item.travel_duration = string.Empty;
                }

                if (isToday && currentTime.HasValue && i < 3)
                {
                    TimeSpan departure;
                    if (TimeSpan.TryParse(item.departure_time, out departure))
                    {
                        TimeSpan until = departure - currentTime.Value;
                        if (until.TotalMinutes <= 0)
                        {
                            item.time_until_departure_value = "Сейчас";
                            item.time_until_departure_prefix = string.Empty;
                        }
                        else if (until.TotalMinutes < 60)
                        {
                            item.time_until_departure_prefix = "Через ";
                            item.time_until_departure_value = string.Format(CultureInfo.InvariantCulture, "{0} мин", (int)until.TotalMinutes);
                        }
                        else
                        {
                            item.time_until_departure_prefix = "Через ";
                            item.time_until_departure_value = string.Format(CultureInfo.InvariantCulture, "{0} ч {1} мин", (int)until.TotalHours, until.Minutes);
                        }
                        item.time_until_departure_visibility = Visibility.Visible;
                    }
                    else
                    {
                        item.time_until_departure_visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    item.time_until_departure_visibility = Visibility.Collapsed;
                }

                string type = item.train_type == null ? string.Empty : item.train_type.ToLower();
                if (type.Contains("иволга"))
                    item.train_type_color = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 89, 147));
                else if (type.Contains("стандарт плюс") || type.Contains("стандартплюс"))
                    item.train_type_color = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 48, 191, 120));
                else if (type.Contains("экспресс") || type.Contains("рэкс"))
                    item.train_type_color = new SolidColorBrush(Windows.UI.Colors.Orange);
                else
                    item.train_type_color = new SolidColorBrush(Windows.UI.Colors.Gray);
            }
        }

        private ScheduleResponse ParseTrainSchedule(string html)
        {
            ScheduleResponse result = new ScheduleResponse
            {
                route = new RouteInfo(),
                schedule = new List<ScheduleItem>()
            };

            if (string.IsNullOrEmpty(html))
                return result;

            try
            {
                string preparedHtml = Regex.Replace(html, @"[\r\n\t]+", " ");

                MatchCollection trainMatches = Regex.Matches(
                    preparedHtml,
                    "<a\\b[^>]*href\\s*=\\s*[\'\"“”][^\'\"“”>]*view\\.php\\?np=([A-Za-z0-9]+)[^\'\"“”>]*[\'\"“”][^>]*>(.*?)</a>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);

                HashSet<string> added = new HashSet<string>();

                for (int i = 0; i < trainMatches.Count; i++)
                {
                    Match match = trainMatches[i];
                    string np = match.Groups[1].Value;
                    string trainName = CleanHtmlText(match.Groups[2].Value);

                    if (string.IsNullOrEmpty(np) || string.IsNullOrEmpty(trainName))
                        continue;

                    int blockStart = match.Index;
                    int blockEnd;
                    if (i + 1 < trainMatches.Count)
                        blockEnd = trainMatches[i + 1].Index;
                    else
                        blockEnd = Math.Min(preparedHtml.Length, blockStart + 4000);

                    if (blockEnd <= blockStart)
                        continue;

                    string block = preparedHtml.Substring(blockStart, blockEnd - blockStart);
                    ScheduleItem item = ParseScheduleBlock(block, np, trainName);

                    if (item == null)
                        continue;

                    string key = item.np + "|" + item.departure_time + "|" + item.arrival_time;
                    if (added.Contains(key))
                        continue;

                    added.Add(key);
                    result.schedule.Add(item);
                }

                if (result.schedule.Count == 0)
                    ParseScheduleFallback(preparedHtml, result);
            }
            catch (Exception)
            {

            }

            return result;
        }

        private ScheduleItem ParseScheduleBlock(string htmlBlock, string np, string trainName)
        {
            string text = CleanHtmlText(htmlBlock);
            if (string.IsNullOrEmpty(text))
                return null;

            int nameIndex = text.IndexOf(trainName, StringComparison.OrdinalIgnoreCase);
            if (nameIndex >= 0)
                text = text.Substring(nameIndex + trainName.Length);

            string departureTime = string.Empty;
            string arrivalTime = string.Empty;

            Match timeWithDurationMatch = Regex.Match(
                text,
                @"(?<dep>\b\d{1,2}:\d{2}\b)\s+(?:(?:\d+\s*ч\s*)?\d+\s*мин(?:\s*в\s*пути)?\s+)?(?<arr>\b\d{1,2}:\d{2}\b)",
                RegexOptions.IgnoreCase);

            if (timeWithDurationMatch.Success)
            {
                departureTime = NormalizeTime(timeWithDurationMatch.Groups["dep"].Value);
                arrivalTime = NormalizeTime(timeWithDurationMatch.Groups["arr"].Value);
            }
            else
            {
                MatchCollection times = Regex.Matches(text, @"\b\d{1,2}:\d{2}\b");
                if (times.Count >= 2)
                {
                    departureTime = NormalizeTime(times[0].Value);
                    arrivalTime = NormalizeTime(times[1].Value);
                }
            }

            if (string.IsNullOrEmpty(departureTime) || string.IsNullOrEmpty(arrivalTime))
                return null;

            ScheduleItem item = new ScheduleItem();
            item.np = np;
            item.departure_time = departureTime;
            item.arrival_time = arrivalTime;

            Match typeMatch = Regex.Match(
                text,
                @"\b(Иволга|Стандарт\s*плюс|Стандартплюс|Экспресс|Ласточка|Спутник|РЭКС|Комфорт)\b",
                RegexOptions.IgnoreCase);

            string trainType = string.Empty;
            if (typeMatch.Success)
                trainType = NormalizeTrainType(typeMatch.Value);

            string trainRoute = ExtractTrainRoute(trainName);
            item.train_type = trainType;
            StoreTrainRoute(item, trainRoute);

            Match pathMatch = Regex.Match(text, @"\bПуть\s*([0-9A-Za-zА-Яа-я]+)", RegexOptions.IgnoreCase);
            if (pathMatch.Success)
                item.path = pathMatch.Groups[1].Value;

            return item;
        }

        private void ParseScheduleFallback(string html, ScheduleResponse result)
        {
            string text = CleanHtmlText(html);
            if (string.IsNullOrEmpty(text))
                return;

            MatchCollection timePairs = Regex.Matches(
                text,
                @"(?<dep>\b\d{1,2}:\d{2}\b)\s+(?:(?:\d+\s*ч\s*)?\d+\s*мин(?:\s*в\s*пути)?\s+)?(?<arr>\b\d{1,2}:\d{2}\b)",
                RegexOptions.IgnoreCase);

            HashSet<string> added = new HashSet<string>();
            int index = 1;

            foreach (Match match in timePairs)
            {
                string dep = NormalizeTime(match.Groups["dep"].Value);
                string arr = NormalizeTime(match.Groups["arr"].Value);
                string key = dep + "|" + arr;

                if (added.Contains(key))
                    continue;

                added.Add(key);

                ScheduleItem item = new ScheduleItem();
                item.np = string.Empty;
                item.departure_time = dep;
                item.arrival_time = arr;
                item.train_type = string.Empty;

                result.schedule.Add(item);
                index++;
            }
        }

        private ScheduleDisplayItem CreateDisplayItem(ScheduleItem item)
        {
            ScheduleDisplayItem displayItem = new ScheduleDisplayItem();
            if (item == null)
                return displayItem;

            displayItem.np = item.np;
            displayItem.departure_time = item.departure_time;
            displayItem.arrival_time = item.arrival_time;
            displayItem.travel_duration = item.travel_duration;
            displayItem.path = item.path;
            displayItem.train_type = item.train_type;
            displayItem.train_type_color = item.train_type_color;
            displayItem.time_until_departure_prefix = item.time_until_departure_prefix;
            displayItem.time_until_departure_value = item.time_until_departure_value;
            displayItem.time_until_departure_visibility = item.time_until_departure_visibility;

            string route;
            if (trainRoutesByKey.TryGetValue(BuildTrainRouteKey(item), out route))
                displayItem.train_route = route;
            else
                displayItem.train_route = string.Empty;

            displayItem.train_route_visibility = string.IsNullOrEmpty(displayItem.train_route) ? Visibility.Collapsed : Visibility.Visible;
            return displayItem;
        }

        private void StoreTrainRoute(ScheduleItem item, string trainRoute)
        {
            if (item == null || string.IsNullOrEmpty(trainRoute))
                return;

            string key = BuildTrainRouteKey(item);
            if (!string.IsNullOrEmpty(key))
                trainRoutesByKey[key] = trainRoute;
        }

        private string BuildTrainRouteKey(ScheduleItem item)
        {
            if (item == null)
                return string.Empty;

            return (item.np == null ? string.Empty : item.np) + "|" +
                   (item.departure_time == null ? string.Empty : item.departure_time) + "|" +
                   (item.arrival_time == null ? string.Empty : item.arrival_time);
        }

        private string NormalizeTime(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            TimeSpan time;
            if (TimeSpan.TryParse(value, out time))
                return time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

            return value.Trim();
        }

        private string NormalizeTrainType(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string normalized = Regex.Replace(value, @"\s+", " ").Trim();
            if (string.Equals(normalized, "Стандартплюс", StringComparison.OrdinalIgnoreCase))
                return "Стандарт плюс";

            return normalized;
        }

        private string ExtractTrainRoute(string trainName)
        {
            if (string.IsNullOrEmpty(trainName))
                return string.Empty;

            string route = CleanHtmlText(trainName);
            route = Regex.Replace(route, @"^\s*[A-Za-zА-Яа-я]?\d+(?:/\d+)?(?:\s+[A-Za-zА-Яа-я](?=\s))?\s+", string.Empty);
            route = Regex.Replace(route, @"\s*[-–—]\s*", " → ");
            route = Regex.Replace(route, @"\s+", " ").Trim();

            if (route.IndexOf("→", StringComparison.OrdinalIgnoreCase) < 0)
                return string.Empty;

            return route;
        }

        private string BuildTrainInfo(string trainType, string trainRoute)
        {
            return trainType == null ? string.Empty : trainType.Trim();
        }

        private string CleanHtmlText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string text = Regex.Replace(value, @"<script[\s\S]*?</script>", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<style[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]+>", " ");

            try
            {
                text = WebUtility.HtmlDecode(text);
            }
            catch (Exception)
            {
            }

            text = text.Replace('\u00A0', ' ');
            text = text.Replace('\u202F', ' ');
            text = text.Replace('\u2060', ' ');
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text;
        }
    }

    public class ScheduleDisplayItem
    {
        public string np { get; set; }
        public string departure_time { get; set; }
        public string arrival_time { get; set; }
        public string travel_duration { get; set; }
        public string path { get; set; }
        public string train_type { get; set; }
        public string train_route { get; set; }
        public Brush train_type_color { get; set; }
        public string time_until_departure_prefix { get; set; }
        public string time_until_departure_value { get; set; }
        public Visibility time_until_departure_visibility { get; set; }
        public Visibility train_route_visibility { get; set; }
    }

    public static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
        {
            return GetValueOrDefault(dict, key, default(TValue));
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
        {
            if (dict == null)
                return defaultValue;

            TValue value;
            if (dict.TryGetValue(key, out value))
                return value;

            return defaultValue;
        }
    }
}
