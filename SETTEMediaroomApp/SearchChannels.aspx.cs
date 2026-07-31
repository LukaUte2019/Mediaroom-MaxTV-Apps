using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;

public partial class SearchChannels : Page
{
    private const string PAGE_BASE_URL = "http://172.16.40.101/SETTEMediaroomApp/SearchChannels.aspx";
    private const string API_BASE_URL = "http://172.16.40.100/youtubeclone/get_artists_from_videos.php";
    private const string PLAY_VIDEO_URL = "http://172.16.40.101/SETTEMediaroomApp/PlayVideo.aspx";
    private const string VIDEO_BASE_PATH = "http://172.16.40.100/youtubeclone/videos_mediaroom/";

    protected void Page_Load(object sender, EventArgs e)
    {
        string meId = Request.QueryString["me_id"] ?? "";
        string deviceGuid = Request.QueryString["DeviceGuid"] ?? "";

        string action = Request.QueryString["action"] ?? "";
        string rawSearchOrig = Request.QueryString["channel"] ?? "";
        rawSearchOrig = rawSearchOrig.Trim();

        string viewVideos = Request.QueryString["viewvideos"] ?? "0";
        bool isVideoView = (viewVideos == "1" && action != "back");

        string artistId = Request.QueryString["artist"] ?? "";
        string artistName = Request.QueryString["artist_name"] ?? "";

        
        int page = 1;
        int pageSize = 7;
        int pageParsed;
        if (int.TryParse(Request.QueryString["page"], out pageParsed) && pageParsed > 0)
            page = pageParsed;

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("<MrmlPage id=\"SearchChannelsList\" appid=\"lukatube.channels/1.0\" width=\"1280\" height=\"720\">");
        sb.AppendLine("<Panel>");

        if (!isVideoView)
        {
            sb.AppendLine("<Text top=\"20\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">Search Channels</Text>");
            sb.AppendLine("<Text top=\"70\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg24\" foreground=\"argb(255,200,200,200)\">Search LukaTube channels</Text>");

            sb.AppendLine(
                "<EditText id=\"SearchSendTo\" name=\"SearchSendTo\" top=\"110\" left=\"40\" width=\"900\" height=\"48\" fontstyle=\"Reg24\">" +
                HttpUtility.HtmlEncode(rawSearchOrig) +
                "</EditText>"
            );

            string searchUrl = PAGE_BASE_URL
                + "?channel=" + HttpUtility.UrlEncode(rawSearchOrig)
                + "&page=1"
                + "&me_id=" + HttpUtility.UrlEncode(meId)
                + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

            sb.AppendLine(
                "<Button id=\"btnSearch\" top=\"110\" left=\"960\" width=\"280\" height=\"48\" href=\"page:" +
                HttpUtility.HtmlAttributeEncode(searchUrl) + "\">" +
                "<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">Search</Text>" +
                "</Button>"
            );

            List<ChannelInfo> channels = GetChannelsFromAPI(rawSearchOrig);

            int totalChannels = channels.Count;
            int startIndex = (page - 1) * pageSize;
            int count = Math.Min(pageSize, Math.Max(0, totalChannels - startIndex));

            List<ChannelInfo> pagedChannels = new List<ChannelInfo>();
            if (count > 0)
                pagedChannels = channels.GetRange(startIndex, count);

            int topPos = 160;

            if (pagedChannels.Count == 0)
            {
                sb.AppendLine("<Text top=\"" + topPos + "\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg28\" foreground=\"argb(255,255,60,60)\">No channels found</Text>");
            }
            else
            {
                for (int i = 0; i < pagedChannels.Count; i++)
                {
                    ChannelInfo c = pagedChannels[i];
                    string safeId = "btn_" + SanitizeId(c.name);
                    string safeVideosId = "btn_videos_" + SanitizeId(c.name);

                    string videosUrl = PAGE_BASE_URL
                        + "?viewvideos=1"
                        + "&artist=" + HttpUtility.UrlEncode(c.id)
                        + "&artist_name=" + HttpUtility.UrlEncode(c.name)
                        + "&channel=" + HttpUtility.UrlEncode(rawSearchOrig)
                        + "&me_id=" + HttpUtility.UrlEncode(meId)
                        + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid)
                        + "&page=1";

                    sb.AppendLine(
                        "<Button id=\"" + HttpUtility.HtmlAttributeEncode(safeId) + "\" top=\"" + topPos + "\" left=\"40\" width=\"760\" height=\"60\" href=\"page:" +
                        HttpUtility.HtmlAttributeEncode(videosUrl) + "\">" +
                        "<Text top=\"5\" left=\"40\" alignment=\"left\" justification=\"left\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">" +
                        HttpUtility.HtmlEncode(c.name) +
                        "</Text>" +
                        "</Button>"
                    );

                    sb.AppendLine(
                        "<Button id=\"" + HttpUtility.HtmlAttributeEncode(safeVideosId) + "\" top=\"" + topPos + "\" left=\"820\" width=\"440\" height=\"60\" href=\"page:" +
                        HttpUtility.HtmlAttributeEncode(videosUrl) + "\">" +
                        "<Text top=\"5\" left=\"0\" width=\"440\" alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">View Videos</Text>" +
                        "</Button>"
                    );

                    topPos += 70;
                }
            }

            if (totalChannels > page * pageSize)
            {
                string nextUrl = PAGE_BASE_URL
                    + "?channel=" + HttpUtility.UrlEncode(rawSearchOrig)
                    + "&page=" + (page + 1)
                    + "&me_id=" + HttpUtility.UrlEncode(meId)
                    + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

                sb.AppendLine(
                    "<Button id=\"btnMore\" top=\"" + (topPos + 10) + "\" left=\"40\" width=\"1200\" height=\"56\" href=\"page:" +
                    HttpUtility.HtmlAttributeEncode(nextUrl) + "\">" +
                    "<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg26\" foreground=\"argb(255,255,255,255)\">Load More</Text>" +
                    "</Button>"
                );
            }
        }
        else
        {
            List<VideoInfo> videos = GetArtistVideos(artistId);
            videos.Sort(delegate (VideoInfo a, VideoInfo b)
            {
                return b.modified.CompareTo(a.modified);
            });

            string titleName = string.IsNullOrEmpty(artistName) ? "Artist Videos" : artistName;
            sb.AppendLine("<Text top=\"20\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">" + HttpUtility.HtmlEncode(titleName) + "</Text>");
            sb.AppendLine("<Text top=\"70\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg24\" foreground=\"argb(255,200,200,200)\">Videos</Text>");

            string backUrl = PAGE_BASE_URL
                + "?action=back"
                + "&channel=" + HttpUtility.UrlEncode(Request.QueryString["channel"] ?? "")
                + "&page=1"
                + "&me_id=" + HttpUtility.UrlEncode(meId)
                + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

            sb.AppendLine(
                "<Button id=\"btnBack\" top=\"110\" left=\"40\" width=\"180\" height=\"48\" href=\"page:" +
                HttpUtility.HtmlAttributeEncode(backUrl) + "\">" +
                "<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">Back</Text>" +
                "</Button>"
            );

            int totalVideos = videos.Count;
            int startIndex = (page - 1) * pageSize;
            int count = Math.Min(pageSize, Math.Max(0, totalVideos - startIndex));

            List<VideoInfo> pagedVideos = new List<VideoInfo>();
            if (count > 0)
                pagedVideos = videos.GetRange(startIndex, count);

            int topPos = 180;

            if (pagedVideos.Count == 0)
            {
                sb.AppendLine("<Text top=\"" + topPos + "\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg28\" foreground=\"argb(255,255,60,60)\">No videos found</Text>");
            }
            else
            {
                for (int i = 0; i < pagedVideos.Count; i++)
                {
                    VideoInfo v = pagedVideos[i];

string videoNameNoExt = GetDisplayVideoName(v.file);

string fullVideoUrl = BuildFullVideoUrl(v.file);

// Replace spaces with underscores, keep other characters for URL encoding
string safeVideoName = videoNameNoExt.Replace(' ', '_');
string channelName = !string.IsNullOrEmpty(v.artist) ? v.artist : artistName;

string playUrl = PLAY_VIDEO_URL
    + "?video_url=" + HttpUtility.UrlEncode(fullVideoUrl)
    + "&video_name=" + HttpUtility.UrlEncode(safeVideoName)
    + "&artist=" + HttpUtility.UrlEncode(artistId)
   + "&channel_name=" + HttpUtility.UrlEncode(channelName)
    + "&artist_name=" + HttpUtility.UrlEncode(artistName)  // <--- URL encode this
    + "&me_id=" + HttpUtility.UrlEncode(meId)
    + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);
                    string agoText = GetRelativeTimeText(v.modified);

                    sb.AppendLine(
                        "<Button id=\"btn_video_" + SanitizeId(v.file) + "\" top=\"" + topPos + "\" left=\"40\" width=\"1200\" height=\"64\" href=\"page:" +
                        HttpUtility.HtmlAttributeEncode(playUrl) + "\">" +
                            "<Text top=\"6\" left=\"30\" width=\"1040\" height=\"28\" alignment=\"left\" justification=\"left\" fontstyle=\"Reg26\" foreground=\"argb(255,255,255,255)\">" +
                                HttpUtility.HtmlEncode(videoNameNoExt) +
                            "</Text>" +
                            "<Text top=\"34\" left=\"30\" width=\"1040\" height=\"22\" alignment=\"left\" justification=\"left\" fontstyle=\"Reg18\" foreground=\"argb(255,180,180,180)\">" +
                                HttpUtility.HtmlEncode("Channel: " + channelName + " • Uploaded: " + agoText) +
                            "</Text>" +
                        "</Button>"
                    );

                    topPos += 72;
                }
            }

            if (totalVideos > page * pageSize)
            {
                string nextUrl = PAGE_BASE_URL
                    + "?viewvideos=1"
                    + "&artist=" + HttpUtility.UrlEncode(artistId)
                    + "&artist_name=" + HttpUtility.UrlEncode(artistName)
                    + "&channel=" + HttpUtility.UrlEncode(Request.QueryString["channel"] ?? "")
                    + "&me_id=" + HttpUtility.UrlEncode(meId)
                    + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid)
                    + "&page=" + (page + 1);

                sb.AppendLine(
                    "<Button id=\"btnMore\" top=\"" + (topPos + 10) + "\" left=\"40\" width=\"1200\" height=\"56\" href=\"page:" +
                    HttpUtility.HtmlAttributeEncode(nextUrl) + "\">" +
                    "<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg26\" foreground=\"argb(255,255,255,255)\">Load More</Text>" +
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

private string BuildFullVideoUrl(string fileName)
{
    if (string.IsNullOrEmpty(fileName))
        return VIDEO_BASE_PATH;

    string name = fileName.Trim();

    // Ако е full URL → извлечи само filename
    if (name.StartsWith("http", StringComparison.OrdinalIgnoreCase))
    {
        int lastSlash = name.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < name.Length - 1)
        {
            name = name.Substring(lastSlash + 1);
        }
    }

    // Замени backslashes ако има
    name = name.Replace("\\", "/");

    return VIDEO_BASE_PATH + name;
}

    private string GetDisplayVideoName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "";

        string name = fileName.Trim();

        int lastSlash = name.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < name.Length - 1)
            name = name.Substring(lastSlash + 1);

        int lastBackSlash = name.LastIndexOf('\\');
        if (lastBackSlash >= 0 && lastBackSlash < name.Length - 1)
            name = name.Substring(lastBackSlash + 1);

        if (name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 4);

        name = name.Replace('_', ' ');
        return name;
    }

