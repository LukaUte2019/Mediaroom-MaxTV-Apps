using System;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public partial class MenuOrSearchArtist : Page
{
    private const string PAGE_BASE_URL = "http://172.16.40.101/SETTEMediaroomApp/MenuOrSearchArtist.aspx";
    private const string SEARCH_ARTISTS_BASE_URL = "http://172.16.40.101/SETTEMediaroomApp/SearchArtists.aspx";
    private const string SEARCH_CHANNELS_BASE_URL = "http://172.16.40.101/SETTEMediaroomApp/SearchChannels.aspx";
    private const string VIEW_INSTAGRAM_URL = "http://172.16.40.101/SETTEMediaroomApp/ViewInstagramProfile.aspx";

    protected void Page_Load(object sender, EventArgs e)
    {
        string query = GetQuery("SearchSendTo");
        string videoChannel = GetQuery("video_channel");
        string meId = GetQuery("me_id");
        string userId = GetQuery("userid");
        string deviceGuid = GetQuery("DeviceGuid");

        if (string.IsNullOrEmpty(meId)) meId = "1003";
        if (string.IsNullOrEmpty(userId)) userId = "1003";
        if (string.IsNullOrEmpty(deviceGuid)) deviceGuid = "ea25707d-3414-439a-a70b-1c2b384d10c8";

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("<MrmlPage id=\"MenuSearchPage\" appid=\"lukatube.menu/1.0\" width=\"1280\" height=\"720\">");
        sb.AppendLine("<Panel>");

        sb.AppendLine("<Text top=\"40\" left=\"40\" width=\"1200\" height=\"40\" fontstyle=\"Reg30\" foreground=\"argb(255,255,255,255)\">Menu Or Search Artist</Text>");

        sb.AppendLine("<Button top=\"100\" left=\"40\" width=\"200\" height=\"50\" href=\"action:menu\">");
        sb.AppendLine("<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg22\" foreground=\"argb(255,255,255,255)\">Menu</Text>");
        sb.AppendLine("</Button>");

        string diagnosticsPage = "page:file:///Diagnostics.xml";
        sb.AppendLine("<Button top=\"100\" left=\"260\" width=\"200\" height=\"50\" href=\"" + HttpUtility.HtmlAttributeEncode(diagnosticsPage) + "\">");
        sb.AppendLine("<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg22\" foreground=\"argb(255,255,255,255)\">Diagnostics</Text>");
        sb.AppendLine("</Button>");

        int y = 180;

        if (!string.IsNullOrEmpty(query))
        {
            sb.AppendLine("<Text top=\"" + y + "\" left=\"40\" width=\"1200\" height=\"32\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">Results for: " + HttpUtility.HtmlEncode(query) + "</Text>");
            y += 60;

            List<string> artistNames = ExtractArtistNames(query);
            if (artistNames.Count == 0)
                artistNames.Add(query);

            foreach (string artist in artistNames)
            {
                if (y > 640)
                    break;

                string href;
                string label;

                if (IsInstagramUsername(artist))
                {
                    string cleanUser = artist.StartsWith("@") ? artist.Substring(1) : artist;
                    href = BuildInstagramUrl(artist, meId, userId, deviceGuid);
                    label = "View Instagram \"" + cleanUser + "\"";
                }
                else
                {
                    href = BuildSearchArtistsUrl(artist, meId, userId, deviceGuid);
                    label = "Search for Lukify Music artist \"" + artist + "\"";
                }

                AddButton(sb, href, label, y);
                y += 66;
            }

            if (!string.IsNullOrEmpty(videoChannel) && y <= 640)
            {
                string href = BuildSearchChannelUrl(videoChannel, meId, userId, deviceGuid);
                AddButton(sb, href, "Search LukaTube Channel \"" + videoChannel + "\"", y);
                y += 66;
            }
        }
        else if (!string.IsNullOrEmpty(videoChannel))
        {
            sb.AppendLine("<Text top=\"" + y + "\" left=\"40\" width=\"1200\" height=\"32\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">Search Channel: " + HttpUtility.HtmlEncode(videoChannel) + "</Text>");
            y += 60;

            string href = BuildSearchChannelUrl(videoChannel, meId, userId, deviceGuid);
            AddButton(sb, href, "Search LukaTube Channel \"" + videoChannel + "\"", y);
        }
        else
        {
            sb.AppendLine("<Text top=\"" + y + "\" left=\"40\" width=\"1200\" height=\"32\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">No search query provided.</Text>");
        }

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private void AddButton(StringBuilder sb, string href, string text, int top)
    {
        sb.AppendLine("<Button top=\"" + top + "\" left=\"40\" width=\"760\" height=\"56\" href=\"" + HttpUtility.HtmlAttributeEncode(href) + "\">");
        sb.AppendLine("<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg22\" foreground=\"argb(255,255,255,255)\">" + HttpUtility.HtmlEncode(text) + "</Text>");
        sb.AppendLine("</Button>");
    }

    private string BuildSearchChannelUrl(string channelName, string meId, string userId, string deviceGuid)
    {
        string url = SEARCH_CHANNELS_BASE_URL
            + "?channel=" + HttpUtility.UrlEncode(channelName)
            + "&me_id=" + HttpUtility.UrlEncode(meId)
            + "&userid=" + HttpUtility.UrlEncode(userId)
            + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

        return "page:" + url;
    }

    private string BuildSearchArtistsUrl(string artistName, string meId, string userId, string deviceGuid)
    {
        string url = SEARCH_ARTISTS_BASE_URL
            + "?me_id=" + HttpUtility.UrlEncode(meId)
            + "&userid=" + HttpUtility.UrlEncode(userId)
            + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid)
            + "&SearchSendTo=" + HttpUtility.UrlEncode(artistName);

        return "page:" + url;
    }

    private string BuildInstagramUrl(string username, string meId, string userId, string deviceGuid)
    {
        string url = VIEW_INSTAGRAM_URL
            + "?me_id=" + HttpUtility.UrlEncode(meId)
            + "&userid=" + HttpUtility.UrlEncode(userId)
            + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid)
            + "&username=" + HttpUtility.UrlEncode(username);

        return "page:" + url;
    }

    private List<string> ExtractArtistNames(string input)
    {
        List<string> results = new List<string>();
        if (string.IsNullOrEmpty(input)) return results;

        string text = HttpUtility.HtmlDecode(input).Trim();
        text = text.Replace("_", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        string[] separators = new string[] { " - ", " – ", " — " };
        int dashIndex = FindFirstSeparatorIndex(text, separators);
        if (dashIndex > 0)
            text = text.Substring(0, dashIndex).Trim();

        text = Regex.Replace(text, @"\s+(feat\.?|ft\.?|featuring)\s+", "|", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s+[xX]\s+", "|");
        text = Regex.Replace(text, @"\s*&\s*", "|");
        text = Regex.Replace(text, @"\s+and\s+", "|", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s*/\s*", "|");
        text = Regex.Replace(text, @"\s*\+\s*", "|");
        text = Regex.Replace(text, @"\s+vs\.?\s+", "|", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s*,\s*", "|");

        string[] parts = text.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            string name = TrimNoise(part);
            if (!string.IsNullOrEmpty(name) && !ContainsIgnoreCase(results, name))
                results.Add(name);
        }

        return results;
    }

    private int FindFirstSeparatorIndex(string text, string[] separators)
    {
        int best = -1;
        foreach (string sep in separators)
        {
            int idx = text.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && (best < 0 || idx < best))
                best = idx;
        }
        return best;
    }

    private string TrimNoise(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Trim();
        s = s.Trim('.', ',', ';', ':', '-', '"', '\'', '(', ')', '[', ']', '{', '}', ' ');
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    private bool ContainsIgnoreCase(List<string> list, string value)
    {
        foreach (string item in list)
        {
            if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private bool IsInstagramUsername(string artist)
    {
        if (string.IsNullOrEmpty(artist))
            return false;

        artist = artist.Trim();
        return Regex.IsMatch(artist, @"^@[A-Za-z0-9._]+$");
    }

    private string GetQuery(string key)
    {
        string v = Request.QueryString[key];
        return string.IsNullOrEmpty(v) ? "" : v;
    }
}