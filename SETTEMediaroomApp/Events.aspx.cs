using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Linq;
using Newtonsoft.Json.Linq;

public partial class Events : Page
{
    private const string API_BASE = "http://172.16.40.100/kupikarta_api.php";
    private const string IMAGE_PROXY_BASE = "http://172.16.40.100/ig_pfp_loader.php?image_url=";
    private const string DEVICEGUID_TO_USERID_API = "http://172.16.40.100/get_lukify_clientidforuserid.php?deviceguid=";

    private const int DISPLAY_PAGE_SIZE = 4;
    private const int API_PAGE_SIZE = 50;

    private const int PAGE_LEFT = 40;
    private const int PAGE_TOP = 170;
    private const int COL_GAP = 20;
    private const int CARD_W = 580;
    private const int CARD_H = 220;
    private const int CARD_GAP_Y = 18;

    private const int IMAGE_LEFT = 12;
    private const int IMAGE_TOP = 10;
    private const int IMAGE_W = 556;
    private const int IMAGE_H = 112;

    protected void Page_Load(object sender, EventArgs e)
    {
        string search = (GetQuery("q") ?? "").Trim();
        int page = GetIntQuery("page", 1);

        string showLocationRaw = (GetQuery("showlocation") ?? "").Trim();
        bool showLocation = IsTrue(showLocationRaw);

        string lat = (GetQuery("lat") ?? "").Trim();
        string lon = (GetQuery("lon") ?? "").Trim();
        string radius = (GetQuery("radius") ?? "").Trim();

        string debugRaw = (GetQuery("debug") ?? "").Trim();
        bool debug = debugRaw == "1";

        string deviceGuid = (GetQuery("DeviceGuid") ?? "").Trim();
        if (string.IsNullOrEmpty(deviceGuid))
            deviceGuid = (GetQuery("deviceguid") ?? "").Trim();

        string userId = (GetQuery("userid") ?? "").Trim();
        if (string.IsNullOrEmpty(userId))
            userId = (GetQuery("user_id") ?? "").Trim();

        if (string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(deviceGuid))
            userId = GetUserIdFromDeviceGuid(deviceGuid);

        string apiDebug;
        List<EventItem> allEvents = GetAllEventsFromApi(
            search, showLocation, lat, lon, radius, debug, out apiDebug
        );

        if (page < 1) page = 1;

        int totalEvents = allEvents.Count;
        int totalPages = (int)Math.Ceiling(totalEvents / (double)DISPLAY_PAGE_SIZE);
        if (totalPages < 1) totalPages = 1;

        if (page > totalPages)
            page = totalPages;

        int startIndex = (page - 1) * DISPLAY_PAGE_SIZE;
        List<EventItem> events = new List<EventItem>();

        if (startIndex >= 0 && startIndex < totalEvents)
            events = allEvents.Skip(startIndex).Take(DISPLAY_PAGE_SIZE).ToList();

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"<MrmlPage id=""EventsPage"" appid=""lukatube.events/1.0"" width=""1280"" height=""720"">");

        string submitBase = "http://172.16.40.101/SETTEMediaroomApp/Events.aspx";

        string searchActionUrl = BuildSelfUrl(
            submitBase,
            "",
            1,
            showLocation,
            lat,
            lon,
            radius,
            debug,
            deviceGuid,
            userId
        );

        sb.AppendLine(@"<Actions>");
        sb.AppendLine(string.Format(
            @"  <Action name=""SearchEvents"" type=""submit"" data=""q"" method=""GET"" url=""page:{0}"" />",
            HttpUtility.HtmlAttributeEncode(searchActionUrl)
        ));
        sb.AppendLine(@"</Actions>");

        sb.AppendLine(@"<Panel>");
        sb.AppendLine(@"  <Text top=""20"" left=""40"" width=""1200"" height=""40"" fontstyle=""Reg30"" foreground=""argb(255,255,255,255)"">Music Events</Text>");

        sb.AppendLine(string.Format(
            @"  <EditText id=""q"" name=""q"" top=""70"" left=""40"" width=""860"" height=""48"" fontstyle=""Reg24"">{0}</EditText>",
            HttpUtility.HtmlEncode(search)
        ));

        sb.AppendLine(
            @"  <Button id=""btnSearch"" top=""70"" left=""920"" width=""320"" height=""48"">" +
            @"<Actions><Event type=""onclick"" action=""SearchEvents"" /></Actions>" +
            @"<Text alignment=""center"" justification=""center"" fontstyle=""Reg24"" foreground=""argb(255,255,255,255)"">Search</Text>" +
            @"</Button>"
        );