    private string GetRelativeTimeText(long unixSeconds)
    {
        try
        {
            DateTimeOffset then = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            TimeSpan diff = DateTimeOffset.UtcNow - then;

            if (diff.TotalSeconds < 60)
                return "just now";

            if (diff.TotalMinutes < 60)
            {
                int minutes = (int)Math.Floor(diff.TotalMinutes);
                return minutes == 1 ? "1 minute ago" : minutes + " minutes ago";
            }

            if (diff.TotalHours < 24)
            {
                int hours = (int)Math.Floor(diff.TotalHours);
                return hours == 1 ? "1 hour ago" : hours + " hours ago";
            }

            if (diff.TotalDays < 30)
            {
                int days = (int)Math.Floor(diff.TotalDays);
                return days == 1 ? "1 day ago" : days + " days ago";
            }

            if (diff.TotalDays < 365)
            {
                int months = (int)Math.Floor(diff.TotalDays / 30.0);
                if (months < 1) months = 1;
                return months == 1 ? "1 month ago" : months + " months ago";
            }

            int years = (int)Math.Floor(diff.TotalDays / 365.0);
            if (years < 1) years = 1;
            return years == 1 ? "1 year ago" : years + " years ago";
        }
        catch
        {
            return "";
        }
    }

    private List<ChannelInfo> GetChannelsFromAPI(string search)
    {
        List<ChannelInfo> result = new List<ChannelInfo>();
        string url = API_BASE_URL + "?action=list";

        try
        {
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                string resp = wc.DownloadString(url);
                JArray j = JArray.Parse(resp);

                for (int i = 0; i < j.Count; i++)
                {
                    JObject item = j[i] as JObject;
                    if (item == null) continue;

                    ChannelInfo ch = new ChannelInfo();

                    JToken idToken = item["id"];
                    JToken artistToken = item["artist"];

                    ch.id = idToken != null ? idToken.ToString() : "";
                    ch.name = artistToken != null ? artistToken.ToString() : "";

                    if (string.IsNullOrEmpty(search) || ch.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                        result.Add(ch);
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private List<VideoInfo> GetArtistVideos(string artistId)
    {
        List<VideoInfo> result = new List<VideoInfo>();
        string url = API_BASE_URL + "?action=get_artist_videos&artist=" + HttpUtility.UrlEncode(artistId);

        try
        {
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                string resp = wc.DownloadString(url);
                JArray arr = JArray.Parse(resp);

                for (int i = 0; i < arr.Count; i++)
                {
                    JObject item = arr[i] as JObject;
                    if (item == null) continue;

                    VideoInfo v = new VideoInfo();

                    JToken fileToken = item["file"];
                    JToken artistToken = item["artist"];
                    JToken modifiedToken = item["modified"];

                    v.file = fileToken != null ? fileToken.ToString() : "";
                    v.artist = artistToken != null ? artistToken.ToString() : "";

                    long modified = 0;
                    if (modifiedToken != null)
                        long.TryParse(modifiedToken.ToString(), out modified);

                    v.modified = modified;

                    if (!string.IsNullOrEmpty(v.file))
                        result.Add(v);
                }
            }
        }
        catch
        {
        }

        return result;
    }

    private string SanitizeId(string input)
    {
        if (string.IsNullOrEmpty(input)) return "unknown";

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else
                sb.Append('_');
        }

        string s = sb.ToString();
        return s.Length <= 60 ? s : s.Substring(0, 60);
    }

    private class ChannelInfo
    {
        public string id;
        public string name;
    }

    private class VideoInfo
    {
        public string file;
        public string artist;
        public long modified;
    }
}