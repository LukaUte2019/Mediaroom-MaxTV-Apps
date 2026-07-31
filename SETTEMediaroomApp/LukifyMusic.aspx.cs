using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.UI;
using Newtonsoft.Json.Linq;

public partial class LukifyMusic : Page
{
    private const int SONGS_PER_PAGE = 5;
    private const int API_CACHE_SECONDS = 60;
    private const string API_CACHE_KEY = "LukifyMusic_API_MAIN";
    private const string APP_BASE_URL = "http://172.16.40.101/SETTEMediaroomApp/";
    private const string BUTTON_FOCUS_BG = "argb(255,0,170,80)";

    protected void Page_Load(object sender, EventArgs e)
    {
        string deviceGuid = Request.QueryString["DeviceGuid"];

        string section = (Request.QueryString["section"] ?? "home").Trim().ToLowerInvariant();
        if (section != "home" && section != "you" && section != "all")
            section = "home";

        string searchQuery = (Request.QueryString["search"] ?? Request.QueryString["SearchLukaTube"] ?? "").Trim();

        int pageIndex = 0;
        if (!string.IsNullOrEmpty(Request.QueryString["page"]))
            int.TryParse(Request.QueryString["page"], out pageIndex);

        if (pageIndex < 0)
            pageIndex = 0;

        bool forceRefresh = string.Equals(Request.QueryString["refresh"], "1", StringComparison.OrdinalIgnoreCase);

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        string apiUrl = "http://172.16.40.100/youtubeclone/radio/api.php?dir=" +
                        HttpUtility.UrlEncode("all songs yt downloaded mak hits instagram audios rnb and spotify songs") +
                        "&search=" + HttpUtility.UrlEncode(searchQuery);

        JObject data = GetApiDataCached(apiUrl, forceRefresh);

        List<SongInfo> homeRecommendations = GetSongsFromKey(data, "home_recommendations");
        List<SongInfo> mostPlayedByYou = GetSongsFromKey(data, "most_played_by_you");
        List<SongInfo> mostPlayedByAll = GetSongsFromKey(data, "most_played_by_all");
        List<SongInfo> searchResults = GetSongsFromKey(data, "search_results");

        List<SongInfo> currentSongs;
        if (!string.IsNullOrEmpty(searchQuery))
            currentSongs = searchResults ?? new List<SongInfo>();
        else
            currentSongs = GetCurrentSectionSongs(section, homeRecommendations, mostPlayedByYou, mostPlayedByAll);

        string sectionTitle = GetSectionTitle(section);
        if (!string.IsNullOrEmpty(searchQuery))
            sectionTitle = "Search Results for: " + searchQuery;

        int maxPage = 0;
        if (currentSongs.Count > 0)
            maxPage = (currentSongs.Count - 1) / SONGS_PER_PAGE;

        if (pageIndex > maxPage)
            pageIndex = maxPage;

        int startIndex = pageIndex * SONGS_PER_PAGE;
        int endIndex = Math.Min(startIndex + SONGS_PER_PAGE, currentSongs.Count);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("<MrmlPage id=\"LukifyMusicPage\" appid=\"lukatube.music/1.0\" width=\"1280\" height=\"720\">");
        sb.AppendLine("<Panel>");

        sb.AppendLine("<Text top=\"18\" left=\"40\" width=\"1200\" height=\"40\" fontstyle=\"Reg30\" foreground=\"argb(255,255,255,255)\">Lukify Music</Text>");

        sb.AppendLine("    <Actions>");
        sb.Append("      <Action name=\"SearchLukaTube\" type=\"submit\" data=\"SearchLukaTube\" url=\"page:");
        sb.Append(EscapeXml(BuildSearchActionUrl(section, pageIndex, deviceGuid)));
        sb.AppendLine("\" method=\"GET\" />");
        sb.AppendLine("    </Actions>");

        sb.AppendLine("    <EditText id=\"SearchLukaTube\" top=\"50\" left=\"20\" width=\"400\" height=\"40\" visible=\"true\" hint=\"" + EscapeXml("Search songs...") + "\">" + EscapeXml(searchQuery) + "</EditText>");
        sb.AppendLine("    <Button id=\"SearchButton\" top=\"50\" left=\"430\" width=\"160\" height=\"40\" justification=\"center\" backgroundFocus=\"" + BUTTON_FOCUS_BG + "\" focusScale=\"1.05\">");
        sb.AppendLine("      <Text top=\"8\" left=\"18\" fontstyle=\"Reg20\" foreground=\"argb(255,255,255,255)\">Search</Text>");
        sb.AppendLine("      <Actions><Event type=\"onclick\" action=\"SearchLukaTube\"/></Actions>");
        sb.AppendLine("    </Button>");

        sb.AppendLine(BuildSectionButton("Home Recommendations", "home", section, deviceGuid, 40, 100));
        sb.AppendLine(BuildSectionButton("Most Played By You", "you", section, deviceGuid, 300, 100));
        sb.AppendLine(BuildSectionButton("Most Played By All", "all", section, deviceGuid, 560, 100));

        sb.AppendLine(BuildRefreshButton(section, pageIndex, deviceGuid, searchQuery, 1040, 100));

        sb.AppendLine("<Text top=\"170\" left=\"40\" width=\"1200\" height=\"32\" fontstyle=\"Reg24\" foreground=\"argb(255,200,200,255)\">" +
                      HttpUtility.HtmlEncode(sectionTitle) + "</Text>");

        if (currentSongs.Count == 0)
        {
            sb.AppendLine("<Text top=\"215\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg24\" foreground=\"argb(255,255,80,80)\">No songs found</Text>");
        }
        else
        {
            sb.AppendLine("<Text top=\"200\" left=\"40\" width=\"1200\" height=\"26\" fontstyle=\"Reg16\" foreground=\"argb(255,180,180,180)\">Page " +
                          (pageIndex + 1) + " of " + (maxPage + 1) + "</Text>");

            int topPos = 235;

            for (int i = startIndex; i < endIndex; i++)
            {
                SongInfo song = currentSongs[i];

                string playSongUrl = BuildSongModeUrl(song, deviceGuid, i, section, pageIndex, searchQuery);
                string playVideoUrl = BuildVideoModeUrl(song, deviceGuid, section, pageIndex, searchQuery);

                string safeId = "btn_" + section + "_" + SanitizeId(song.title) + "_" + i;

                sb.AppendLine(
                    "<Button id=\"" + HttpUtility.HtmlAttributeEncode(safeId) + "\" top=\"" + topPos + "\" left=\"60\" width=\"930\" height=\"70\" href=\"" + HttpUtility.HtmlAttributeEncode(playSongUrl) + "\" backgroundFocus=\"" + BUTTON_FOCUS_BG + "\" focusScale=\"1.05\">" +
                        "<Image top=\"5\" left=\"5\" width=\"60\" height=\"60\" url=\"" + HttpUtility.HtmlAttributeEncode(song.cover ?? "") + "\"/>" +
                        "<Text top=\"5\" left=\"75\" alignment=\"left\" justification=\"left\" fontstyle=\"Reg20\" foreground=\"argb(255,255,255,255)\">" +
                            HttpUtility.HtmlEncode(song.title) +
                        "</Text>" +
                        "<Text top=\"28\" left=\"75\" alignment=\"left\" justification=\"left\" fontstyle=\"Reg16\" foreground=\"argb(255,200,200,200)\">" +
                            HttpUtility.HtmlEncode(song.artist) +
                        "</Text>" +
                    "</Button>"
                );

                sb.AppendLine(
                    "<Button id=\"" + HttpUtility.HtmlAttributeEncode(safeId + "_video") + "\" top=\"" + topPos + "\" left=\"1010\" width=\"190\" height=\"70\" href=\"" + HttpUtility.HtmlAttributeEncode(playVideoUrl) + "\" backgroundFocus=\"" + BUTTON_FOCUS_BG + "\" focusScale=\"1.05\">" +
                        "<Text top=\"22\" left=\"28\" fontstyle=\"Reg20\" foreground=\"argb(255,255,255,255)\">Play Video</Text>" +
                    "</Button>"
                );

                topPos += 75;
            }

            int navTop = 235 + (SONGS_PER_PAGE * 75) + 15;

            if (pageIndex < maxPage)
            {
                string loadMoreUrl = BuildSectionPageUrl(section, pageIndex + 1, deviceGuid, searchQuery, true);
                sb.AppendLine(
                    "<Button id=\"btnLoadMore\" top=\"" + navTop + "\" left=\"40\" width=\"220\" height=\"50\" href=\"" + HttpUtility.HtmlAttributeEncode(loadMoreUrl) + "\" backgroundFocus=\"" + BUTTON_FOCUS_BG + "\" focusScale=\"1.05\">" +
                        "<Text top=\"5\" left=\"18\" fontstyle=\"Reg20\" foreground=\"argb(255,255,255,255)\">Load More</Text>" +
                    "</Button>"
                );
            }

            if (pageIndex > 0)
            {
                string prevUrl = BuildSectionPageUrl(section, pageIndex - 1, deviceGuid, searchQuery, true);
                sb.AppendLine(
                    "<Button id=\"btnPrev\" top=\"" + navTop + "\" left=\"1020\" width=\"220\" height=\"50\" href=\"" + HttpUtility.HtmlAttributeEncode(prevUrl) + "\" backgroundFocus=\"" + BUTTON_FOCUS_BG + "\" focusScale=\"1.05\">" +
                        "<Text top=\"5\" left=\"18\" fontstyle=\"Reg20\" foreground=\"argb(255,255,255,255)\">Previous</Text>" +
                    "</Button>"
                );
            }
        }

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    private string BuildSearchActionUrl(string section, int pageIndex, string deviceGuid)
    {
        string url = BuildAbsolutePageUrl("LukifyMusic.aspx") +
                     "?section=" + HttpUtility.UrlEncode(section) +
                     "&page=" + pageIndex;

        if (!string.IsNullOrEmpty(deviceGuid))
            url += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

        return url;
    }

    private string BuildRefreshButton(string section, int pageIndex, string deviceGuid, string searchQuery, int left, int top)
    {
        string url = BuildSectionPageUrl(section, pageIndex, deviceGuid, searchQuery, true);
        url += "&refresh=1";

        return
            "<Button id=\"btnRefresh\" top=\"" + top + "\" left=\"" + left + "\" width=\"180\" height=\"45\" href=\"" + HttpUtility.HtmlAttributeEncode(url) + "\" backgroundFocus=\"" + BUTTON_FOCUS_BG + "\" focusScale=\"1.05\">" +
                "<Text top=\"8\" left=\"18\" fontstyle=\"Reg18\" foreground=\"argb(255,255,255,255)\">Refresh</Text>" +
            "</Button>";
    }

    private string BuildVideoModeUrl(SongInfo song, string deviceGuid, string section, int pageIndex, string searchQuery)
    {
        string hrefUrl = "page:" + BuildAbsolutePageUrl("GetVideoFromSong.aspx") + "?song_url=" +
                         HttpUtility.UrlEncode(song.file_url ?? "");

        if (!string.IsNullOrEmpty(deviceGuid))
            hrefUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

        if (!string.IsNullOrEmpty(section))
            hrefUrl += "&section=" + HttpUtility.UrlEncode(section);

        hrefUrl += "&page=" + pageIndex;

        if (!string.IsNullOrEmpty(searchQuery))
            hrefUrl += "&search=" + HttpUtility.UrlEncode(searchQuery);

        return hrefUrl;
    }

    private string BuildSongModeUrl(SongInfo song, string deviceGuid, int index, string section, int pageIndex, string searchQuery)
    {
        string hrefUrl = "page:" + BuildAbsolutePageUrl("PlayLukifyMusicSong.aspx") + "?song_url=" +
                         HttpUtility.UrlEncode(song.file_url ?? "") +
                         "&title=" + HttpUtility.UrlEncode(song.title ?? "") +
                         "&artist=" + HttpUtility.UrlEncode(song.artist ?? "") +
                         "&cover=" + HttpUtility.UrlEncode(song.cover ?? "") +
                         "&index=" + index;

        if (!string.IsNullOrEmpty(deviceGuid))
            hrefUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

        if (!string.IsNullOrEmpty(section))
            hrefUrl += "&section=" + HttpUtility.UrlEncode(section);

        hrefUrl += "&page=" + pageIndex;

        if (!string.IsNullOrEmpty(searchQuery))
            hrefUrl += "&search=" + HttpUtility.UrlEncode(searchQuery);

        return hrefUrl;
    }

    private string BuildSectionButton(string title, string sectionValue, string currentSection, string deviceGuid, int left, int top)
    {
        string url = BuildSectionPageUrl(sectionValue, 0, deviceGuid, "", false);

        string color = "argb(255,200,200,200)";
        if (sectionValue == currentSection)
            color = "argb(255,255,255,255)";

        return
            "<Button id=\"btnSection_" + sectionValue + "\" top=\"" + top + "\" left=\"" + left + "\" width=\"240\" height=\"45\" href=\"" + HttpUtility.HtmlAttributeEncode(url) + "\" backgroundFocus=\"" + BUTTON_FOCUS_BG + "\" focusScale=\"1.05\">" +
                "<Text top=\"8\" left=\"14\" fontstyle=\"Reg18\" foreground=\"" + color + "\">" +
                    HttpUtility.HtmlEncode(title) +
                "</Text>" +
            "</Button>";
    }

    private string BuildSectionPageUrl(string section, int pageIndex, string deviceGuid, string searchQuery, bool includeSearch)
    {
        string url = BuildAbsolutePageUrl("LukifyMusic.aspx") +
                     "?section=" + HttpUtility.UrlEncode(section) +
                     "&page=" + pageIndex;

        if (!string.IsNullOrEmpty(deviceGuid))
            url += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

        if (includeSearch && !string.IsNullOrEmpty(searchQuery))
            url += "&search=" + HttpUtility.UrlEncode(searchQuery);

        return url;
    }

    private string BuildAbsolutePageUrl(string pageName)
    {
        return APP_BASE_URL + pageName;
    }

    private string GetSectionTitle(string section)
    {
        switch (section)
        {
            case "you":
                return "Most Played By You";
            case "all":
                return "Most Played By All";
            default:
                return "Home Recommendations";
        }
    }

    private List<SongInfo> GetCurrentSectionSongs(string section, List<SongInfo> home, List<SongInfo> you, List<SongInfo> all)
    {
        switch (section)
        {
            case "you":
                return you ?? new List<SongInfo>();
            case "all":
                return all ?? new List<SongInfo>();
            default:
                return home ?? new List<SongInfo>();
        }
    }

    private JObject GetApiDataCached(string url, bool forceRefresh)
    {
        string cacheKey = API_CACHE_KEY + "|" + url;

        if (forceRefresh)
            HttpRuntime.Cache.Remove(cacheKey);

        object cached = HttpRuntime.Cache[cacheKey];
        JObject cachedData = cached as JObject;
        if (cachedData != null)
            return cachedData;

        JObject freshData = GetApiData(url);

        HttpRuntime.Cache.Insert(
            cacheKey,
            freshData,
            null,
            DateTime.UtcNow.AddSeconds(API_CACHE_SECONDS),
            Cache.NoSlidingExpiration
        );

        return freshData;
    }

    private JObject GetApiData(string url)
    {
        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 10000;
            req.UserAgent = "LukifyMusic/1.0";

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string body = sr.ReadToEnd();
                return JObject.Parse(body);
            }
        }
        catch
        {
            return new JObject();
        }
    }

    private List<SongInfo> GetSongsFromKey(JObject data, string key)
    {
        List<SongInfo> result = new List<SongInfo>();

        if (data == null || data[key] == null)
            return result;

        JArray arr = data[key] as JArray;
        if (arr == null)
            return result;

        foreach (var s in arr)
        {
            SongInfo song = new SongInfo();

            song.title = GetString(s, "title");
            if (string.IsNullOrEmpty(song.title))
                song.title = GetString(s, "filename");

            song.artist = GetString(s, "display_artist");
            if (string.IsNullOrEmpty(song.artist))
                song.artist = GetString(s, "artist");

            song.file_url = GetString(s, "song_url");
            if (string.IsNullOrEmpty(song.file_url))
                song.file_url = GetString(s, "file_url");

            song.cover = GetString(s, "cover_artwork_uri");
            if (string.IsNullOrEmpty(song.cover))
                song.cover = GetString(s, "cover");

            if (!string.IsNullOrEmpty(song.cover))
                song.cover = song.cover.Replace("lukaserver.ddns.net", "172.16.40.100");

            if (!string.IsNullOrEmpty(song.file_url))
                song.file_url = song.file_url.Replace("lukaserver.ddns.net", "172.16.40.100");

            result.Add(song);
        }

        return result;
    }

    private string GetString(JToken token, string key)
    {
        try
        {
            if (token == null || token[key] == null)
                return "";
            return token[key].ToString();
        }
        catch
        {
            return "";
        }
    }

    private string SanitizeId(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "unknown";

        StringBuilder sb = new StringBuilder();
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else
                sb.Append('_');
        }

        string value = sb.ToString();
        return value.Length <= 60 ? value : value.Substring(0, 60);
    }

    private string EscapeXml(string s)
    {
        if (s == null)
            return "";
        return System.Security.SecurityElement.Escape(s);
    }

    private class SongInfo
    {
        public string title;
        public string artist;
        public string file_url;
        public string cover;
    }
}