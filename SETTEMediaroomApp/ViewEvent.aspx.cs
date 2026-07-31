using System;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Net;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Collections;
using System.Text.RegularExpressions;

public partial class ViewEvent : Page
{
    private const string IMAGE_PROXY_BASE = "http://172.16.40.100/ig_pfp_loader.php?image_url=";
    private const string PAGE_BASE_URL = "http://172.16.40.101/SETTEMediaroomApp/ViewEvent.aspx";
    private const string EVENTS_HOME_URL = "http://172.16.40.101/SETTEMediaroomApp/Events.aspx";
    private const string SEND_LINK_BASE = "http://172.16.40.101/SETTEMediaroomApp/SendLinkToPhone.aspx";

    protected void Page_Load(object sender, EventArgs e)
    {
        string eventId = GetQuery("id");
        if (string.IsNullOrEmpty(eventId))
        {
            Response.Write("No event ID provided.");
            Response.End();
            return;
        }

        bool showDescriptionFull = GetBoolQuery("showdesc");

        string userId = GetQuery("user_id");
        if (string.IsNullOrEmpty(userId))
            userId = GetQuery("userid");
        if (string.IsNullOrEmpty(userId))
            userId = GetQuery("me_id");

        string deviceGuid = GetQuery("deviceguid");
        if (string.IsNullOrEmpty(deviceGuid))
            deviceGuid = GetQuery("DeviceGuid");

        string apiUrl = "http://172.16.40.100/concertinfo.php?eventid=" + HttpUtility.UrlEncode(eventId);
        string json = "";

        try
        {
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                json = wc.DownloadString(apiUrl);
            }
        }
        catch
        {
            Response.Write("Failed to fetch event data.");
            Response.End();
            return;
        }

        JavaScriptSerializer js = new JavaScriptSerializer();
        var data = js.Deserialize<Dictionary<string, object>>(json);

        if (data == null || !data.ContainsKey("success") || !(bool)data["success"])
        {
            Response.Write("{\"success\":false,\"error\":\"Event not found\"}");
            Response.End();
            return;
        }

        var eData = data["event"] as Dictionary<string, object>;
        if (eData == null)
        {
            Response.Write("{\"success\":false,\"error\":\"Event data invalid\"}");
            Response.End();
            return;
        }

        Func<string, string> getString = key => eData.ContainsKey(key) && eData[key] != null ? eData[key].ToString() : "";
        Func<string, bool> getBool = key => eData.ContainsKey(key) && eData[key] != null && eData[key].ToString().ToLower() == "true";

        string id = getString("id");
        string title = getString("title");
        string date = getString("date");
        string time = getString("time");
        string price = getString("price");
        string image = getString("image");
        string location = getString("location");
        string venue = getString("venue");
        string mapsUrl = getString("maps_url");
        string ticketLink = getString("ticket_link");
        string descriptionHtml = getString("description");
        bool soldOut = getBool("sold_out");
        bool closed = getBool("closed");
        string latitude = getString("latitude");
        string longitude = getString("longitude");
        string distanceKm = getString("distance_km");
        string matchPercent = getString("match_percent");

