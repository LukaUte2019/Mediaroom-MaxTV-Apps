using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.Script.Serialization;

namespace PFTvBills
{
    public partial class LukifyVideos : Page
    {
        private const string QUERY_SECRET = "supersecure123"; // <<< Ова треба да е тука
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
            Response.Cache.SetNoStore();
            

            string hostAbsolute = "http://172.16.40.101";
            string deviceGuid = Request.QueryString["DeviceGuid"];
            string userId = "";

            if (!string.IsNullOrEmpty(deviceGuid))
            {
                userId = GetUserIdFromDeviceGuid(deviceGuid);
            }
            string mode = (Request.QueryString["mode"] ?? "all").ToLower(); // "all" или "following"

            string feedUrl = "http://172.16.40.100/social_feed.php";


            if (!string.IsNullOrEmpty(userId) && mode == "following")
            {
                feedUrl += "?query_secret=" + QUERY_SECRET +
                           "&me_id=" + userId +
                           "&following=true";
            }

            int page = GetInt(Request.QueryString["page"], 1);
            int limit = GetInt(Request.QueryString["pageSize"], 10);
            string rawSearch = (Request.QueryString["SearchLukaTube"] ?? "").Trim().ToLower();

            Feed feed = FetchFeed(feedUrl, page, limit, rawSearch, mode);

            List<Post> posts = feed.posts ?? new List<Post>();
            int totalPosts = feed.total_posts > 0 ? feed.total_posts : posts.Count;
            bool hasMore = feed.has_more;

            StringBuilder mrml = new StringBuilder();

            mrml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            mrml.Append("<uidescription version=\"3.0\">");
            mrml.Append("<MrmlPage id=\"LukifyVideosList\" appid=\"lukatube.app/1.0\" width=\"1280\" height=\"720\">");
            mrml.Append("<Header />");
            mrml.Append("<Panel id=\"MainPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"720\">");

          // Determine mode display text
string modeDisplay = mode.Equals("following", StringComparison.OrdinalIgnoreCase) ? "Following" : "All";

// Build title text
string titleText = totalPosts > 0
    ? string.Format("Lukify Videos ({0} - page {1}, showing {2} of {3})",
        modeDisplay,
        page,
        posts.Count,
        totalPosts)
    : "Lukify Videos";

// Append to MRML
mrml.AppendFormat(
    "<Text id=\"Title\" top=\"10\" left=\"20\" width=\"900\" height=\"30\" fontstyle=\"Reg26\" foreground=\"argb(255,228,0,115)\">{0}</Text>",
    EscapeXml(titleText)
);

            int topOffset = 50;

            if (!string.IsNullOrEmpty(deviceGuid))
            {
               // Toggle бутони MRML со EscapeXml
               mrml.Append("<Panel top=\"40\" left=\"20\" width=\"1240\" height=\"50\">");

               string followingUrl = hostAbsolute + "/SETTEMediaroomApp/LukifyVideos.aspx?DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid) + "&mode=following";
               string allUrl       = hostAbsolute + "/SETTEMediaroomApp/LukifyVideos.aspx?DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid) + "&mode=all";

               mrml.AppendFormat(
                   "<Button top=\"0\" left=\"0\" width=\"200\" height=\"40\" href=\"page:{0}\">" +
                   "<Text top=\"0\" left=\"8\" width=\"184\" height=\"40\">Following</Text></Button>",
                   EscapeXml(followingUrl)
               );

               mrml.AppendFormat(
                   "<Button top=\"0\" left=\"210\" width=\"200\" height=\"40\" href=\"page:{0}\">" +
                   "<Text top=\"0\" left=\"8\" width=\"184\" height=\"40\">All</Text></Button>",
                   EscapeXml(allUrl)
               );

               mrml.Append("</Panel>");
            }

