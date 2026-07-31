using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;

public partial class SearchArtists : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string meId = Request.QueryString["me_id"];
        string deviceGuid = Request.QueryString["DeviceGuid"];

        string rawSearchOrig = (GetSearchFromRequest() ?? "").Trim();
        string rawSearch = rawSearchOrig.ToLowerInvariant();

        string pageStr = Request.QueryString["page"];
        string debugStr = Request.QueryString["debug"];
        bool debug = !string.IsNullOrEmpty(debugStr) && debugStr == "1";

        string[] searchSendToValues = Request.QueryString.GetValues("SearchSendTo");
        if (searchSendToValues == null) searchSendToValues = new string[0];

        string finalSearchSendTo = rawSearch;
        for (int i = searchSendToValues.Length - 1; i >= 0; i--)
        {
            if (!string.IsNullOrEmpty(searchSendToValues[i]))
            {
                finalSearchSendTo = searchSendToValues[i].ToLowerInvariant();
                break;
            }
        }

        string displaySearch = !string.IsNullOrEmpty(rawSearchOrig) ? rawSearchOrig : finalSearchSendTo;

        string apiDebug;
        List<ArtistInfo> artists = GetArtists(finalSearchSendTo, meId, debug, out apiDebug);

        int page = 1;
        int pageSize = 8;
        int pageParsed;
        if (int.TryParse(pageStr, out pageParsed) && pageParsed > 0)
            page = pageParsed;

        int totalArtists = artists.Count;
        int startIndex = (page - 1) * pageSize;
        int count = Math.Min(pageSize, totalArtists - startIndex);

        List<ArtistInfo> pagedArtists = new List<ArtistInfo>();
        if (count > 0)
            pagedArtists = artists.GetRange(startIndex, count);

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"<MrmlPage id=""SearchArtistsList"" appid=""lukatube.artists/1.0"" width=""1280"" height=""720"">");

        string submitBaseHttp = "http://172.16.40.101/SETTEMediaroomApp/SearchArtists.aspx";
        var submitQs = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrEmpty(meId)) submitQs["me_id"] = meId;
        if (!string.IsNullOrEmpty(deviceGuid)) submitQs["DeviceGuid"] = deviceGuid;

        string submitQueryPart = submitQs.ToString();
        string submitUrlFull = submitBaseHttp + (submitQueryPart.Length > 0 ? "?" + submitQueryPart : "");
        string submitPageUrl = "page:" + submitUrlFull;

        sb.AppendLine("<Actions>");
        sb.AppendLine(string.Format(
            "<Action name=\"SearchArtistsAction\" type=\"submit\" data=\"SearchSendTo\" method=\"GET\" url=\"{0}\" />",
            HttpUtility.HtmlAttributeEncode(submitPageUrl)
        ));
        sb.AppendLine("</Actions>");

        sb.AppendLine("<Panel>");
        sb.AppendLine("<Text top=\"20\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">Lukify Music Artist Search</Text>");
        sb.AppendLine("<Text top=\"70\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg24\" foreground=\"argb(255,200,200,200)\">Search Artists</Text>");

        sb.AppendLine(string.Format(
            "<EditText id=\"SearchSendTo\" name=\"SearchSendTo\" top=\"110\" left=\"40\" width=\"900\" height=\"48\" fontstyle=\"Reg24\">{0}</EditText>",
            HttpUtility.HtmlEncode(displaySearch)
        ));

        sb.AppendLine(
            "<Button id=\"btnSearch\" top=\"110\" left=\"960\" width=\"280\" height=\"48\">" +
              "<Actions><Event type=\"onclick\" action=\"SearchArtistsAction\" /></Actions>" +
              "<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">Search Lukify Music Artist</Text>" +
            "</Button>"
        );

        string searchChannelHref = BuildSearchChannelsUrl(displaySearch, deviceGuid);
        sb.AppendLine(
            "<Button id=\"btnSearchChannels\" top=\"170\" left=\"960\" width=\"280\" height=\"48\" href=\"" +
            HttpUtility.HtmlAttributeEncode(searchChannelHref) + "\">" +
              "<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">Search LukaTube Channels</Text>" +
            "</Button>"
        );

        string instagramUsernameForSearch = GetArtistUsernameFromApi(displaySearch);
        bool showInstagramButton =
            !IsAllDigits(displaySearch) &&
            !string.IsNullOrWhiteSpace(instagramUsernameForSearch) &&
            !IsAllDigits(instagramUsernameForSearch);

        if (showInstagramButton)
        {
            string instagramSearchUrl = BuildInstagramProfileUrl(
                displaySearch,
                !string.IsNullOrEmpty(meId) ? meId : "1003"
            );

            string instagramButtonText = "@" + instagramUsernameForSearch.Trim() + "'s Instagram Profile";

            sb.AppendLine(
                "<Button id=\"btnSearchInstagram\" top=\"230\" left=\"960\" width=\"280\" height=\"48\" href=\"" +
                HttpUtility.HtmlAttributeEncode(instagramSearchUrl) + "\">" +
                  "<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">" +
                  HttpUtility.HtmlEncode(instagramButtonText) +
                  "</Text>" +
                "</Button>"
            );
        }

        int resultsHeaderTop = 160;
        sb.AppendLine(string.Format(
            "<Text top=\"{0}\" left=\"40\" width=\"400\" height=\"28\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">Found {1} artist{2}</Text>",
            resultsHeaderTop,
            totalArtists,
            totalArtists == 1 ? "" : "s"
        ));

        int searchButtonTop = resultsHeaderTop + 40;
        string typedArtistForHref = displaySearch ?? "";
        string viewArtistHref = BuildViewArtistPageUrl(typedArtistForHref, deviceGuid);
        string viewEventsHref = BuildViewEventsPageUrl(typedArtistForHref, deviceGuid);

        if (totalArtists > 0)
        {
            sb.AppendLine(
                "<Button id=\"btnSearchForTyped\" top=\"" + searchButtonTop + "\" left=\"40\" width=\"1200\" height=\"60\" href=\"" +
                HttpUtility.HtmlAttributeEncode(viewArtistHref) + "\">" +
                    "<Text alignment=\"left\" justification=\"left\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">" +
                    HttpUtility.HtmlEncode(string.IsNullOrEmpty(displaySearch) ? "Search" : ("Search Lukify Music Songs of Artist for \"" + displaySearch + "\"")) +
                    "</Text>" +
                "</Button>"
            );

            sb.AppendLine(
                "<Button id=\"btnSearchEventsForTyped\" top=\"" + (searchButtonTop + 64) + "\" left=\"40\" width=\"1200\" height=\"52\" href=\"" +
                HttpUtility.HtmlAttributeEncode(viewEventsHref) + "\">" +
                    "<Text alignment=\"left\" justification=\"left\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">View music events for \"" +
                    HttpUtility.HtmlEncode(displaySearch) +
                    "\"</Text>" +
                "</Button>"
            );
        }

        int topPos = searchButtonTop + 130;

        if (pagedArtists.Count == 0)
        {
            sb.AppendLine("<Text top=\"" + topPos + "\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg28\" foreground=\"argb(255,255,60,60)\">No artists found</Text>");
        }
        else
        {
            foreach (var a in pagedArtists)
            {
                string artistName = a.name ?? "";
                string artistViewUrl = BuildViewArtistPageUrl(artistName, deviceGuid);
                string artistEventsUrl = BuildViewEventsPageUrl(artistName, deviceGuid);
                string artistInstagramUrl = BuildInstagramProfileUrl(artistName, !string.IsNullOrEmpty(meId) ? meId : "1003");

                string safeId = "btn_" + SanitizeId(artistName);
                string safeEventsId = "btn_events_" + SanitizeId(artistName);
                string safeInstagramId = "btn_ig_" + SanitizeId(artistName);

                sb.AppendLine(
                    "<Button id=\"" + HttpUtility.HtmlAttributeEncode(safeId) + "\" top=\"" + topPos + "\" left=\"40\" width=\"500\" height=\"60\" href=\"" +
                    HttpUtility.HtmlAttributeEncode(artistViewUrl) + "\">" +
                        "<Text top=\"5\" left=\"40\" alignment=\"left\" justification=\"left\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">" +
                        HttpUtility.HtmlEncode(artistName) +
                        "</Text>" +
                    "</Button>"
                );

                sb.AppendLine(
                    "<Button id=\"" + HttpUtility.HtmlAttributeEncode(safeEventsId) + "\" top=\"" + topPos + "\" left=\"560\" width=\"220\" height=\"60\" href=\"" +
                    HttpUtility.HtmlAttributeEncode(artistEventsUrl) + "\">" +
                        "<Text top=\"5\" left=\"0\" width=\"220\" alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">View Events</Text>" +
                    "</Button>"
                );

                sb.AppendLine(
                    "<Button id=\"" + HttpUtility.HtmlAttributeEncode(safeInstagramId) + "\" top=\"" + topPos + "\" left=\"800\" width=\"400\" height=\"60\" href=\"" +
                    HttpUtility.HtmlAttributeEncode(artistInstagramUrl) + "\">" +
                        "<Text top=\"5\" left=\"0\" width=\"400\" alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">View Instagram Profile</Text>" +
                    "</Button>"
                );

                topPos += 70;
            }
        }

        if (totalArtists > page * pageSize)
        {
            string nextUrl = submitBaseHttp + "?";
            if (!string.IsNullOrEmpty(meId))
                nextUrl += "me_id=" + HttpUtility.UrlEncode(meId) + "&";
            if (!string.IsNullOrEmpty(deviceGuid))
                nextUrl += "DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid) + "&";
            if (!string.IsNullOrEmpty(displaySearch))
                nextUrl += "SearchSendTo=" + HttpUtility.UrlEncode(displaySearch) + "&";
            nextUrl += "page=" + (page + 1);

            sb.AppendLine(
                "<Button id=\"btnMore\" top=\"" + (topPos + 10) + "\" left=\"40\" width=\"1200\" height=\"56\" href=\"page:" +
                HttpUtility.HtmlAttributeEncode(nextUrl) + "\">" +
                    "<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg26\" foreground=\"argb(255,255,255,255)\">Load More</Text>" +
                "</Button>"
            );
        }

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    private bool IsAllDigits(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return true;

        foreach (char c in input.Trim())
        {
            if (!char.IsDigit(c))
                return false;
        }
        return true;
    }

    private string BuildViewArtistPageUrl(string artistName, string deviceGuid)
    {
        string url = "page:http://172.16.40.101/SETTEMediaroomApp/ViewArtist.aspx?artist=" + HttpUtility.UrlEncode(artistName ?? "");
        if (!string.IsNullOrEmpty(deviceGuid))
            url += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);
        return url;
    }

    private string BuildViewEventsPageUrl(string artistName, string deviceGuid)
    {
        string url = "page:http://172.16.40.101/SETTEMediaroomApp/Events.aspx?q=" + HttpUtility.UrlEncode(artistName ?? "");
        if (!string.IsNullOrEmpty(deviceGuid))
            url += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);
        return url;
    }

    private string BuildSearchChannelsUrl(string channelName, string deviceGuid)
    {
        string url = "page:http://172.16.40.101/SETTEMediaroomApp/SearchChannels.aspx?channel=" + HttpUtility.UrlEncode(channelName ?? "");
        if (!string.IsNullOrEmpty(deviceGuid))
            url += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);
        return url;
    }

    private string BuildInstagramProfileUrl(string artistName, string selectedUserId)
    {
        string url = "page:http://172.16.40.101/SETTEMediaroomApp/ViewInstagramProfile.aspx?artist="
            + HttpUtility.UrlEncode(artistName ?? "");

        if (!string.IsNullOrEmpty(selectedUserId))
            url += "&selected_user_id=" + HttpUtility.UrlEncode(selectedUserId);

        return url;
    }

    private string GetArtistUsernameFromApi(string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return "";

        string url = "http://172.16.40.100/get_username_from_artist.php?artist="
            + HttpUtility.UrlEncode(artistName);

        try
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 10000;
            req.UserAgent = "LukaTube/1.0";
            req.Accept = "application/json";

            string respBody;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                respBody = sr.ReadToEnd();
            }

            var j = JObject.Parse(respBody);
            string username = j["username"] != null ? j["username"].ToString() : "";

            if (!string.IsNullOrWhiteSpace(username))
                return username.Trim();
        }
        catch
        {
        }

        return "";
    }

    private List<ArtistInfo> GetArtists(string search, string meId, bool debug, out string apiDebug)
    {
        apiDebug = null;
        List<ArtistInfo> result = new List<ArtistInfo>();
        string searchSafe = (search ?? "").Trim();

        string url = "http://172.16.40.100/search_users.php?search=" + HttpUtility.UrlEncode(searchSafe);
        if (!string.IsNullOrEmpty(meId))
            url += "&me_id=" + HttpUtility.UrlEncode(meId);

        try
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 10000;
            req.UserAgent = "LukaTube/1.0";

            string respBody;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                respBody = sr.ReadToEnd();
            }

            if (debug) apiDebug = url + "\r\n" + respBody;

            var j = JObject.Parse(respBody);
            JToken resultsToken = j["results"];
            JToken artistsToken = null;

            if (resultsToken != null && resultsToken["artists"] != null)
                artistsToken = resultsToken["artists"];

            if (artistsToken != null && artistsToken.Type == JTokenType.Array)
            {
                JArray artistsArray = (JArray)artistsToken;
                foreach (var it in artistsArray)
                {
                    var artist = new ArtistInfo();
                    if (it != null) artist.name = it.ToString();
                    result.Add(artist);
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private string GetSearchFromRequest()
    {
        string[] keys = new[] { "SearchSendTo", "search", "query", "q", "txtQuery" };
        foreach (var k in keys)
        {
            string v = Request.QueryString[k];
            if (!string.IsNullOrEmpty(v))
                return HttpUtility.UrlDecode(v);

            if (Request.Form != null)
            {
                string fv = Request.Form[k];
                if (!string.IsNullOrEmpty(fv))
                    return HttpUtility.HtmlDecode(fv);
            }
        }
        return "";
    }

    private string SanitizeId(string input)
    {
        if (string.IsNullOrEmpty(input)) return "unknown";
        StringBuilder sb = new StringBuilder();
        foreach (char c in input)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                sb.Append(c);
            else
                sb.Append('_');
        }
        string s = sb.ToString();
        return s.Length <= 60 ? s : s.Substring(0, 60);
    }

    private class ArtistInfo
    {
        public string name { get; set; }
    }
}