        List<Dictionary<string, object>> zones = GetZonesFromEventObject(eData);

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"<MrmlPage id=""ViewEventPage"" appid=""lukatube.events/1.0"" width=""1280"" height=""720"">");
        sb.AppendLine("<Panel>");

        string buyHref = BuildSendLinkToPhoneUrl(userId, deviceGuid, ticketLink);
        string homeHref = EVENTS_HOME_URL;

        if (!showDescriptionFull)
        {
            string backUrl = "back";
            string backHref = "action:" + backUrl;
            string mapHref = !string.IsNullOrEmpty(mapsUrl) ? mapsUrl : "#";

            sb.AppendLine(string.Format(
                @"  <Button id=""btnBack"" top=""20"" left=""40"" width=""180"" height=""44"" href=""{0}"">
    <Text alignment=""center"" justification=""center"" fontstyle=""Reg22"" foreground=""argb(255,255,255,255)"">Back</Text>
  </Button>",
                HttpUtility.HtmlAttributeEncode(backHref)));

            sb.AppendLine(string.Format(
                @"  <Button id=""btnEventsHome"" top=""20"" left=""240"" width=""180"" height=""44"" href=""page:{0}"">
    <Text alignment=""center"" justification=""center"" fontstyle=""Reg22"" foreground=""argb(255,255,255,255)"">Events Home</Text>
  </Button>",
                HttpUtility.HtmlAttributeEncode(homeHref)));

            if (!string.IsNullOrEmpty(ticketLink))
            {
                sb.AppendLine(string.Format(
                    @"  <Button id=""btnBuy"" top=""20"" left=""1040"" width=""200"" height=""44"" href=""page:{0}"">
    <Text alignment=""center"" justification=""center"" fontstyle=""Reg22"" foreground=""argb(255,255,255,255)"">Buy Ticket</Text>
  </Button>",
                    HttpUtility.HtmlAttributeEncode(buyHref)));
            }

            if (!string.IsNullOrEmpty(image))
            {
                sb.AppendLine(string.Format(
                    @"  <Image top=""80"" left=""40"" width=""360"" height=""240"" url=""{0}"" />",
                    HttpUtility.HtmlAttributeEncode(BuildProxyImageUrl(image))));
            }

            sb.AppendLine(string.Format(
                @"  <Text top=""80"" left=""430"" width=""780"" height=""40"" fontstyle=""Reg30"" foreground=""argb(255,255,255,255)"">{0}</Text>",
                HttpUtility.HtmlEncode(string.IsNullOrEmpty(title) ? "Event" : title)));

            int y = 132;

            if (!string.IsNullOrEmpty(date))
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""{1}"" left=""430"" width=""780"" height=""28"" fontstyle=""Reg22"" foreground=""argb(255,200,200,200)"">Date: {0}</Text>",
                    HttpUtility.HtmlEncode(date), y));
                y += 34;
            }

            if (!string.IsNullOrEmpty(time))
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""{1}"" left=""430"" width=""780"" height=""28"" fontstyle=""Reg22"" foreground=""argb(255,200,200,200)"">Time: {0}</Text>",
                    HttpUtility.HtmlEncode(time), y));
                y += 34;
            }

            if (!string.IsNullOrEmpty(price))
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""{1}"" left=""430"" width=""780"" height=""28"" fontstyle=""Reg22"" foreground=""argb(255,200,200,200)"">Price: {0} den.</Text>",
                    HttpUtility.HtmlEncode(price), y));
                y += 34;
            }

            if (!string.IsNullOrEmpty(venue))
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""{1}"" left=""430"" width=""780"" height=""28"" fontstyle=""Reg22"" foreground=""argb(255,200,200,200)"">Venue: {0}</Text>",
                    HttpUtility.HtmlEncode(venue), y));
                y += 34;
            }

            if (!string.IsNullOrEmpty(location))
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""{1}"" left=""430"" width=""780"" height=""28"" fontstyle=""Reg22"" foreground=""argb(255,200,200,200)"">Location: {0}</Text>",
                    HttpUtility.HtmlEncode(location), y));
                y += 34;
            }

            if (!string.IsNullOrEmpty(latitude) && !string.IsNullOrEmpty(longitude))
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""{2}"" left=""430"" width=""780"" height=""28"" fontstyle=""Reg20"" foreground=""argb(255,180,180,180)"">Lat/Lon: {0}, {1}</Text>",
                    HttpUtility.HtmlEncode(latitude),
                    HttpUtility.HtmlEncode(longitude),
                    y));
                y += 30;
            }

            if (!string.IsNullOrEmpty(matchPercent))
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""{1}"" left=""430"" width=""780"" height=""28"" fontstyle=""Reg20"" foreground=""argb(255,180,180,180)"">Match: {0}%</Text>",
                    HttpUtility.HtmlEncode(matchPercent), y));
                y += 30;
            }

            if (!string.IsNullOrEmpty(distanceKm))
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""{1}"" left=""430"" width=""780"" height=""28"" fontstyle=""Reg20"" foreground=""argb(255,180,180,180)"">Distance: {0} km</Text>",
                    HttpUtility.HtmlEncode(distanceKm), y));
                y += 30;
            }

            int descriptionTop = y + 8;
            int nextY = descriptionTop;

            if (!string.IsNullOrEmpty(descriptionHtml))
            {
                string descriptionPreview = HtmlToPlainText(descriptionHtml);
                descriptionPreview = NormalizeDescriptionText(descriptionPreview);

                string previewToShow = Truncate(descriptionPreview, 180);

                sb.AppendLine(string.Format(
                    @"  <Text top=""{0}"" left=""430"" width=""780"" height=""110"" fontstyle=""Reg20"" foreground=""argb(255,230,230,230)"">{1}</Text>",
                    descriptionTop,
                    HttpUtility.HtmlEncode(previewToShow)));

                nextY = descriptionTop + 120;

                if (descriptionPreview.Length > previewToShow.Length)
                {
                    string toggleUrl = BuildCurrentPageUrl(eventId, true, userId, deviceGuid);
                    sb.AppendLine(string.Format(
                        @"  <Button id=""btnToggleDescription"" top=""{0}"" left=""430"" width=""240"" height=""44"" href=""page:{1}"">
    <Text alignment=""center"" justification=""center"" fontstyle=""Reg22"" foreground=""argb(255,255,255,255)"">Load more</Text>
  </Button>",
                        nextY,
                        HttpUtility.HtmlAttributeEncode(toggleUrl)));

                    nextY += 60;
                }
            }

            if (zones != null && zones.Count > 0)
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""{0}"" left=""430"" width=""780"" height=""28"" fontstyle=""Reg24"" foreground=""argb(255,255,255,255)"">Zones</Text>",
                    nextY));

                nextY += 34;

                AppendZones(sb, zones, 430, nextY, 780, 20, 28, 640);
                nextY += Math.Min(zones.Count * 30, 220);
            }

            if (soldOut)
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""{0}"" left=""430"" width=""780"" height=""28"" fontstyle=""Reg22"" foreground=""argb(255,255,80,80)"">Sold out</Text>",
                    nextY));
                nextY += 34;
            }

            if (closed)
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""{0}"" left=""430"" width=""780"" height=""28"" fontstyle=""Reg22"" foreground=""argb(255,255,80,80)"">Closed</Text>",
                    nextY));
                nextY += 34;
            }

            string buttonTop = Math.Max(nextY + 10, 500).ToString();

            if (!string.IsNullOrEmpty(mapsUrl))
            {
                sb.AppendLine(string.Format(
                    @"  <Button id=""btnMap"" top=""{1}"" left=""730"" width=""280"" height=""52"" href=""page:{0}"">
    <Text alignment=""center"" justification=""center"" fontstyle=""Reg24"" foreground=""argb(255,255,255,255)"">Open Map</Text>
  </Button>",
                    HttpUtility.HtmlAttributeEncode(mapHref),
                    buttonTop));
            }

            if (!string.IsNullOrEmpty(id))
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""660"" left=""40"" width=""1200"" height=""24"" fontstyle=""Reg18"" foreground=""argb(255,140,140,140)"">Event ID: {0}</Text>",
                    HttpUtility.HtmlEncode(id)));
            }
        }
        else
        {
            string desc = HtmlToPlainText(descriptionHtml);
            desc = NormalizeDescriptionText(desc);

            string backToShortUrl = BuildCurrentPageUrl(eventId, false, userId, deviceGuid);

            sb.AppendLine(string.Format(
                @"  <Button id=""btnCloseDescription"" top=""20"" left=""40"" width=""180"" height=""44"" href=""page:{0}"">
    <Text alignment=""center"" justification=""center"" fontstyle=""Reg22"" foreground=""argb(255,255,255,255)"">Show less</Text>
  </Button>",
                HttpUtility.HtmlAttributeEncode(backToShortUrl)));

            sb.AppendLine(string.Format(
                @"  <Button id=""btnEventsHome"" top=""20"" left=""240"" width=""180"" height=""44"" href=""page:{0}"">
    <Text alignment=""center"" justification=""center"" fontstyle=""Reg22"" foreground=""argb(255,255,255,255)"">Events Home</Text>
  </Button>",
                HttpUtility.HtmlAttributeEncode(homeHref)));

            if (!string.IsNullOrEmpty(ticketLink))
            {
                sb.AppendLine(string.Format(
                    @"  <Button id=""btnBuyFull"" top=""20"" left=""1040"" width=""200"" height=""44"" href=""page:{0}"">
    <Text alignment=""center"" justification=""center"" fontstyle=""Reg22"" foreground=""argb(255,255,255,255)"">Buy Ticket</Text>
  </Button>",
                    HttpUtility.HtmlAttributeEncode(buyHref)));
            }

            if (!string.IsNullOrEmpty(desc))
            {
                AppendWrappedText(sb, desc, 40, 90, 1200, 24, 38, 520);
            }
            else
            {
                sb.AppendLine(@"  <Text top=""90"" left=""40"" width=""1200"" height=""28"" fontstyle=""Reg24"" foreground=""argb(255,230,230,230)"">No description available.</Text>");
            }

            int zonesTop = 560;

            if (zones != null && zones.Count > 0)
            {
                sb.AppendLine(string.Format(
                    @"  <Text top=""{0}"" left=""40"" width=""1200"" height=""28"" fontstyle=""Reg24"" foreground=""argb(255,255,255,255)"">Zones</Text>",
                    zonesTop));

                AppendZones(sb, zones, 40, zonesTop + 34, 1200, 22, 30, 625);
                zonesTop += Math.Min(zones.Count * 32, 180);
            }
        }

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    private List<Dictionary<string, object>> GetZonesFromEventObject(Dictionary<string, object> eData)
    {
        List<Dictionary<string, object>> zones = new List<Dictionary<string, object>>();

        if (eData == null || !eData.ContainsKey("zones") || eData["zones"] == null)
            return zones;

        object raw = eData["zones"];

        var asList = raw as ArrayList;
        if (asList != null)
        {
            foreach (object item in asList)
            {
                Dictionary<string, object> zone = item as Dictionary<string, object>;
                if (zone != null)
                    zones.Add(zone);
            }
            return zones;
        }

        var asObjectArray = raw as object[];
        if (asObjectArray != null)
        {
            foreach (object item in asObjectArray)
            {
                Dictionary<string, object> zone = item as Dictionary<string, object>;
                if (zone != null)
                    zones.Add(zone);
            }
        }

        return zones;
    }

    private void AppendZones(
        StringBuilder sb,
        List<Dictionary<string, object>> zones,
        int left,
        int top,
        int width,
        int fontSize,
        int lineHeight,
        int maxBottom)
    {
        if (zones == null || zones.Count == 0) return;

        int y = top;

        foreach (Dictionary<string, object> zone in zones)
        {
            if (y + lineHeight > maxBottom) break;

            string name = GetZoneString(zone, "name_first");
            if (string.IsNullOrEmpty(name))
                name = GetZoneString(zone, "name_second");
            if (string.IsNullOrEmpty(name))
                name = GetZoneString(zone, "name_third");

            string freeSeats = GetZoneIntString(zone, "free_seats");
            string price = GetZonePriceString(zone);
            bool notAvailable = GetZoneBool(zone, "not_available");

            StringBuilder line = new StringBuilder();

            if (!string.IsNullOrEmpty(name))
                line.Append(name);

            if (!string.IsNullOrEmpty(price))
            {
                if (line.Length > 0) line.Append(" - ");
                line.Append(price).Append(" den.");
            }

            if (!string.IsNullOrEmpty(freeSeats))
            {
                if (line.Length > 0) line.Append(" - ");
                line.Append("Free Seats: ").Append(freeSeats);
            }

            if (notAvailable)
            {
                if (line.Length > 0) line.Append(" - ");
                line.Append("Not available");
            }

            if (line.Length == 0)
                line.Append("Zone");

            sb.AppendLine(string.Format(
                @"  <Text top=""{0}"" left=""{1}"" width=""{2}"" height=""{3}"" fontstyle=""Reg{4}"" foreground=""argb(255,230,230,230)"">{5}</Text>",
                y,
                left,
                width,
                lineHeight,
                fontSize,
                HttpUtility.HtmlEncode(line.ToString())));

            y += lineHeight;
        }
    }

    private string GetZoneString(Dictionary<string, object> zone, string key)
    {
        if (zone == null || !zone.ContainsKey(key) || zone[key] == null) return "";
        return zone[key].ToString().Trim();
    }

    private string GetZoneIntString(Dictionary<string, object> zone, string key)
    {
        if (zone == null || !zone.ContainsKey(key) || zone[key] == null) return "";
        try
        {
            return Convert.ToInt32(zone[key]).ToString();
        }
        catch
        {
            return zone[key].ToString();
        }
    }

    private string GetZonePriceString(Dictionary<string, object> zone)
    {
        if (zone == null) return "";

        object priceObj = null;
        if (zone.ContainsKey("price_second"))
            priceObj = zone["price_second"];
        else if (zone.ContainsKey("PriceSecond"))
            priceObj = zone["PriceSecond"];

        if (priceObj == null) return "";

        try
        {
            decimal price = Convert.ToDecimal(priceObj);
            return price.ToString("0.##");
        }
        catch
        {
            return priceObj.ToString();
        }
    }

    private bool GetZoneBool(Dictionary<string, object> zone, string key)
    {
        if (zone == null || !zone.ContainsKey(key) || zone[key] == null) return false;

        string v = zone[key].ToString().Trim().ToLowerInvariant();
        return v == "true" || v == "1" || v == "yes" || v == "on";
    }

    private string BuildSendLinkToPhoneUrl(string userId, string deviceGuid, string url)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);

        if (!string.IsNullOrEmpty(userId))
            qs["user_id"] = userId;

        if (!string.IsNullOrEmpty(deviceGuid))
            qs["deviceguid"] = deviceGuid;

        if (!string.IsNullOrEmpty(url))
            qs["url"] = url;

        qs["dontredirect"] = "true";

        return SEND_LINK_BASE + "?" + qs.ToString();
    }

    private void AppendWrappedText(StringBuilder sb, string text, int left, int top, int width, int fontSize, int lineHeight, int maxBottom)
    {
        if (string.IsNullOrEmpty(text)) return;

        List<string> lines = WrapText(text, 90);
        int y = top;

        foreach (string line in lines)
        {
            if (y + lineHeight > maxBottom) break;

            sb.AppendLine(string.Format(
                @"  <Text top=""{0}"" left=""{1}"" width=""{2}"" height=""{3}"" fontstyle=""Reg{4}"" foreground=""argb(255,230,230,230)"">{5}</Text>",
                y,
                left,
                width,
                lineHeight,
                fontSize,
                HttpUtility.HtmlEncode(line)));

            y += lineHeight;
        }
    }

    private List<string> WrapText(string text, int maxCharsPerLine)
    {
        List<string> result = new List<string>();
        if (string.IsNullOrEmpty(text)) return result;

        string[] paragraphs = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        foreach (string p in paragraphs)
        {
            string paragraph = Regex.Replace(p, @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(paragraph))
            {
                result.Add("");
                continue;
            }

            string[] words = paragraph.Split(' ');
            StringBuilder line = new StringBuilder();

            foreach (string word in words)
            {
                if (string.IsNullOrEmpty(word)) continue;

                if (line.Length == 0)
                {
                    line.Append(word);
                }
                else if (line.Length + 1 + word.Length <= maxCharsPerLine)
                {
                    line.Append(" ").Append(word);
                }
                else
                {
                    result.Add(line.ToString());
                    line.Clear();
                    line.Append(word);
                }
            }

            if (line.Length > 0)
                result.Add(line.ToString());
        }

        return result;
    }

    private string HtmlToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        string s = HttpUtility.HtmlDecode(html);

        s = Regex.Replace(s, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</\s*p\s*>", "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</\s*div\s*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</\s*li\s*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*li\b[^>]*>", "• ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*/?\s*(strong|b|em|i|span|u)\b[^>]*>", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "", RegexOptions.Singleline);

        return s.Trim();
    }

    private string NormalizeDescriptionText(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\r\n", "\n").Replace("\r", "\n");
        s = Regex.Replace(s, @"[ \t]+", " ");
        return s.Trim();
    }

    private string BuildProxyImageUrl(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return "";

        string absoluteUrl = imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? imageUrl
            : "https://kupikarta.com/" + imageUrl.TrimStart('/');

        return IMAGE_PROXY_BASE + HttpUtility.UrlEncode(absoluteUrl);
    }

    private string BuildCurrentPageUrl(string eventId, bool showFull, string userId, string deviceGuid)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["id"] = eventId;

        if (showFull)
            qs["showdesc"] = "1";

        if (!string.IsNullOrEmpty(userId))
        {
            qs["user_id"] = userId;
            qs["userid"] = userId;
            qs["me_id"] = userId;
        }

        if (!string.IsNullOrEmpty(deviceGuid))
            qs["deviceguid"] = deviceGuid;

        return PAGE_BASE_URL + "?" + qs.ToString();
    }

    private string GetQuery(string key)
    {
        string v = Request.QueryString[key];
        return string.IsNullOrEmpty(v) ? "" : HttpUtility.UrlDecode(v);
    }

    private bool GetBoolQuery(string key)
    {
        string v = GetQuery(key);
        if (string.IsNullOrEmpty(v)) return false;

        v = v.Trim().ToLowerInvariant();
        return v == "1" || v == "true" || v == "yes" || v == "on";
    }

    private string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || max <= 0) return "";
        if (s.Length <= max) return s;
        return s.Substring(0, max).TrimEnd() + "...";
    }
}