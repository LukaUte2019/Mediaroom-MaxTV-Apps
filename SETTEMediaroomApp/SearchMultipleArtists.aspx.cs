using System;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public partial class SearchMultipleArtists : Page
{
    private const string SEARCH_ARTISTS_BASE_URL = "http://172.16.40.101/SETTEMediaroomApp/SearchArtists.aspx";
    private const string VIEW_INSTAGRAM_URL = "http://172.16.40.101/SETTEMediaroomApp/ViewInstagramProfile.aspx";

    protected void Page_Load(object sender, EventArgs e)
    {
        string query = GetQuery("SearchSendTo");
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
        sb.AppendLine("<MrmlPage id=\"SearchMultipleArtistsPage\" appid=\"lukatube.menu/1.0\" width=\"1280\" height=\"720\">");
        sb.AppendLine("<Panel>");

        // Page title
        sb.AppendLine("<Text top=\"40\" left=\"40\" width=\"1200\" height=\"40\" fontstyle=\"Reg30\" foreground=\"argb(255,255,255,255)\">Search Artists</Text>");

        int y = 100;

        if (!string.IsNullOrEmpty(query))
        {
            sb.AppendLine("<Text top=\"" + y + "\" left=\"40\" width=\"1200\" height=\"32\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">Results for: " + HttpUtility.HtmlEncode(query) + "</Text>");
            y += 60;

            List<string> artistNames = ParseArtists(query);

            foreach (string artist in artistNames)
            {
                string href;
                string displayName = artist;

                // Handle Instagram usernames starting with @
                if (artist.Contains("@"))
                {
                    displayName = CleanInstagramUsername(artist);
                    href = BuildInstagramUrl(displayName, meId, userId, deviceGuid);
                    sb.AppendLine("<Button top=\"" + y + "\" left=\"40\" width=\"760\" height=\"56\" href=\"page:" + HttpUtility.HtmlAttributeEncode(href) + "\">");
                    sb.AppendLine("<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg22\" foreground=\"argb(255,255,255,255)\">View Instagram \"" + HttpUtility.HtmlEncode(displayName) + "\"</Text></Button>");
                }
                else
                {
                    href = BuildSearchArtistsUrl(artist, meId, userId, deviceGuid);
                    sb.AppendLine("<Button top=\"" + y + "\" left=\"40\" width=\"760\" height=\"56\" href=\"page:" + HttpUtility.HtmlAttributeEncode(href) + "\">");
                    sb.AppendLine("<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg22\" foreground=\"argb(255,255,255,255)\">Search for \"" + HttpUtility.HtmlEncode(displayName) + "\"</Text></Button>");
                }

                y += 66;
                if (y > 660) break;
            }
        }

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    private string BuildSearchArtistsUrl(string artistName, string meId, string userId, string deviceGuid)
    {
        return SEARCH_ARTISTS_BASE_URL
            + "?me_id=" + HttpUtility.UrlEncode(meId)
            + "&userid=" + HttpUtility.UrlEncode(userId)
            + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid)
            + "&SearchSendTo=" + HttpUtility.UrlEncode(artistName);
    }

    private string BuildInstagramUrl(string username, string meId, string userId, string deviceGuid)
    {
        return VIEW_INSTAGRAM_URL
            + "?me_id=" + HttpUtility.UrlEncode(meId)
            + "&userid=" + HttpUtility.UrlEncode(userId)
            + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid)
            + "&username=" + HttpUtility.UrlEncode(username);
    }

    private List<string> ParseArtists(string input)
    {
        List<string> results = new List<string>();
        if (string.IsNullOrEmpty(input)) return results;

        string text = HttpUtility.HtmlDecode(input).Trim();
        text = text.Replace("_", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        // Replace all separators with |
        string pattern = @"\s*(ft\.?|feat\.?|featuring|&|\+|,|/|vs\.?|and|x)\s*"; // case-insensitive
        string[] parts = Regex.Split(text, pattern, RegexOptions.IgnoreCase);

        foreach (string part in parts)
        {
            string name = CleanArtistName(part);
            if (!string.IsNullOrEmpty(name) && !results.Exists(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(name);
            }
        }

        return results;
    }

    private string CleanArtistName(string name)
    {
        name = name.Trim();
        name = name.Trim('.', ',', ';', ':', '-', '"', '\'', ' ');
        name = Regex.Replace(name, @"\s+", " ");
        return name;
    }

    private string CleanInstagramUsername(string username)
    {
        int atIndex = username.IndexOf('@');
        if (atIndex >= 0 && atIndex + 1 < username.Length)
            return username.Substring(atIndex + 1).Trim();
        return username.Trim();
    }

    private string GetQuery(string key)
    {
        string v = Request.QueryString[key];
        return string.IsNullOrEmpty(v) ? "" : HttpUtility.UrlDecode(v);
    }
}