            // Поместување на topOffset за видеата под toggle
            topOffset += 60;
            foreach (Post post in posts)
            {
                if (string.IsNullOrEmpty(post.video_url) || post.video_url == "not found")
                    continue;

                string userName = EscapeXml(StripEmojis(
                    post.user != null
                        ? (post.user.full_name ?? post.user.username ?? "Unknown")
                        : "Unknown"
                ));

                string caption = EscapeXml(StripEmojis(post.caption ?? "No title"));

                string videoUrl = post.video_url.Replace(
                    "https://lukaserver.ddns.net",
                    "http://172.16.40.100"
                );

               // Base64 encode за URL-safe пренос
               string videoId = HttpUtility.UrlEncode(
                   Convert.ToBase64String(Encoding.UTF8.GetBytes(videoUrl))
               );

               string videoNameEncoded = HttpUtility.UrlEncode(
                   Convert.ToBase64String(
                       Encoding.UTF8.GetBytes(post.caption ?? "Video")
                   )
               );

               // Додавање song name
               string songName = "";
               if (post.song != null)
               {
                   songName = (post.song.title ?? "Unknown song") + " - " + (post.song.artist ?? "Unknown artist");
               }

               string songNameEncoded = HttpUtility.UrlEncode(
                   Convert.ToBase64String(Encoding.UTF8.GetBytes(songName))
               );

             // --- User info encoding (new) ---
string userUsername = (post.user != null && !string.IsNullOrEmpty(post.user.username)) 
    ? post.user.username 
    : "";

string userFullName = (post.user != null && !string.IsNullOrEmpty(post.user.full_name)) 
    ? post.user.full_name 
    : "";

string userAvatar = (post.user != null && !string.IsNullOrEmpty(post.user.profile_picture_url)) 
    ? post.user.profile_picture_url 
    : "";

// User ID од постот
string userIdPost = (post.user != null) 
    ? post.user.userid.ToString() 
    : "";

// Encode за GET (Base64 + URL)
string userUsernameEncoded = HttpUtility.UrlEncode(Convert.ToBase64String(Encoding.UTF8.GetBytes(userUsername)));
string userFullNameEncoded = HttpUtility.UrlEncode(Convert.ToBase64String(Encoding.UTF8.GetBytes(userFullName)));
string userAvatarEncoded   = HttpUtility.UrlEncode(Convert.ToBase64String(Encoding.UTF8.GetBytes(userAvatar)));
string userIdPostEncoded   = HttpUtility.UrlEncode(userIdPost);
// --- end user info encoding ---
// Правање song_url од song објект
string songUrl = "";
if (post.song != null && !string.IsNullOrEmpty(post.song.song_url))
{
    songUrl = post.song.song_url; // вистинскиот URL на песната
}

// Encode за GET
string songUrlEncoded = HttpUtility.UrlEncode(songUrl);

// Финален playUrl со song_url, deviceGuid и user_userid
string playUrl = hostAbsolute +
    "/SETTEMediaroomApp/PlayLukifyVideo.aspx?videoId=" + videoId +
    "&videoName=" + videoNameEncoded +
    "&songName=" + songNameEncoded +
    "&mode=" + HttpUtility.UrlEncode(mode) +
    (!string.IsNullOrEmpty(userId) ? "&me_id=" + HttpUtility.UrlEncode(userId) : "") +
    "&user_username=" + userUsernameEncoded +
    "&user_fullname=" + userFullNameEncoded +
    "&user_avatar=" + userAvatarEncoded +
    "&song_url=" + songUrlEncoded +
    (!string.IsNullOrEmpty(deviceGuid) ? "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid) : "") +
    (!string.IsNullOrEmpty(userId) ? "&user_userid=" + HttpUtility.UrlEncode(userIdPostEncoded) : "");
                string songText = "";
                if (post.song != null)
                {
                    songText = " (Song: " +
                        EscapeXml(post.song.title ?? "Unknown song") +
                        " by " +
                        EscapeXml(post.song.artist ?? "Unknown artist") +
                        ")";
                }

                bool hasPic = post.user != null && !string.IsNullOrEmpty(post.user.profile_picture_url);

                mrml.AppendFormat(
                    "<Panel top=\"{0}\" left=\"20\" width=\"1240\" height=\"60\">",
                    topOffset
                );

                if (hasPic)
                {
                    mrml.AppendFormat(
                        "<Image top=\"5\" left=\"0\" width=\"50\" height=\"50\" src=\"{0}\" />",
                        EscapeXml(post.user.profile_picture_url)
                    );
                }

                mrml.AppendFormat(
                    "<Button top=\"0\" left=\"{0}\" width=\"{1}\" height=\"50\" href=\"page:{2}\">" +
                    "<Text top=\"0\" left=\"8\" width=\"{3}\" height=\"50\">" +
                    "Caption: {4}{5} by {6}" +
                    "</Text></Button>",
                    hasPic ? 60 : 0,
                    hasPic ? 1180 : 1240,
                    EscapeXml(playUrl),
                    hasPic ? 1112 : 1224,
                    caption,
                    songText,
                    userName
                );

                mrml.Append("</Panel>");

                topOffset += 70;
            }

            if (hasMore)
            {
                string nextUrl = hostAbsolute +
                    "/SETTEMediaroomApp/LukifyVideos.aspx?DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid) + "&page=" + (page + 1) +
                    "&pageSize=" + limit +
                    (!string.IsNullOrEmpty(mode)
                        ? "&mode=" + HttpUtility.UrlEncode(mode)
                        : "") +
                    (!string.IsNullOrEmpty(rawSearch)
                        ? "&SearchLukaTube=" + HttpUtility.UrlEncode(rawSearch)
                        : "");

                mrml.AppendFormat(
                    "<Panel id=\"loadmorePanel\" width=\"1200\" height=\"80\" top=\"{0}\" left=\"40\">" +
                    "<Button id=\"loadMoreBtn\" top=\"10\" left=\"0\" width=\"600\" height=\"40\" fontstyle=\"Reg26\" href=\"page:{1}\">" +
                    "<Text top=\"0\" left=\"8\" width=\"584\" height=\"40\">Load more videos...</Text>" +
                    "</Button></Panel>",
                    topOffset + 10,
                    EscapeXml(nextUrl)
                );
            }

            mrml.Append("</Panel></MrmlPage></uidescription>");

            Response.Write(mrml.ToString());
            Response.End();
        }

