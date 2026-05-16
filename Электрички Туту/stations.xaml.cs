using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Windows.Phone.UI.Input;
using Windows.UI;
using Электрички_Туту;


namespace Электрички_Туту
{

    public sealed partial class stations : Page
    {
        private string npValue;
        private ObservableCollection<StationTimelineItem> stationsList;
        private static readonly string[] SupportedTimeFormats = { "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss" };
        private static readonly Regex TimeRegex = new Regex(@"\d{1,2}:\d{2}(:\d{2})?", RegexOptions.None);

        public stations()
        {
            this.InitializeComponent();
            stationsList = new ObservableCollection<StationTimelineItem>();
            StationsListView.ItemsSource = stationsList;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {

            HardwareButtons.BackPressed += HardwareButtons_BackPressed;

            if (e.Parameter != null)
            {
                var parameters = e.Parameter as Dictionary<string, string>;
                if (parameters != null && parameters.ContainsKey("np"))
                {
                    npValue = parameters["np"];

                    LoadTrainDetails();
                }
            }
        }

        void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {

            if (Frame.CanGoBack)
            {
                Frame.GoBack();
                e.Handled = true;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {

            HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
            base.OnNavigatedFrom(e);
        }

        private async void LoadTrainDetails()
        {
            if (string.IsNullOrEmpty(npValue))
                return;

            LoadingProgressRing.IsActive = true;
            LoadingProgressRing.Visibility = Visibility.Visible;
            ContentScrollViewer.Visibility = Visibility.Collapsed;

            try
            {

                string url = $"https://www.tutu.ru/view.php?np={npValue}";
                
                using (HttpClient client = new HttpClient())
                {

                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                    client.DefaultRequestHeaders.Add("Referer", "https://www.tutu.ru/");
                    
                    HttpResponseMessage response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string htmlContent = await response.Content.ReadAsStringAsync();
                        var trainDetails = ParseTrainDetailsFromHtml(htmlContent);

                        string displayTrainNumber = string.IsNullOrWhiteSpace(trainDetails.train_number) ? npValue : trainDetails.train_number;

                        var trainNumberMatch = Regex.Match(displayTrainNumber, @"\b(\d+[А-ЯA-Z]?)\b");
                        if (trainNumberMatch.Success)
                        {
                            displayTrainNumber = trainNumberMatch.Groups[1].Value;
                        }
                        else
                        {

                            displayTrainNumber = Regex.Replace(displayTrainNumber, @"[^\dА-ЯA-Z]", "");
                        }
                        
                        TrainNumberTextBlock.Text = $"ПОЕЗД №{displayTrainNumber}";
                        TrainNameTextBlock.Text = string.IsNullOrWhiteSpace(trainDetails.train_name) ? "—" : trainDetails.train_name;
                        RouteTextBlock.Text = string.IsNullOrWhiteSpace(trainDetails.route) ? "Маршрут недоступен" : trainDetails.route;
                        DateTextBlock.Text = string.IsNullOrWhiteSpace(trainDetails.date) ? "—" : trainDetails.date;
                        CarrierTextBlock.Text = string.IsNullOrWhiteSpace(trainDetails.carrier) ? "—" : trainDetails.carrier;
                        MovementModeTextBlock.Text = string.IsNullOrWhiteSpace(trainDetails.movement_mode) ? "—" : trainDetails.movement_mode;

                        stationsList.Clear();
                        if (trainDetails.stations != null)
                        {
                            var enrichedStations = BuildTimelineItems(trainDetails.stations, trainDetails.date);
                            foreach (var station in enrichedStations)
                            {
                                stationsList.Add(station);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

                TrainNumberTextBlock.Text = "Ошибка загрузки данных";
                TrainNameTextBlock.Text = "";
                RouteTextBlock.Text = "";
                DateTextBlock.Text = "";
                CarrierTextBlock.Text = "";
                MovementModeTextBlock.Text = "";
                Debug.WriteLine(ex);
            }
            finally
            {

                LoadingProgressRing.IsActive = false;
                LoadingProgressRing.Visibility = Visibility.Collapsed;
                ContentScrollViewer.Visibility = Visibility.Visible;
            }
        }

        private IEnumerable<StationTimelineItem> BuildTimelineItems(List<StationInfo> stations, string dateString)
        {
            var result = new List<StationTimelineItem>();
            if (stations == null || stations.Count == 0)
            {
                return result;
            }

            DateTime? baseDate = ParseBaseDate(dateString);

            foreach (var station in stations)
            {
                var timelineItem = new StationTimelineItem(station)
                {
                    StationDateTime = ParseStationDateTime(station, baseDate)
                };

                result.Add(timelineItem);
            }

            DateTime now = DateTime.Now;
            int firstUpcomingIndex = -1;
            for (int i = 0; i < result.Count; i++)
            {
                var item = result[i];
                bool isPastByTime = item.StationDateTime.HasValue && item.StationDateTime.Value <= now;
                if (!isPastByTime && firstUpcomingIndex == -1)
                {
                    firstUpcomingIndex = i;
                }
            }

            if (firstUpcomingIndex == -1)
            {
                firstUpcomingIndex = result.Count; // все станции уже прошли
            }

            for (int i = 0; i < result.Count; i++)
            {
                var item = result[i];
                item.IsFirst = i == 0;
                item.IsLast = i == result.Count - 1;
                bool isPastByTime = item.StationDateTime.HasValue && item.StationDateTime.Value <= now;
                bool isPastByOrder = !item.StationDateTime.HasValue && i < firstUpcomingIndex;
                item.IsPast = isPastByTime || isPastByOrder;

                item.IsCurrent = !item.IsPast && firstUpcomingIndex < result.Count && i == firstUpcomingIndex;

                if (!item.IsPast && !item.StationDateTime.HasValue && i > 0 && result[i - 1].IsPast)
                {
                    item.IsPast = true;
                }

                item.IsTopSegmentActive = i > 0 && result[i - 1].IsPast;
                item.IsBottomSegmentActive = item.IsPast;
                item.RefreshVisuals();
            }

            return result;
        }

        private DateTime? ParseBaseDate(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
            {
                return null;
            }

            DateTime parsed;
            if (DateTime.TryParse(dateString, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                return parsed;
            }

            if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                return parsed;
            }

            return null;
        }

        private DateTime? ParseStationDateTime(StationInfo station, DateTime? baseDate)
        {
            if (station == null)
            {
                return null;
            }

            string primaryTime = ExtractTimeToken(station.actual_time);
            string fallbackTime = ExtractTimeToken(station.scheduled_time);
            string normalized = !string.IsNullOrEmpty(primaryTime) ? primaryTime : fallbackTime;

            if (!string.IsNullOrEmpty(normalized))
            {

                normalized = System.Net.WebUtility.HtmlDecode(normalized);

                normalized = Regex.Replace(normalized, @"\s*\(\s*путь\s*[\dIVX]+\s*\)\s*", "", RegexOptions.IgnoreCase);
                normalized = Regex.Replace(normalized, @"путь\s*[\dIVX]+", "", RegexOptions.IgnoreCase);
                normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            }

            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            DateTime parsedTime;
            if (DateTime.TryParseExact(normalized, SupportedTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedTime) ||
                DateTime.TryParseExact(normalized, SupportedTimeFormats, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedTime))
            {
                var referenceDate = baseDate ?? DateTime.Today;
                return new DateTime(referenceDate.Year, referenceDate.Month, referenceDate.Day, parsedTime.Hour, parsedTime.Minute, parsedTime.Second);
            }

            if (DateTime.TryParse(normalized, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out parsedTime))
            {
                var referenceDate = baseDate ?? DateTime.Today;
                return new DateTime(referenceDate.Year, referenceDate.Month, referenceDate.Day, parsedTime.Hour, parsedTime.Minute, parsedTime.Second);
            }

            return null;
        }

        private string ExtractTimeToken(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            string decodedInput = System.Net.WebUtility.HtmlDecode(input);

            string cleanedInput = Regex.Replace(decodedInput, @"\s*\(\s*путь\s*[\dIVX]+\s*\)\s*", " ", RegexOptions.IgnoreCase);
            cleanedInput = Regex.Replace(cleanedInput, @"путь\s*[\dIVX]+", "", RegexOptions.IgnoreCase);
            cleanedInput = Regex.Replace(cleanedInput, @"\s+", " ").Trim();

            var match = TimeRegex.Match(cleanedInput);
            if (match.Success)
            {
                return match.Value;
            }

            return string.Empty;
        }

        private TrainDetailsResponse ParseTrainDetailsFromHtml(string htmlContent)
        {
            var result = new TrainDetailsResponse
            {
                np = npValue,
                train_number = "",
                train_name = "",
                route = "",
                date = "",
                carrier = "",
                movement_mode = "",
                stations = new List<StationInfo>()
            };
            
            try
            {

                int h1Start = htmlContent.IndexOf("<h1");
                if (h1Start != -1)
                {
                    int h1End = htmlContent.IndexOf("</h1>", h1Start);
                    if (h1End != -1)
                    {
                        string h1Content = htmlContent.Substring(h1Start, h1End - h1Start + 5);

                        var nameMatch = Regex.Match(h1Content, "<span[^>]*class=[\"'][^\"']*(?:comfort|ivolga)[\"'][^>]*>([^<]*)</span>", 
                            RegexOptions.IgnoreCase);
                        if (nameMatch.Success)
                        {
                            result.train_name = nameMatch.Groups[1].Value.Trim('"', ' ');
                        }

                        var routeMatch = Regex.Match(h1Content, "<b[^>]*>([^<]*)</b>");
                        if (routeMatch.Success)
                        {
                            result.route = routeMatch.Groups[1].Value.Trim();
                        }

                        if (string.IsNullOrEmpty(result.train_number))
                        {

                            string h1Text = Regex.Replace(h1Content, "<[^>]*>", "");
                            h1Text = System.Net.WebUtility.HtmlDecode(h1Text);

                            h1Text = Regex.Replace(h1Text, @"[\s\r\n]+", " ").Trim();

                            var numberMatch = Regex.Match(h1Text, @"электрички\s+""[^""]+""\s+([^\s]+\s*[А-ЯA-Z]?)", 
                                RegexOptions.IgnoreCase);
                            if (numberMatch.Success)
                            {
                                result.train_number = numberMatch.Groups[1].Value.Trim();
                            }
                            else
                            {

                                numberMatch = Regex.Match(h1Text, @"электрички\s+([^\s]+\s*[А-ЯA-Z]?)", 
                                    RegexOptions.IgnoreCase);
                                if (numberMatch.Success)
                                {
                                    result.train_number = numberMatch.Groups[1].Value.Trim();
                                }
                            }

                            if (!string.IsNullOrEmpty(result.train_number))
                            {
                                var trainNumberMatch = Regex.Match(result.train_number, @"\b(\d+[А-ЯA-Z]?)\b");
                                if (trainNumberMatch.Success)
                                {
                                    result.train_number = trainNumberMatch.Groups[1].Value;
                                }
                                else
                                {

                                    result.train_number = Regex.Replace(result.train_number, @"[^\dА-ЯA-Z]", "");
                                }
                            }
                        }
                    }
                }

                var dateMatch = Regex.Match(htmlContent, "<div[^>]*class=[\"'][^\"']*center_block date_block[^\"']*[\"'][^>]*>([^<]*)</div>", 
                    RegexOptions.IgnoreCase);
                if (dateMatch.Success)
                {
                    result.date = dateMatch.Groups[1].Value.Trim();
                }

                var carrierMatch = Regex.Match(htmlContent, "<div[^>]*class=[\"'][^\"']*center_block movement_block[^\"']*[\"'][^>]*>[^<]*Перевозчик[^<]*<a[^>]*>([^<]*)</a>", 
                    RegexOptions.IgnoreCase);
                if (carrierMatch.Success)
                {
                    result.carrier = carrierMatch.Groups[1].Value.Trim();
                }

                var movementMatch = Regex.Match(htmlContent, "<div[^>]*class=[\"'][^\"']*center_block movement_block[^\"']*[\"'][^>]*>[^<]*Режим движения[^<]*:([^<]*)</div>", 
                    RegexOptions.IgnoreCase);
                if (movementMatch.Success)
                {
                    result.movement_mode = movementMatch.Groups[1].Value.Trim();
                }

                int tableStart = htmlContent.IndexOf("id=\"schedule_table\"");
                if (tableStart != -1)
                {

                    int tableOpen = htmlContent.LastIndexOf("<table", tableStart);
                    if (tableOpen != -1)
                    {
                        int tableLevel = 1;
                        int pos = tableOpen + 6;
                        
                        while (tableLevel > 0 && pos < htmlContent.Length)
                        {
                            int nextOpenTable = htmlContent.IndexOf("<table", pos);
                            int nextCloseTable = htmlContent.IndexOf("</table>", pos);
                            
                            if (nextCloseTable == -1) break;
                            
                            if (nextOpenTable != -1 && nextOpenTable < nextCloseTable)
                            {
                                tableLevel++;
                                pos = nextOpenTable + 6;
                            }
                            else
                            {
                                tableLevel--;
                                pos = nextCloseTable + 8;
                            }
                        }
                        
                        if (tableLevel == 0)
                        {
                            string tableContent = htmlContent.Substring(tableOpen, pos - tableOpen);
                            ParseStationsFromTable(tableContent, result.stations);
                        }
                    }
                }

                result.train_number = Regex.Replace(result.train_number ?? "", @"\s+", " ").Trim();
                result.train_name = Regex.Replace(result.train_name ?? "", @"\s+", " ").Trim();
                result.route = Regex.Replace(result.route ?? "", @"\s+", " ").Trim();

                result.route = Regex.Replace(result.route ?? "", @"\s*на сегодня.*$", "", RegexOptions.IgnoreCase).Trim();

                if (string.IsNullOrEmpty(result.train_number))
                {

                    int breadcrumbStart = htmlContent.IndexOf("breadcrumbs_top");
                    if (breadcrumbStart != -1)
                    {

                        int divStart = htmlContent.LastIndexOf("<div", breadcrumbStart);
                        if (divStart != -1)
                        {
                            int divEnd = htmlContent.IndexOf("</div>", divStart);
                            if (divEnd != -1)
                            {
                                string breadcrumbContent = htmlContent.Substring(divStart, divEnd - divStart + 6);
                                string decodedBreadcrumb = System.Net.WebUtility.HtmlDecode(breadcrumbContent);

                                var breadcrumbMatch = Regex.Match(decodedBreadcrumb, @"электрички?\s+([^\s]+\s*[А-ЯA-Z]?)", 
                                    RegexOptions.IgnoreCase);
                                if (breadcrumbMatch.Success)
                                {
                                    result.train_number = breadcrumbMatch.Groups[1].Value.Trim();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing HTML: {ex.Message}");
            }
            
            return result;
        }

        private void ParseStationsFromTable(string tableContent, List<StationInfo> stations)
        {

            var rowMatches = Regex.Matches(tableContent, "<tr[^>]*>(.*?)</tr>", 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            foreach (Match rowMatch in rowMatches)
            {
                string rowContent = rowMatch.Groups[1].Value;

                if (rowContent.Contains("<th") || rowContent.Contains("заголовок"))
                {
                    continue;
                }

                var cellMatches = Regex.Matches(rowContent, "<td[^>]*>(.*?)</td>", 
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                if (cellMatches.Count >= 4)
                {

                    var stationLinkMatch = Regex.Match(cellMatches[1].Value, "<a[^>]*>([^<]*)</a>");
                    string station = stationLinkMatch.Success ? stationLinkMatch.Groups[1].Value.Trim() : "";

                    string actualTime = "";
                    var actualElement = cellMatches[2].Value;
                    if (actualElement.Contains("Нет данных"))
                    {
                        actualTime = "Нет данных";
                    }
                    else
                    {
                        actualTime = Regex.Replace(actualElement, "<.*?>", "").Trim();

                        actualTime = System.Net.WebUtility.HtmlDecode(actualTime);

                        actualTime = Regex.Replace(actualTime, @"\s*\(\s*путь\s*[\dIVX]+\s*\)\s*", " ", RegexOptions.IgnoreCase);
                        actualTime = Regex.Replace(actualTime, @"путь\s*[\dIVX]+", "", RegexOptions.IgnoreCase);
                        actualTime = Regex.Replace(actualTime, @"\s+", " ").Trim();
                    }

                    string scheduledTime = Regex.Replace(cellMatches[3].Value, "<.*?>", "").Trim();

                    scheduledTime = System.Net.WebUtility.HtmlDecode(scheduledTime);

                    scheduledTime = Regex.Replace(scheduledTime, @"\s*\(\s*путь\s*[\dIVX]+\s*\)\s*", " ", RegexOptions.IgnoreCase);
                    scheduledTime = Regex.Replace(scheduledTime, @"путь\s*[\dIVX]+", "", RegexOptions.IgnoreCase);
                    scheduledTime = Regex.Replace(scheduledTime, @"\s+", " ").Trim();

                    if (!string.IsNullOrEmpty(station))
                    {
                        stations.Add(new StationInfo
                        {
                            station = station,
                            actual_time = actualTime,
                            scheduled_time = scheduledTime
                        });
                    }
                }
            }
        }
    }

    public class StationTimelineItem : StationInfo
    {
        private static readonly SolidColorBrush AccentBrush = new SolidColorBrush(Color.FromArgb(255, 15, 124, 255));
        private static readonly SolidColorBrush InactiveBrush = new SolidColorBrush(Color.FromArgb(255, 50, 50, 50));
        private static readonly SolidColorBrush UpcomingBrush = new SolidColorBrush(Color.FromArgb(255, 157, 163, 174));

        public StationTimelineItem(StationInfo source)
        {
            if (source == null)
            {
                return;
            }

            station = source.station;

            actual_time = CleanTimeField(source.actual_time);

            scheduled_time = CleanTimeField(source.scheduled_time);
        }

        private string CleanTimeField(string timeValue)
        {
            if (string.IsNullOrWhiteSpace(timeValue))
                return timeValue;

            string decoded = System.Net.WebUtility.HtmlDecode(timeValue);

            string cleaned = Regex.Replace(decoded, @"\s*\(\s*путь\s*[\dIVX]+\s*\)\s*", " ", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"путь\s*[\dIVX]+", "", RegexOptions.IgnoreCase);

            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            
            return cleaned;
        }

        public DateTime? StationDateTime { get; set; }
        public bool IsPast { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsFirst { get; set; }
        public bool IsLast { get; set; }
        public bool IsTopSegmentActive { get; set; }
        public bool IsBottomSegmentActive { get; set; }

        public SolidColorBrush NodeFillBrush { get; private set; } = InactiveBrush;
        public SolidColorBrush NodeStrokeBrush { get; private set; } = InactiveBrush;
        public SolidColorBrush TopConnectorBrush { get; private set; } = InactiveBrush;
        public SolidColorBrush BottomConnectorBrush { get; private set; } = InactiveBrush;
        public Visibility TopConnectorVisibility => IsFirst ? Visibility.Collapsed : Visibility.Visible;
        public Visibility BottomConnectorVisibility => IsLast ? Visibility.Collapsed : Visibility.Visible;

        public string StatusLabel
        {
            get
            {
                if (IsPast)
                {
                    return "Проехали";
                }

                if (IsCurrent)
                {
                    return "Скоро остановка";
                }

                return "Далее";
            }
        }

        public void RefreshVisuals()
        {
            NodeFillBrush = IsPast ? AccentBrush : InactiveBrush;
            NodeStrokeBrush = (IsPast || IsCurrent) ? AccentBrush : UpcomingBrush;
            TopConnectorBrush = IsTopSegmentActive ? AccentBrush : InactiveBrush;
            BottomConnectorBrush = IsBottomSegmentActive ? AccentBrush : InactiveBrush;
        }
    }
}