        if (showLocation)
        {
            sb.AppendLine(string.Format(
                @"  <Text top=""124"" left=""40"" width=""1200"" height=""24"" fontstyle=""Reg18"" foreground=""argb(255,200,200,200)"">Near me: {0}, {1}{2}</Text>",
                HttpUtility.HtmlEncode(lat),
                HttpUtility.HtmlEncode(lon),
                string.IsNullOrEmpty(radius) ? "" : " • radius " + HttpUtility.HtmlEncode(radius) + " km"
            ));
        }

        int top = PAGE_TOP;

        if (debug)
        {
            sb.AppendLine(string.Format(
                @"  <Text top=""{0}"" left=""40"" width=""1200"" height=""24"" fontstyle=""Reg18"" foreground=""argb(255,255,200,100)"">DEBUG: {1}</Text>",
                top,
                HttpUtility.HtmlEncode(apiDebug ?? "")
            ));
            top += 30;
        }

        if (events == null || events.Count == 0)
        {
            sb.AppendLine(string.Format(
                @"  <Text top=""{0}"" left=""40"" width=""1200"" height=""40"" fontstyle=""Reg28"" foreground=""argb(255,255,80,80)"">No events found</Text>",
                top
            ));
        }
        else
        {
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];

                int row = i / 2;
                int col = i % 2;

                int left = PAGE_LEFT + (col * (CARD_W + COL_GAP));
                int cardTop = top + (row * (CARD_H + CARD_GAP_Y));

                string title = ev.title ?? "";
                string titleEncoded = HttpUtility.HtmlEncode(title);

                List<string> parts = new List<string>();

                if (!string.IsNullOrEmpty(ev.date))
                    parts.Add("Date: " + ev.date);

                if (!string.IsNullOrEmpty(ev.venue))
                    parts.Add(", Venue: " + ev.venue);

                if (!string.IsNullOrEmpty(ev.location))
                    parts.Add("Location: " + ev.location);

                if (!string.IsNullOrEmpty(ev.source))
                    parts.Add(", Source: " + ev.source);

                if (ev.price.HasValue)
                    parts.Add("Price: " + ev.price.Value.ToString("0.##") + " den.");
                else if (!string.IsNullOrEmpty(ev.priceText))
                    parts.Add("Price: " + ev.priceText + " den.");

                if (ev.sold_out)
                    parts.Add("Sold out");

                if (ev.closed)
                    parts.Add("Closed");

                if (ev.match_percent.HasValue)
                    parts.Add("Match: " + ev.match_percent.Value.ToString("0.0") + "%");

                if (ev.distance_km.HasValue)
                    parts.Add("Distance: " + ev.distance_km.Value.ToString("0.0") + " km");

                string subtitleParts = string.Join(",", parts);

                var evQs = HttpUtility.ParseQueryString(string.Empty);
                evQs["id"] = ev.id;
                evQs["title"] = ev.title ?? "";
                evQs["source"] = ev.source ?? "";

                if (!string.IsNullOrEmpty(deviceGuid))
                    evQs["DeviceGuid"] = deviceGuid;

                if (!string.IsNullOrEmpty(userId))
                {
                    evQs["userid"] = userId;
                    evQs["user_id"] = userId;
                    evQs["me_id"] = userId;
                }

                string buttonUrl = "http://172.16.40.101/SETTEMediaroomApp/ViewEvent.aspx?" + evQs.ToString();
                string cardId = "ev_" + SanitizeId(ev.id);

