using System;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Net;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

public partial class OpenInstagramVideo : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetNoStore();

        string videoUrl = (Request.QueryString["video_url"] ?? "").Trim();
        string videoName = (Request.QueryString["video_name"] ?? "").Trim();
        string postShareLink = (Request.QueryString["post_share_link"] ?? "").Trim();
        string postShareId = (Request.QueryString["post_share_id"] ?? "").Trim();
        string selectedUserId = (Request.QueryString["selected_user_id"] ?? "").Trim();
        string thumbnailUrl = (Request.QueryString["thumbnail_url"] ?? "").Trim();
        string igUsername = (Request.QueryString["ig_username"] ?? "").Trim();
        string ownerUsername = (Request.QueryString["owner_username"] ?? "").Trim();
        string songName = (Request.QueryString["title"] ?? "").Trim();
        string artistName = (Request.QueryString["artist_name"] ?? "").Trim();

        string view = (Request.QueryString["view"] ?? "").Trim().ToLowerInvariant();
        string mode = (Request.QueryString["mode"] ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(mode))
            mode = (view == "videos") ? "video" : "reel";

        bool isNavMode = mode == "reel" || mode == "video";

        int currentIndex = 0;
        int.TryParse(Request.QueryString["start"], out currentIndex);
        if (currentIndex < 0) currentIndex = 0;

        string displayView = NormalizeView(view);
        if (string.IsNullOrEmpty(displayView))
            displayView = mode == "video" ? "videos" : "reels";

        igUsername = (igUsername ?? "").Trim();

        if (string.IsNullOrEmpty(videoName))
            videoName = DeriveNameFromUrl(videoUrl);

        if (string.IsNullOrEmpty(videoName))
            videoName = "Instagram video";

        if (!string.IsNullOrEmpty(ownerUsername))
        {
            string audioLabel = "";
            if (!string.IsNullOrEmpty(songName))
            {
                if (!string.IsNullOrEmpty(artistName))
                    audioLabel = artistName + " - " + songName;
                else
                    audioLabel = songName;
            }

            if (!string.Equals(ownerUsername, igUsername, StringComparison.OrdinalIgnoreCase))
                videoName = "Instagram post by @" + ownerUsername + " and @" + igUsername + " - " + videoName;
            else
                videoName = "Instagram post by @" + ownerUsername + " - " + videoName;

            if (!string.IsNullOrEmpty(audioLabel))
                videoName += " Song: " + audioLabel;
        }

        List<JObject> playablePosts = new List<JObject>();
        try
        {
            playablePosts = LoadPlayablePosts(igUsername, displayView);
        }
        catch
        {
            playablePosts = new List<JObject>();
        }

        if (currentIndex < 0) currentIndex = 0;
        if (playablePosts.Count > 0 && currentIndex >= playablePosts.Count)
            currentIndex = playablePosts.Count - 1;

        string prevHref = "";
        string nextHref = "";

        if (isNavMode && playablePosts.Count > 0)
        {
            if (currentIndex > 0)
                prevHref = BuildOpenVideoUrlFromNode(playablePosts[currentIndex - 1], igUsername, displayView, selectedUserId, currentIndex - 1);

            if (currentIndex + 1 < playablePosts.Count)
                nextHref = BuildOpenVideoUrlFromNode(playablePosts[currentIndex + 1], igUsername, displayView, selectedUserId, currentIndex + 1);
        }

        string apiUrl = "http://172.16.40.100/ig_reel_proxy_downloader.php?video_url=" + HttpUtility.UrlEncode(videoUrl);
        string mp4Url = "";

        if (!string.IsNullOrEmpty(postShareId))
        {
            apiUrl += "&post_id=" + HttpUtility.UrlEncode(postShareId);
            apiUrl += "&post_share_id=" + HttpUtility.UrlEncode(postShareId);
        }

        try
        {
            using (WebClient wc = new WebClient())
            {
                string json = wc.DownloadString(apiUrl);

                JavaScriptSerializer js = new JavaScriptSerializer();
                object decoded = js.DeserializeObject(json);
                Dictionary<string, object> data = decoded as Dictionary<string, object>;

                if (data != null && data.ContainsKey("mp4_url") && data["mp4_url"] != null)
                    mp4Url = data["mp4_url"].ToString();
            }
        }
        catch
        {
        }

        string videoToSend = !string.IsNullOrEmpty(mp4Url) ? mp4Url : videoUrl;
        if (!string.IsNullOrEmpty(videoToSend))
            videoToSend = videoToSend.Replace("172.16.40.100", "lukaserver.ddns.net");

        string escapedMp4 = EscapeXml(mp4Url);
        string escapedThumbnailUrl = EscapeXml(thumbnailUrl);

        string sendPageFull = string.Format(
            "page:http://172.16.40.101/SETTEMediaroomApp/SendTo.aspx?message={0}&userid={1}&thumbnail={2}",
            HttpUtility.UrlEncode(postShareLink ?? ""),
            HttpUtility.UrlEncode(selectedUserId ?? ""),
            HttpUtility.UrlEncode(thumbnailUrl ?? "")
        );
        string escapedSendPageFull = EscapeXml(sendPageFull);

        string sendVideoComponent = string.Format(
            "page:http://172.16.40.101/SETTEMediaroomApp/SendTo.aspx?message={0}&video_name={1}&userid={2}&thumbnail={3}",
            HttpUtility.UrlEncode(videoToSend ?? ""),
            HttpUtility.UrlEncode(videoName ?? ""),
            HttpUtility.UrlEncode(selectedUserId ?? ""),
            HttpUtility.UrlEncode(thumbnailUrl ?? "")
        );
        string escapedSendVideoComponent = EscapeXml(sendVideoComponent);

        string displayTitle = EscapeXml(Truncate(videoName, 120));

        StringBuilder mrml = new StringBuilder();
        mrml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        mrml.Append("<uidescription version=\"3.0\">");
        mrml.Append("<MrmlPage id=\"OpenInstagramVideo\" width=\"1280\" height=\"720\">");

        mrml.AppendFormat(
            "<Video id=\"VideoPlayer\" left=\"0\" top=\"0\" width=\"1280\" height=\"720\" tuneurl=\"{0}\" ispip=\"false\" autoplay=\"true\" showbusyindicator=\"true\" />",
            escapedMp4
        );

        mrml.AppendFormat(
            "<Text left=\"20\" top=\"16\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">{0}</Text>",
            displayTitle
        );

        if (!string.IsNullOrEmpty(escapedThumbnailUrl))
        {
            int thumbWidth = 240;
            int thumbHeight = 135;
            int thumbLeft = 1280 - thumbWidth - 20;
            int thumbTop = 12;

            mrml.AppendFormat(
                "<Image left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" url=\"{4}\" />",
                thumbLeft, thumbTop, thumbWidth, thumbHeight, escapedThumbnailUrl
            );
        }

        mrml.Append("<Button id=\"BackButton\" left=\"460\" top=\"660\" width=\"150\" height=\"40\" href=\"action:back\">Back</Button>");

        mrml.AppendLine("<Actions>");
        mrml.AppendLine("  <Action name=\"SendToPhone\" type=\"submit\" data=\"\" url=\"" + escapedSendPageFull + "\" method=\"GET\"/>");
        mrml.AppendLine("  <Event type=\"onkey:left\" action=\"SendToPhone\"/>");
        mrml.AppendLine("  <Event type=\"onkey:green\" action=\"SendToPhone\"/>");

        mrml.AppendLine("  <Action name=\"SendVideoFromPlayer\" type=\"submit\" data=\"\" url=\"" + escapedSendVideoComponent + "\" method=\"GET\"/>");
        mrml.AppendLine("  <Event type=\"onkey:right\" action=\"SendVideoFromPlayer\"/>");

        if (isNavMode)
        {
            if (!string.IsNullOrEmpty(prevHref))
            {
                mrml.AppendLine("  <Action name=\"PrevMedia\" type=\"submit\" data=\"\" url=\"" + EscapeXml(prevHref) + "\" method=\"GET\"/>");
                mrml.AppendLine("  <Event type=\"onkey:down\" action=\"PrevMedia\"/>");
                mrml.AppendLine("  <Event type=\"onkey:channeldown\" action=\"PrevMedia\"/>");
            }

            if (!string.IsNullOrEmpty(nextHref))
            {
                mrml.AppendLine("  <Action name=\"NextMedia\" type=\"submit\" data=\"\" url=\"" + EscapeXml(nextHref) + "\" method=\"GET\"/>");
                mrml.AppendLine("  <Event type=\"onkey:up\" action=\"NextMedia\"/>");
                mrml.AppendLine("  <Event type=\"onkey:channelup\" action=\"NextMedia\"/>");
                mrml.AppendLine("      <Event type=\"onmediaend\" action=\"NextMedia\"/>");

            }
        }

        string hardDiskPage = "page:\\Hard Disk\\TV2ClientCE\\Content\\channeltvhd.xml";
        mrml.AppendLine("  <Action name=\"OpenHardDisk\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"" + EscapeXml(hardDiskPage) + "\" method=\"GET\"/>");
        mrml.AppendLine("  <Event type=\"onenter\" action=\"OpenHardDisk\"/>");
        mrml.AppendLine("</Actions>");

        mrml.Append("</MrmlPage>");
        mrml.Append("</uidescription>");

        Response.Write(mrml.ToString());
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private List<JObject> LoadPlayablePosts(string username, string view)
    {
        List<JObject> playablePosts = new List<JObject>();

        string apiUrl = "http://172.16.40.100/get_ig_profile_info.php?query_secret=my_super_secret_key&username="
                        + HttpUtility.UrlEncode(username ?? "");

        JObject profileData = null;

        using (WebClient wc = new WebClient())
        {
            wc.Encoding = Encoding.UTF8;
            string json = wc.DownloadString(apiUrl);
            profileData = JObject.Parse(json);
        }

        if (profileData == null)
            return playablePosts;

        JObject dataObj = profileData["data"] as JObject;
        JObject user = null;
        if (dataObj != null)
            user = dataObj["user"] as JObject;
        if (user == null)
            return playablePosts;

        JArray posts = view == "videos"
            ? GetPostsArray(user, "edge_felix_video_timeline")
            : GetPostsArray(user, "edge_owner_to_timeline_media");

        for (int i = 0; i < posts.Count; i++)
        {
            JToken edge = posts[i];
            JObject node = edge["node"] as JObject;
            if (node == null) continue;

            if (IsPlayablePost(node))
                playablePosts.Add(node);
        }

        return playablePosts;
    }

    private string BuildOpenVideoUrlFromNode(JObject node, string username, string view, string selectedUserId, int startIndex)
    {
        if (node == null)
            return "";

        string postTitle = GetPostTitle(node);
        string postAuthor = GetPostAuthorUsername(node, username);

        string thumbRaw = node["thumbnail_src"] != null ? node["thumbnail_src"].ToString() : "";
        string thumbSmall = "http://172.16.40.100/ig_pfp_loader.php?image_url=" + HttpUtility.UrlEncode(thumbRaw);

        string videoUrl = node["video_url"] != null ? node["video_url"].ToString() : "";
        string videoName = Truncate(postTitle, 60);

        string shortcode = node["shortcode"] != null ? node["shortcode"].ToString() : "";
        string postShareLink = "";
        if (!string.IsNullOrEmpty(shortcode))
            postShareLink = "https://www.instagram.com/p/" + shortcode + "/";

        string postShareId = GetPostShareId(node);

        string postViews = GetPostViews(node);
        string postLikes = GetPostLikes(node);
        string postDate = GetPostUploadDate(node);

        string audioArtist = "";
        string audioTitle = "";
        if (NormalizeView(view) == "videos")
        {
            audioArtist = "";
            audioTitle = "";
        }
        else
        {
            audioArtist = GetClipsMusicArtist(node);
            audioTitle = GetClipsMusicSong(node);
        }

        string mode = NormalizeView(view) == "videos" ? "video" : "reel";

        StringBuilder url = new StringBuilder();
        url.Append("page:http://172.16.40.101/SETTEMediaroomApp/OpenInstagramVideo.aspx?");
        url.Append("video_url=").Append(HttpUtility.UrlEncode(videoUrl ?? ""));
        url.Append("&video_name=").Append(HttpUtility.UrlEncode(videoName ?? ""));
        url.Append("&ig_username=").Append(HttpUtility.UrlEncode(username ?? ""));
        url.Append("&owner_username=").Append(HttpUtility.UrlEncode(postAuthor ?? ""));
        url.Append("&post_share_link=").Append(HttpUtility.UrlEncode(postShareLink ?? ""));
        url.Append("&post_share_id=").Append(HttpUtility.UrlEncode(postShareId ?? ""));
        url.Append("&selected_user_id=").Append(HttpUtility.UrlEncode(selectedUserId ?? ""));
        url.Append("&thumbnail_url=").Append(HttpUtility.UrlEncode(thumbSmall ?? ""));
        url.Append("&view=").Append(HttpUtility.UrlEncode(NormalizeView(view) ?? ""));
        url.Append("&mode=").Append(HttpUtility.UrlEncode(mode));
        url.Append("&start=").Append(HttpUtility.UrlEncode(startIndex.ToString()));
        url.Append("&artist_name=").Append(HttpUtility.UrlEncode(audioArtist ?? ""));
        url.Append("&title=").Append(HttpUtility.UrlEncode(audioTitle ?? ""));
        url.Append("&views=").Append(HttpUtility.UrlEncode(postViews ?? ""));
        url.Append("&likes=").Append(HttpUtility.UrlEncode(postLikes ?? ""));
        url.Append("&uploaded_date=").Append(HttpUtility.UrlEncode(postDate ?? ""));

        return url.ToString();
    }

    private string GetPostShareId(JObject node)
    {
        if (node == null)
            return "";

        try
        {
            if (node["shortcode"] != null)
            {
                string v = node["shortcode"].ToString().Trim();
                if (!string.IsNullOrEmpty(v))
                    return v;
            }

            if (node["code"] != null)
            {
                string v = node["code"].ToString().Trim();
                if (!string.IsNullOrEmpty(v))
                    return v;
            }

            if (node["id"] != null)
            {
                string v = node["id"].ToString().Trim();
                if (!string.IsNullOrEmpty(v))
                    return v;
            }
        }
        catch
        {
        }

        return "";
    }

    private string NormalizeView(string view)
    {
        string v = (view ?? "").Trim().ToLowerInvariant();
        if (v == "reel") return "reels";
        if (v == "video") return "videos";
        if (v != "reels" && v != "videos") return "";
        return v;
    }

    private string NormalizePageHref(string href)
    {
        if (string.IsNullOrEmpty(href))
            return "";

        href = href.Trim();

        if (href.StartsWith("page:", StringComparison.OrdinalIgnoreCase))
            return href;

        if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return href;
        }

        return "page:" + href;
    }

    private string EscapeXml(string s)
    {
        return (s ?? "")
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private string DeriveNameFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        try
        {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u))
            {
                if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Uri.TryCreate("http://" + url, UriKind.Absolute, out u))
                        return "";
                }
                else
                {
                    return "";
                }
            }

            string path = u.AbsolutePath;
            if (string.IsNullOrEmpty(path)) return "";

            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "";

            string last = parts[parts.Length - 1];
            last = last.Split(new[] { '?', '#' })[0];
            last = HttpUtility.UrlDecode(last);
            last = last.Replace('-', ' ').Replace('_', ' ').Trim();
            if (last.Length > 60) last = last.Substring(0, 60).Trim();
            return last;
        }
        catch
        {
            return "";
        }
    }

    private string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || max <= 0) return "";
        if (s.Length <= max) return s;
        return s.Substring(0, max).TrimEnd() + "...";
    }

    private JArray GetPostsArray(JObject user, string key)
    {
        try
        {
            if (user == null) return new JArray();

            if (user[key] != null && user[key]["edges"] != null)
            {
                JArray arr = user[key]["edges"] as JArray;
                if (arr != null)
                    return arr;
            }
        }
        catch
        {
        }

        return new JArray();
    }

    private bool IsPlayablePost(JObject node)
    {
        if (node == null)
            return false;

        try
        {
            string typename = node["__typename"] != null ? node["__typename"].ToString() : "";
            string productType = node["product_type"] != null ? node["product_type"].ToString() : "";

            return string.Equals(typename, "GraphVideo", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(productType, "igtv", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private string GetPostTitle(JObject node)
    {
        if (node == null) return "Instagram post";

        if (node["edge_media_to_caption"] != null && node["edge_media_to_caption"]["edges"] != null)
        {
            JArray captionEdges = node["edge_media_to_caption"]["edges"] as JArray;
            if (captionEdges != null && captionEdges.Count > 0)
            {
                JObject capNode = captionEdges[0]["node"] as JObject;
                if (capNode != null && capNode["text"] != null)
                    return capNode["text"].ToString();
            }
        }

        if (node["accessibility_caption"] != null)
            return node["accessibility_caption"].ToString();

        return "Instagram post";
    }

    private string GetPostAuthorUsername(JObject node, string fallbackUsername)
    {
        if (node == null)
            return fallbackUsername ?? "";

        try
        {
            if (node["owner"] != null)
            {
                JObject owner = node["owner"] as JObject;
                if (owner != null && owner["username"] != null)
                    return owner["username"].ToString();
            }

            if (node["user"] != null)
            {
                JObject u = node["user"] as JObject;
                if (u != null)
                {
                    if (u["username"] != null)
                        return u["username"].ToString();

                    if (u["full_name"] != null)
                        return u["full_name"].ToString();
                }
            }
        }
        catch
        {
        }

        return fallbackUsername ?? "";
    }

    private string GetPostViews(JObject node)
    {
        if (node == null)
            return "Views: 0";

        string[] candidateKeys = new string[]
        {
            "video_view_count",
            "view_count",
            "play_count",
            "video_play_count",
            "plays"
        };

        foreach (string key in candidateKeys)
        {
            if (node[key] != null)
            {
                string raw = node[key].ToString();
                long n;
                if (long.TryParse(raw, out n))
                    return "Views: " + n.ToString("N0");

                if (!string.IsNullOrEmpty(raw))
                    return "Views: " + raw;
            }
        }

        return "Views: 0";
    }

    private string GetPostLikes(JObject node)
    {
        if (node == null)
            return "Likes: 0";

        try
        {
            if (node["edge_media_preview_like"] != null && node["edge_media_preview_like"]["count"] != null)
            {
                string raw = node["edge_media_preview_like"]["count"].ToString();
                long n;
                if (long.TryParse(raw, out n))
                    return "Likes: " + n.ToString("N0");
                if (!string.IsNullOrEmpty(raw))
                    return "Likes: " + raw;
            }

            if (node["edge_liked_by"] != null && node["edge_liked_by"]["count"] != null)
            {
                string raw = node["edge_liked_by"]["count"].ToString();
                long n;
                if (long.TryParse(raw, out n))
                    return "Likes: " + n.ToString("N0");
                if (!string.IsNullOrEmpty(raw))
                    return "Likes: " + raw;
            }

            string[] candidateKeys = new string[]
            {
                "like_count",
                "likes"
            };

            foreach (string key in candidateKeys)
            {
                if (node[key] != null)
                {
                    string raw = node[key].ToString();
                    long n;
                    if (long.TryParse(raw, out n))
                        return "Likes: " + n.ToString("N0");

                    if (!string.IsNullOrEmpty(raw))
                        return "Likes: " + raw;
                }
            }
        }
        catch
        {
        }

        return "Likes: 0";
    }

    private string GetPostUploadDate(JObject node)
    {
        if (node == null)
            return "Uploaded: unknown";

        try
        {
            string[] candidateKeys = new string[]
            {
                "taken_at_timestamp",
                "taken_at",
                "date",
                "timestamp"
            };

            foreach (string key in candidateKeys)
            {
                if (node[key] == null)
                    continue;

                string raw = node[key].ToString().Trim();
                if (string.IsNullOrEmpty(raw))
                    continue;

                long unixSeconds;
                if (long.TryParse(raw, out unixSeconds))
                {
                    DateTimeOffset dto = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
                    return "Uploaded: " + dto.ToString("d MMMM yyyy HH:mm", CultureInfo.InvariantCulture);
                }

                DateTime dt;
                if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
                {
                    DateTime local = dt.ToLocalTime();
                    return "Uploaded: " + local.ToString("d MMMM yyyy HH:mm", CultureInfo.InvariantCulture);
                }

                if (DateTime.TryParse(raw, out dt))
                {
                    return "Uploaded: " + dt.ToString("d MMMM yyyy HH:mm", CultureInfo.InvariantCulture);
                }
            }
        }
        catch
        {
        }

        return "Uploaded: unknown";
    }

    private string GetClipsMusicArtist(JObject node)
    {
        if (node == null)
            return "";

        try
        {
            JObject music = node["clips_music_attribution_info"] as JObject;
            if (music == null) return "";

            return music["artist_name"] != null ? music["artist_name"].ToString() : "";
        }
        catch
        {
        }

        return "";
    }

    private string GetClipsMusicSong(JObject node)
    {
        if (node == null)
            return "";

        try
        {
            JObject music = node["clips_music_attribution_info"] as JObject;
            if (music == null) return "";

            return music["song_name"] != null ? music["song_name"].ToString() : "";
        }
        catch
        {
        }

        return "";
    }
}