        // ================= HELPERS =================

        private Feed FetchFeed(string url, int page, int limit, string search, string mode)
        {
            try
            {
                string fullUrl;

                if (url.Contains("?"))
                    fullUrl = url + "&page=" + page + "&limit=" + limit;
                else
                    fullUrl = url + "?page=" + page + "&limit=" + limit;

                // add mode
                if (!string.IsNullOrEmpty(mode))
                    fullUrl += "&mode=" + HttpUtility.UrlEncode(mode);

                if (!string.IsNullOrEmpty(search))
                    fullUrl += "&SearchLukaTube=" + HttpUtility.UrlEncode(search);

                WebClient wc = new WebClient();
                wc.Encoding = Encoding.UTF8;
                string json = wc.DownloadString(fullUrl);

                JavaScriptSerializer js = new JavaScriptSerializer();
                return js.Deserialize<Feed>(json) ?? new Feed();
            }
            catch
            {
                return new Feed();
            }
        }

        private string GetUserIdFromDeviceGuid(string deviceGuid)
        {
            try
            {
                string url = "http://172.16.40.100/get_lukify_clientidforuserid.php?deviceguid=" 
                             + HttpUtility.UrlEncode(deviceGuid);

                WebClient wc = new WebClient();
                wc.Encoding = Encoding.UTF8;
                string json = wc.DownloadString(url);

                JavaScriptSerializer js = new JavaScriptSerializer();
                var data = js.Deserialize<Dictionary<string, object>>(json);

                if (data != null && data.ContainsKey("userid"))
                    return data["userid"].ToString();
            }
            catch { }

            return "";
        }

        private int GetInt(string v, int d)
        {
            int i;
            return int.TryParse(v, out i) ? i : d;
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

        private string StripEmojis(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c <= 0xFFFF)
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }

    // ================= MODELS ================
    public class Feed
    {
        public List<Post> posts;
        public int total_posts;
        public bool has_more;
    }

    public class Post
    {
        public string caption;
        public string video_url;
        public User user;
        public Song song;
    }

  public class User
{
    public int userid; // <<< додадено
    public string username;
    public string full_name;
    public string profile_picture_url;
}

    public class Song
    {
        public string title;
        public string artist;
            public string song_url; // <-- додај ова поле

    }
}