                AppendEventCard(
                    sb,
                    cardId,
                    left,
                    cardTop,
                    buttonUrl,
                    ev,
                    titleEncoded,
                    HttpUtility.HtmlEncode(subtitleParts)
                );
            }

            int totalRows = (int)Math.Ceiling(events.Count / 2.0);
            int nextTop = top + (totalRows * (CARD_H + CARD_GAP_Y)) + 8;

            if (page < totalPages)
            {
                string nextUrl = BuildSelfUrl(
                    submitBase,
                    search,
                    page + 1,
                    showLocation,
                    lat,
                    lon,
                    radius,
                    debug,
                    deviceGuid,
                    userId
                );

                sb.AppendLine(string.Format(
                    @"  <Button id=""btnMore"" top=""{0}"" left=""40"" width=""1200"" height=""56"" href=""page:{1}"">" +
                    @"    <Text alignment=""center"" justification=""center"" fontstyle=""Reg26"" foreground=""argb(255,255,255,255)"">Load More</Text>" +
                    @"  </Button>",
                    nextTop,
                    HttpUtility.HtmlAttributeEncode(nextUrl)
                ));
            }
        }

        sb.AppendLine(@"</Panel>");
        sb.AppendLine(@"</MrmlPage>");
        sb.AppendLine(@"</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    private void AppendEventCard(
        StringBuilder sb,
        string cardId,
        int left,
        int top,
        string buttonUrl,
        EventItem ev,
        string titleEncoded,
        string subtitleEncoded
    )
    {
        string imageTag = "";
        if (!string.IsNullOrEmpty(ev.image))
        {
            string proxiedImageUrl = BuildProxyImageUrl(ev.image, ev.source);
            imageTag = string.Format(
                @"<Image top=""{0}"" left=""{1}"" width=""{2}"" height=""{3}"" url=""{4}"" />",
                IMAGE_TOP,
                IMAGE_LEFT,
                IMAGE_W,
                IMAGE_H,
                HttpUtility.HtmlAttributeEncode(proxiedImageUrl)
            );
        }

        sb.AppendLine(string.Format(
            @"  <Button id=""{0}"" top=""{1}"" left=""{2}"" width=""{3}"" height=""{4}"" href=""page:{5}"">" +
            @"    {6}" +
            @"    <Text top=""132"" left=""20"" width=""540"" height=""30"" alignment=""center"" justification=""center"" fontstyle=""Reg26"" foreground=""argb(255,255,255,255)"">{7}</Text>" +
            @"    <Text top=""170"" left=""20"" width=""540"" height=""34"" alignment=""center"" justification=""center"" fontstyle=""Reg18"" foreground=""argb(255,200,200,200)"">{8}</Text>" +
            @"  </Button>",
            HttpUtility.HtmlAttributeEncode(cardId),
            top,
            left,
            CARD_W,
            CARD_H,
            HttpUtility.HtmlAttributeEncode(buttonUrl),
            imageTag,
            titleEncoded,
            subtitleEncoded
        ));
    }

    private string GetUserIdFromDeviceGuid(string deviceGuid)
    {
        if (string.IsNullOrEmpty(deviceGuid))
            return "";

        try
        {
            string apiUrl = DEVICEGUID_TO_USERID_API + HttpUtility.UrlEncode(deviceGuid);

            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                string response = wc.DownloadString(apiUrl);

                if (string.IsNullOrWhiteSpace(response))
                    return "";

                response = response.Trim();

                if (response.StartsWith("{") || response.StartsWith("["))
                {
                    try
                    {
                        JToken root = JToken.Parse(response);

                        if (root.Type == JTokenType.Object)
                        {
                            JObject obj = (JObject)root;

                            string[] keys = new[] { "userid", "user_id", "clientid", "id" };
                            foreach (string key in keys)
                            {
                                JToken token;
                                if (obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out token) && token != null)
                                {
                                    string s = token.ToString().Trim();
                                    if (!string.IsNullOrEmpty(s))
                                        return s;
                                }
                            }

                            if (obj.Properties().Count() == 1)
                            {
                                var p = obj.Properties().First();
                                if (p.Value != null)
                                {
                                    string s = p.Value.ToString().Trim();
                                    if (!string.IsNullOrEmpty(s))
                                        return s;
                                }
                            }
                        }
                        else if (root.Type == JTokenType.Array)
                        {
                            foreach (var item in (JArray)root)
                            {
                                if (item == null) continue;

                                if (item.Type == JTokenType.Object)
                                {
                                    JObject obj = (JObject)item;
                                    string[] keys = new[] { "userid", "user_id", "clientid", "id" };

                                    foreach (string key in keys)
                                    {
                                        JToken token;
                                        if (obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out token) && token != null)
                                        {
                                            string s = token.ToString().Trim();
                                            if (!string.IsNullOrEmpty(s))
                                                return s;
                                        }
                                    }
                                }
                                else
                                {
                                    string s = item.ToString().Trim();
                                    if (!string.IsNullOrEmpty(s))
                                        return s;
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                return response.Trim().Trim('"');
            }
        }
        catch
        {
            return "";
        }
    }

    private string BuildProxyImageUrl(string imageUrl, string source)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return "";

        string absoluteUrl = imageUrl;

        if (!imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(source) && source.IndexOf("karti", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                absoluteUrl = "https://www.karti.com.mk/" + imageUrl.TrimStart('/');
            }
            else
            {
                absoluteUrl = "https://kupikarta.com/" + imageUrl.TrimStart('/');
            }
        }

        return IMAGE_PROXY_BASE + HttpUtility.UrlEncode(absoluteUrl);
    }

    private List<EventItem> GetAllEventsFromApi(
        string q,
        bool showLocation,
        string lat,
        string lon,
        string radius,
        bool debug,
        out string apiDebug
    )
    {
        apiDebug = "";
        var allEvents = new List<EventItem>();
        int page = 1;
        int safetyPages = 50;

        string lastDebugUrl = "";
        string lastDebugResponse = "";

        for (int i = 0; i < safetyPages; i++)
        {
            var url = new StringBuilder(API_BASE);
            url.Append("?page=").Append(HttpUtility.UrlEncode(page.ToString()));
            url.Append("&size=").Append(HttpUtility.UrlEncode(API_PAGE_SIZE.ToString()));

            if (!string.IsNullOrEmpty(q))
                url.Append("&q=").Append(HttpUtility.UrlEncode(q));

            if (showLocation)
                url.Append("&showlocation=true");

            if (!string.IsNullOrEmpty(lat))
                url.Append("&lat=").Append(HttpUtility.UrlEncode(lat));

            if (!string.IsNullOrEmpty(lon))
                url.Append("&lon=").Append(HttpUtility.UrlEncode(lon));

            if (!string.IsNullOrEmpty(radius))
                url.Append("&radius=").Append(HttpUtility.UrlEncode(radius));

            string apiUrl = url.ToString();
            string responseText = null;

            try
            {
                var req = (HttpWebRequest)WebRequest.Create(apiUrl);
                req.Method = "GET";
                req.Timeout = 15000;
                req.UserAgent = "LukaTubeEvents/1.0";
                req.Accept = "application/json";

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream()))
                {
                    responseText = sr.ReadToEnd();
                }

                lastDebugUrl = apiUrl;
                lastDebugResponse = responseText ?? "";
            }
            catch (Exception ex)
            {
                if (debug)
                    apiDebug = apiUrl + "\r\nERROR: " + ex.Message;

                break;
            }

            if (string.IsNullOrWhiteSpace(responseText))
                break;

            List<EventItem> pageEvents = ParseEventsResponse(responseText);
            if (pageEvents == null || pageEvents.Count == 0)
            {
                if (debug)
                    apiDebug = apiUrl + "\r\nNO PARSED EVENTS\r\n" + responseText;
                break;
            }

            allEvents.AddRange(pageEvents);

            if (pageEvents.Count < API_PAGE_SIZE)
                break;

            page++;
        }

        if (debug && string.IsNullOrEmpty(apiDebug))
            apiDebug = lastDebugUrl + "\r\n" + lastDebugResponse;

        return allEvents;
    }

    private List<EventItem> ParseEventsResponse(string responseText)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return new List<EventItem>();

            responseText = responseText.TrimStart('\uFEFF', '\u200B', ' ', '\t', '\r', '\n');

            JToken rootToken = JToken.Parse(responseText);
            JArray items = null;

            if (rootToken.Type == JTokenType.Array)
            {
                items = (JArray)rootToken;
            }
            else if (rootToken.Type == JTokenType.Object)
            {
                JObject root = (JObject)rootToken;

                JToken successToken = root["success"];
                if (successToken != null && successToken.Type == JTokenType.Boolean && root.Value<bool>("success") == false)
                {
                    return new List<EventItem>();
                }

                if (root["events"] != null && root["events"].Type == JTokenType.Array)
                    items = (JArray)root["events"];
                else if (root["data"] != null && root["data"]["events"] != null && root["data"]["events"].Type == JTokenType.Array)
                    items = (JArray)root["data"]["events"];
                else if (root["results"] != null && root["results"].Type == JTokenType.Array)
                    items = (JArray)root["results"];
                else if (root["items"] != null && root["items"].Type == JTokenType.Array)
                    items = (JArray)root["items"];
            }

            if (items == null)
                return new List<EventItem>();

            var list = new List<EventItem>();

            foreach (JToken token in items)
            {
                if (token == null || token.Type != JTokenType.Object)
                    continue;

                try
                {
                    string id =
                        GetString(token, "id") ??
                        GetString(token, "eventid") ??
                        GetString(token, "Id") ??
                        Guid.NewGuid().ToString("N");

                    EventItem item = new EventItem();
                    item.id = id;
                    item.title = GetString(token, "title") ?? "";
                    item.date = GetString(token, "date") ?? "";
                    item.time = GetString(token, "time") ?? "";
                    item.location = GetString(token, "location") ?? "";
                    item.venue = GetString(token, "venue") ?? "";
                    item.image = GetString(token, "image") ?? "";
                    item.source = GetString(token, "source") ?? "";
                    item.source_url = GetString(token, "source_url") ?? "";
                    item.ticket_link = GetString(token, "ticket_link") ?? "";
                    item.maps_url = GetString(token, "maps_url") ?? "";
                    item.sold_out = GetBool(token, "sold_out");
                    item.closed = GetBool(token, "closed");
                    item.match_percent = GetDecimal(token, "match_percent");
                    item.distance_km = GetDecimal(token, "distance_km");
                    item.priceText = GetString(token, "price");

                    if (string.IsNullOrEmpty(item.image))
                        item.image = GetString(token, "thumbnail") ?? GetString(token, "thumb") ?? "";

                    list.Add(item);
                }
                catch
                {
                }
            }

            return list;
        }
        catch
        {
            return new List<EventItem>();
        }
    }

    private string GetString(JToken token, string key)
    {
        try
        {
            JToken v = token[key];
            if (v == null || v.Type == JTokenType.Null)
                return null;

            string s = Convert.ToString(v);
            if (string.IsNullOrWhiteSpace(s))
                return null;

            return s.Trim();
        }
        catch
        {
            return null;
        }
    }

    private bool GetBool(JToken token, string key)
    {
        try
        {
            JToken v = token[key];
            if (v == null || v.Type == JTokenType.Null)
                return false;

            bool b;
            if (bool.TryParse(Convert.ToString(v), out b))
                return b;

            int n;
            if (int.TryParse(Convert.ToString(v), out n))
                return n != 0;

            return false;
        }
        catch
        {
            return false;
        }
    }

    private decimal? GetDecimal(JToken token, string key)
    {
        try
        {
            JToken v = token[key];
            if (v == null || v.Type == JTokenType.Null)
                return null;

            decimal d;
            if (decimal.TryParse(Convert.ToString(v), out d))
                return d;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private string BuildSelfUrl(
        string baseUrl,
        string q,
        int page,
        bool showLocation,
        string lat,
        string lon,
        string radius,
        bool debug,
        string deviceGuid,
        string userId
    )
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);

        if (!string.IsNullOrEmpty(q))
            qs["q"] = q;

        qs["page"] = page.ToString();

        if (showLocation)
            qs["showlocation"] = "true";

        if (!string.IsNullOrEmpty(lat))
            qs["lat"] = lat;

        if (!string.IsNullOrEmpty(lon))
            qs["lon"] = lon;

        if (!string.IsNullOrEmpty(radius))
            qs["radius"] = radius;

        if (debug)
            qs["debug"] = "1";

        if (!string.IsNullOrEmpty(deviceGuid))
            qs["DeviceGuid"] = deviceGuid;

        if (!string.IsNullOrEmpty(userId))
        {
            qs["userid"] = userId;
            qs["user_id"] = userId;
            qs["me_id"] = userId;
        }

        return baseUrl + "?" + qs.ToString();
    }

    private string GetQuery(string key)
    {
        string v = Request.QueryString[key];
        if (!string.IsNullOrEmpty(v))
            return HttpUtility.UrlDecode(v);

        return "";
    }

    private int GetIntQuery(string key, int fallback)
    {
        int n;
        if (int.TryParse(GetQuery(key), out n) && n > 0)
            return n;
        return fallback;
    }

    private bool IsTrue(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        value = value.Trim().ToLowerInvariant();
        return value == "1" || value == "true" || value == "yes" || value == "on";
    }

    private string SanitizeId(string input)
    {
        if (string.IsNullOrEmpty(input)) return "unknown";
        var sb = new StringBuilder();
        foreach (char c in input)
        {
            if ((c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9'))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('_');
            }
        }

        string s = sb.ToString();
        return s.Length <= 60 ? s : s.Substring(0, 60);
    }

    public class EventItem
    {
        public string id { get; set; }
        public string title { get; set; }
        public string date { get; set; }
        public string time { get; set; }
        public decimal? price { get; set; }
        public string priceText { get; set; }
        public string image { get; set; }
        public string location { get; set; }
        public string venue { get; set; }
        public decimal? latitude { get; set; }
        public decimal? longitude { get; set; }
        public decimal? distance_km { get; set; }
        public bool sold_out { get; set; }
        public bool closed { get; set; }
        public string ticket_link { get; set; }
        public string maps_url { get; set; }
        public decimal? match_percent { get; set; }
        public string source { get; set; }
        public string source_url { get; set; }
    }
}