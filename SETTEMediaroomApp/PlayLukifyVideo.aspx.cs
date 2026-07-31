using System;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Net;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace PFTvBills
{
    public partial class PlayLukifyVideo : Page
    {
        private const string FOLLOWING_QUERY_SECRET = "supersecure123";

        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
            Response.Cache.SetNoStore();

            string videoId = Request.QueryString["videoId"];
            string videoNameRaw = Request.QueryString["videoName"];
            string songNameRaw = Request.QueryString["songName"];
            string userAgent = Request.UserAgent ?? "";

            string mode = Request.QueryString["mode"] ?? "all";
            string meId = Request.QueryString["me_id"] ?? Request.QueryString["meId"] ?? "";
            string deviceGuid = Request.QueryString["DeviceGuid"] ?? "";

            string userUserId = Request.QueryString["user_userid"];
            string threadId = Request.QueryString["thread_id"] ?? Request.QueryString["threadId"] ?? Request.QueryString["thread"] ?? "";

            if (string.IsNullOrEmpty(videoId))
            {
                Response.StatusCode = 400;
                Response.Write("Missing videoId");
                Response.End();
                return;
            }

            string videoUrl = "";
            string videoName = "Video";
            string songName = "";

            string userUsername = "";
            string userFullName = "";
            string userAvatar = "";

            string userUsernameRaw = Request.QueryString["user_username"];
            string userFullNameRaw = Request.QueryString["user_fullname"];
            string userAvatarRaw = Request.QueryString["user_avatar"];

            try
            {
                videoUrl = Encoding.UTF8.GetString(Convert.FromBase64String(videoId)).Trim();

                if (!string.IsNullOrEmpty(videoNameRaw))
                    videoName = Encoding.UTF8.GetString(Convert.FromBase64String(videoNameRaw));

                if (!string.IsNullOrEmpty(songNameRaw))
                    songName = Encoding.UTF8.GetString(Convert.FromBase64String(songNameRaw));

                if (!string.IsNullOrEmpty(userUsernameRaw))
                    userUsername = Encoding.UTF8.GetString(Convert.FromBase64String(userUsernameRaw));

                if (!string.IsNullOrEmpty(userFullNameRaw))
                    userFullName = Encoding.UTF8.GetString(Convert.FromBase64String(userFullNameRaw));

                if (!string.IsNullOrEmpty(userAvatarRaw))
                    userAvatar = Encoding.UTF8.GetString(Convert.FromBase64String(userAvatarRaw));
            }
            catch
            {
            }

            string nextPlayPageUrl = "";
            string prevPlayPageUrl = "";

            try
            {
                Feed feed = FetchFeed(1, 200, mode, meId);
                if (feed != null && feed.posts != null && feed.posts.Count > 0)
                {
                    string normalizedCurrent = NormalizeVideoUrl(videoUrl);
                    int foundIndex = -1;

                    for (int i = 0; i < feed.posts.Count; i++)
                    {
                        string pUrl = feed.posts[i] != null ? feed.posts[i].video_url : null;
                        if (string.IsNullOrEmpty(pUrl)) continue;

                        string normalized = NormalizeVideoUrl(pUrl);
                        if (string.Equals(normalized, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
                        {
                            foundIndex = i;
                            break;
                        }
                    }

                    if (foundIndex >= 0)
                    {
                        for (int i = foundIndex + 1; i < feed.posts.Count; i++)
                        {
                            var candPost = feed.posts[i];
                            if (candPost == null || string.IsNullOrEmpty(candPost.video_url)) continue;

                            nextPlayPageUrl = BuildPlayPageUrl(
                                candPost.video_url,
                                candPost.caption,
                                candPost.song,
                                candPost.user,
                                mode,
                                meId
                            );
                            break;
                        }

                        for (int i = foundIndex - 1; i >= 0; i--)
                        {
                            var candPost = feed.posts[i];
                            if (candPost == null || string.IsNullOrEmpty(candPost.video_url)) continue;

                            prevPlayPageUrl = BuildPlayPageUrl(
                                candPost.video_url,
                                candPost.caption,
                                candPost.song,
                                candPost.user,
                                mode,
                                meId
                            );
                            break;
                        }
                    }
                }
            }
            catch
            {
            }

            string mrml = BuildMrml(
                videoUrl,
                videoName,
                songName,
                userAgent,
                nextPlayPageUrl,
                prevPlayPageUrl,
                userUsername,
                userFullName,
                userAvatar,
                userUserId,
                meId,
                threadId,
                deviceGuid
            );

            Response.Write(mrml);
            Response.End();
        }

        private string BuildMrml(
            string videoUrl,
            string videoName,
            string songName,
            string userAgent,
            string nextUrl,
            string prevUrl,
            string uploaderUsername,
            string uploaderFullName,
            string uploaderAvatar,
            string uploaderUserId,
            string viewerMeId,
            string threadId,
            string deviceGuid
        )
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Append("<uidescription version=\"3.0\">");
            sb.Append("<MrmlPage id=\"PlayVideoPage\" appid=\"lukatube.app/1.0\" width=\"1280\" height=\"720\">");
            sb.Append("<Header />");

            sb.Append("<Actions>");
            sb.Append("<Action name=\"scriptExit\" type=\"script\" function=\"handleAppLeave\"/>");

            if (!string.IsNullOrEmpty(nextUrl))
            {
                sb.Append("<Action name=\"NextVideo\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"page:");
                sb.Append(EscapeXml(nextUrl));
                sb.Append("\" method=\"GET\"/>");
                sb.Append("<Event type=\"onkey:channelup\" action=\"NextVideo\"/>");
                sb.Append("<Event type=\"onkey:up\" action=\"NextVideo\"/>");
                sb.Append("<Event type=\"onmediaend\" action=\"NextVideo\"/>");
            }

            if (!string.IsNullOrEmpty(prevUrl))
            {
                sb.Append("<Action name=\"PreviousVideo\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"page:");
                sb.Append(EscapeXml(prevUrl));
                sb.Append("\" method=\"GET\"/>");
                sb.Append("<Event type=\"onkey:channeldown\" action=\"PreviousVideo\"/>");
                sb.Append("<Event type=\"onkey:down\" action=\"PreviousVideo\"/>");
            }

            if (!string.IsNullOrEmpty(uploaderUsername) || !string.IsNullOrEmpty(uploaderUserId))
            {
                try
                {
                    string hostPart = Request.Url.GetLeftPart(UriPartial.Authority);
                    string appPath = Request.ApplicationPath ?? "";
                    if (appPath.EndsWith("/")) appPath = appPath.TrimEnd('/');

                    string profilePath = hostPart + appPath + "/SETTEMediaroomApp/ViewProfile.aspx";
                    var qlist = new List<string>();

                    if (!string.IsNullOrEmpty(uploaderUsername))
                        qlist.Add("username=" + HttpUtility.UrlEncode(uploaderUsername));

                    if (!string.IsNullOrEmpty(viewerMeId))
                        qlist.Add("user_id=" + HttpUtility.UrlEncode(viewerMeId));

                    if (!string.IsNullOrEmpty(uploaderUserId))
                        qlist.Add("selected_user_id=" + HttpUtility.UrlEncode(uploaderUserId));

                    if (!string.IsNullOrEmpty(threadId))
                        qlist.Add("thread_id=" + HttpUtility.UrlEncode(threadId));

                    string profileUrl = profilePath + (qlist.Count > 0 ? "?" + string.Join("&", qlist) : "");

                    sb.Append("<Action name=\"OpenProfile\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"page:");
                    sb.Append(EscapeXml(profileUrl));
                    sb.Append("\" method=\"GET\"/>");
                    sb.Append("<Event type=\"onkey:left\" action=\"OpenProfile\"/>");
                }
                catch
                {
                }
            }

            // Search song in LukifyMusic.aspx
            if (!string.IsNullOrWhiteSpace(songName) && !string.IsNullOrWhiteSpace(deviceGuid))
            {
                try
                {
                    string searchSongUrl = BuildLukifyMusicSearchUrl(songName, deviceGuid);

                    sb.Append("<Action name=\"SearchSong\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"page:");
                    sb.Append(EscapeXml(searchSongUrl));
                    sb.Append("\" method=\"GET\"/>");
                    sb.Append("<Event type=\"onkey:menu\" action=\"SearchSong\"/>");
                }
                catch
                {
                }
            }

            sb.Append("</Actions>");

            sb.Append("<Scripts>");
            sb.Append("<Script><![CDATA[");
            sb.Append("function handleAppLeave() { try { Application.Exit(); } catch(e) { } }");
            sb.Append("]]></Script>");
            sb.Append("</Scripts>");

            sb.Append("<Panel>");

            sb.Append("<Video id=\"video\" width=\"1280\" height=\"720\" visible=\"true\" ");
            sb.Append("showbusyindicator=\"true\" allowtrickmodes=\"true\" timeshiftenabled=\"true\" ");
            sb.Append("timeshiftbuffersize=\"3600\" tuneurl=\"");
            sb.Append(EscapeXml(videoUrl));
            sb.Append("\"></Video>");

            sb.Append("<Text id=\"VideoName\" top=\"10\" left=\"20\" width=\"1200\" height=\"40\" fontstyle=\"Reg26\" foreground=\"argb(255,228,0,115)\">");
            sb.Append(EscapeXml(videoName));
            sb.Append("</Text>");

            if (!string.IsNullOrWhiteSpace(songName) && !string.IsNullOrWhiteSpace(deviceGuid))
            {
                string searchSongUrl = BuildLukifyMusicSearchUrl(songName, deviceGuid);

                sb.Append("<Button id=\"btnSearchSong\" top=\"150\" left=\"20\" width=\"420\" height=\"60\" ");
                sb.Append("fontstyle=\"Reg22\" foreground=\"argb(255,255,255,255)\" ");
                sb.Append("backgroundcolor=\"argb(255,50,50,50)\" focusable=\"true\" visible=\"true\" href=\"page:");
                sb.Append(EscapeXml(searchSongUrl));
                sb.Append("\">");
                sb.Append(EscapeXml("Search Song: " + songName));
                sb.Append("</Button>");
            }

            if (!string.IsNullOrEmpty(songName))
            {
                sb.Append("<Text id=\"SongName\" top=\"60\" left=\"20\" width=\"1200\" height=\"30\" fontstyle=\"Reg22\" foreground=\"argb(255,180,180,180)\">");
                sb.Append("Song: ");
                sb.Append(EscapeXml(songName));
                sb.Append("</Text>");
            }

            int uploaderTop = 100;
            if (!string.IsNullOrEmpty(uploaderAvatar))
            {
                sb.Append("<Image id=\"UploaderAvatar\" top=\"");
                sb.Append(uploaderTop);
                sb.Append("\" left=\"20\" width=\"64\" height=\"64\" src=\"");
                sb.Append(EscapeXml(uploaderAvatar));
                sb.Append("\" />");
            }

            int uploaderTextLeft = string.IsNullOrEmpty(uploaderAvatar) ? 20 : 100;
            sb.Append("<Text id=\"UploaderText\" top=\"");
            sb.Append(uploaderTop);
            sb.Append("\" left=\"");
            sb.Append(uploaderTextLeft);
            sb.Append("\" width=\"1100\" height=\"30\" fontstyle=\"Reg22\" foreground=\"argb(255,200,200,200)\">");
            string uploaderDisplay = (!string.IsNullOrEmpty(uploaderFullName) ? uploaderFullName : uploaderUsername);
            if (string.IsNullOrEmpty(uploaderDisplay))
                uploaderDisplay = "Unknown uploader";
            sb.Append("Uploader: " + EscapeXml(uploaderDisplay));
            sb.Append("</Text>");

            sb.Append("<Text id=\"Main_WelcomeText\" highlightcolor=\"argb(255,228,0,115)\" margin=\"rect(30,20,0,0)\" width=\"800\" height=\"80\">");
            sb.Append("Device info: ");
            sb.Append(EscapeXml(ParseIPTVUserAgent(userAgent)));
            sb.Append("</Text>");

            sb.Append("</Panel>");
            sb.Append("</MrmlPage>");
            sb.Append("</uidescription>");

            return sb.ToString();
        }

        private string BuildLukifyMusicSearchUrl(string songName, string deviceGuid)
        {
            string hostPart = Request.Url.GetLeftPart(UriPartial.Authority);
            string appPath = Request.ApplicationPath ?? "";
            if (appPath.EndsWith("/")) appPath = appPath.TrimEnd('/');

            string basePath = hostPart + appPath + "/SETTEMediaroomApp/LukifyMusic.aspx";

            return basePath
                + "?search=" + HttpUtility.UrlEncode(songName)
                + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);
        }

        private string BuildPlayPageUrl(
            string rawVideoUrl,
            string caption,
            Song song,
            PostUser user,
            string mode,
            string meId
        )
        {
            if (string.IsNullOrEmpty(rawVideoUrl)) return "";

            string fixedVideoUrl = rawVideoUrl.Replace("https://lukaserver.ddns.net", "http://172.16.40.100").Trim().TrimEnd('/');

            string vidB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(fixedVideoUrl));
            string namePlain = string.IsNullOrEmpty(caption) ? "Video" : caption;
            string nameB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(namePlain));

            string songPlain = "";
            if (song != null)
            {
                songPlain = (song.title ?? "Unknown") + " - " + (song.artist ?? "Unknown");
            }
            string songB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(songPlain));

            string uUsernameB64 = "";
            string uFullnameB64 = "";
            string uAvatarB64 = "";
            if (user != null)
            {
                if (!string.IsNullOrEmpty(user.username))
                    uUsernameB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(user.username));
                if (!string.IsNullOrEmpty(user.full_name))
                    uFullnameB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(user.full_name));
                if (!string.IsNullOrEmpty(user.profile_picture_url))
                    uAvatarB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(user.profile_picture_url));
            }

            string hostPart = Request.Url.GetLeftPart(UriPartial.Authority);
            string appPath = Request.ApplicationPath ?? "";
            if (appPath.EndsWith("/")) appPath = appPath.TrimEnd('/');
            string basePlayPath = hostPart + appPath + "/SETTEMediaroomApp/PlayLukifyVideo.aspx";

            var q = new List<string>();
            q.Add("videoId=" + HttpUtility.UrlEncode(vidB64));
            q.Add("videoName=" + HttpUtility.UrlEncode(nameB64));
            if (!string.IsNullOrEmpty(mode))
                q.Add("mode=" + HttpUtility.UrlEncode(mode));
            if (!string.IsNullOrEmpty(meId))
                q.Add("me_id=" + HttpUtility.UrlEncode(meId));
            q.Add("songName=" + HttpUtility.UrlEncode(songB64));

            if (!string.IsNullOrEmpty(uUsernameB64))
                q.Add("user_username=" + HttpUtility.UrlEncode(uUsernameB64));
            if (!string.IsNullOrEmpty(uFullnameB64))
                q.Add("user_fullname=" + HttpUtility.UrlEncode(uFullnameB64));
            if (!string.IsNullOrEmpty(uAvatarB64))
                q.Add("user_avatar=" + HttpUtility.UrlEncode(uAvatarB64));

            return basePlayPath + "?" + string.Join("&", q);
        }

        private string EscapeXml(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;");
        }

        private string ParseIPTVUserAgent(string ua)
        {
            if (string.IsNullOrEmpty(ua)) return "Unknown device";
            return ua.Length > 120 ? ua.Substring(0, 120) : ua;
        }

        private string NormalizeVideoUrl(string u)
        {
            if (string.IsNullOrEmpty(u)) return "";
            string v = u.Trim().Replace("https://lukaserver.ddns.net", "http://172.16.40.100");
            if (v.EndsWith("/")) v = v.TrimEnd('/');
            return v;
        }

        private Feed FetchFeed(int page, int limit, string mode, string meId)
        {
            try
            {
                string feedEndpoint = "http://172.16.40.100/social_feed.php";
                string fullUrl = feedEndpoint + "?page=" + page + "&limit=" + limit;

                if (!string.IsNullOrEmpty(mode))
                {
                    if (mode == "following" && !string.IsNullOrEmpty(meId))
                    {
                        fullUrl += "&me_id=" + HttpUtility.UrlEncode(meId) +
                                   "&following=true" +
                                   "&query_secret=" + HttpUtility.UrlEncode(FOLLOWING_QUERY_SECRET);
                    }
                    else
                    {
                        fullUrl += "&mode=" + HttpUtility.UrlEncode(mode);
                    }
                }

                using (WebClient wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    string json = wc.DownloadString(fullUrl);

                    JavaScriptSerializer js = new JavaScriptSerializer();
                    Feed f = js.Deserialize<Feed>(json) ?? new Feed();
                    if (f.posts == null) f.posts = new List<Post>();
                    return f;
                }
            }
            catch
            {
                return new Feed { posts = new List<Post>() };
            }
        }

        public class Feed
        {
            public List<Post> posts { get; set; }
            public int total_posts { get; set; }
            public bool has_more { get; set; }

            public Feed()
            {
                posts = new List<Post>();
            }
        }

        public class Post
        {
            public string caption { get; set; }
            public string video_url { get; set; }
            public PostUser user { get; set; }
            public Song song { get; set; }
        }

        public class PostUser
        {
            public string username { get; set; }
            public string full_name { get; set; }
            public string profile_picture_url { get; set; }
        }

        public class Song
        {
            public string title { get; set; }
            public string artist { get; set; }
        }
    }
}