using System;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;

namespace PFTvBills
{
    public partial class FollowersList : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
            Response.Cache.SetNoStore();

            string meId = Request.QueryString["user_id"];          // Logged-in user
            string userId = Request.QueryString["selected_user_id"]; // Profile being viewed

            // Paging parameters
            int page = 1;
            int limit = 10;
            if (!string.IsNullOrEmpty(Request.QueryString["page"]))
            {
                int.TryParse(Request.QueryString["page"], out page);
                if (page < 1) page = 1;
            }
            if (!string.IsNullOrEmpty(Request.QueryString["limit"]))
            {
                int.TryParse(Request.QueryString["limit"], out limit);
                if (limit < 1) limit = 10;
            }

            JArray followersList = new JArray();
            JArray followingList = new JArray(); // For mutual check
            try
            {
                // Fetch followers
                string followersListUrl = string.Format(
                    "http://172.16.40.100/followerslist.php?query_secret=supersecure123&me_id={0}&followers_page={1}&followers_limit={2}",
                    HttpUtility.UrlEncode(userId),
                    page,
                    limit
                );

                using (WebClient wc = new WebClient())
                {
                    string json = wc.DownloadString(followersListUrl);
                    JObject followersData = JObject.Parse(json);

                    if (followersData["followers"] != null &&
                        followersData["followers"]["items"] != null &&
                        followersData["followers"]["items"].Type == JTokenType.Array)
                    {
                        followersList = (JArray)followersData["followers"]["items"];
                    }

                    // Fetch following list of logged-in user for mutual check
                    string followingUrl = string.Format(
                        "http://172.16.40.100/followerslist.php?query_secret=supersecure123&me_id={0}&following_page=1&following_limit=1000",
                        HttpUtility.UrlEncode(meId)
                    );

                    string followingJson = wc.DownloadString(followingUrl);
                    JObject followingData = JObject.Parse(followingJson);
                    if (followingData["following"] != null &&
                        followingData["following"]["items"] != null &&
                        followingData["following"]["items"].Type == JTokenType.Array)
                    {
                        followingList = (JArray)followingData["following"]["items"];
                    }
                }
            }
            catch
            {
                followersList = new JArray();
                followingList = new JArray();
            }

            // --- Build MRML ---
            StringBuilder mrml = new StringBuilder();
            mrml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            mrml.Append("<uidescription version=\"3.0\">");
            mrml.Append("<MrmlPage id=\"FollowersListPage\" width=\"1280\" height=\"720\">");
            mrml.Append("<Panel id=\"MainPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"720\">");
            mrml.Append("<Text left=\"60\" top=\"20\" fontstyle=\"Reg36\" foreground=\"argb(255,255,255,255)\">Followers</Text>");

            int topPos = 80;
            int leftPos = 60;
            int size = 80;         // image size
            int spacing = 40;
            int maxPerRow = 8;
            int count = 0;

            foreach (var f in followersList)
            {
                JObject follower = f as JObject;
                if (follower == null) continue;

                string username = "";
                if (follower["username"] != null && follower["username"].Type != JTokenType.Null)
                    username = follower["username"].ToString();

                string fullName = username;
                if (follower["full_name"] != null && follower["full_name"].Type != JTokenType.Null)
                    fullName = follower["full_name"].ToString();

                string profilePic = "AppImages/default_profile.png";
                if (follower["profile_picture_url"] != null && follower["profile_picture_url"].Type != JTokenType.Null)
                    profilePic = follower["profile_picture_url"].ToString();

                profilePic = profilePic.Replace("https://lukaserver.ddns.net", "http://172.16.40.100")
                                       .Replace("http://lukaserver.ddns.net", "http://172.16.40.100");
                profilePic = EscapeXml(profilePic);

                string selectedUserId = "";
                if (follower["user_id"] != null && follower["user_id"].Type != JTokenType.Null)
                    selectedUserId = follower["user_id"].ToString();
                else if (follower["id"] != null && follower["id"].Type != JTokenType.Null)
                    selectedUserId = follower["id"].ToString();

                // Check mutual
                bool isMutual = false;
                for (int i = 0; i < followingList.Count; i++)
                {
                    JObject fUser = followingList[i] as JObject;
                    if (fUser != null)
                    {
                        string fid = "";
                        if (fUser["user_id"] != null) fid = fUser["user_id"].ToString();
                        else if (fUser["id"] != null) fid = fUser["id"].ToString();

                        if (fid == selectedUserId)
                        {
                            isMutual = true;
                            break;
                        }
                    }
                }

                var qs = HttpUtility.ParseQueryString(string.Empty);
                qs["username"] = username;
                qs["user_id"] = meId;
                qs["userid"] = meId;
                qs["selected_user_id"] = selectedUserId;

                string href = "page:http://172.16.40.101/SETTEMediaroomApp/ViewProfile.aspx?" + qs.ToString();
                href = EscapeXml(href);

                // --- Build button ---
                mrml.AppendFormat(
                    "<Button id=\"FollowerBtn{0}\" left=\"{1}\" top=\"{2}\" width=\"{3}\" height=\"{4}\" href=\"{5}\">",
                    count, leftPos, topPos, size, size + 48, href
                );

                mrml.AppendFormat("<Image left=\"0\" top=\"0\" width=\"{0}\" height=\"{0}\" url=\"{1}\" />", size, profilePic);

                // Full name + username + mutual in smaller font
                string textLabel = fullName + " (" + username + ")";
                if (isMutual) textLabel += " [Mutual]";

                mrml.AppendFormat("<Text left=\"0\" top=\"{0}\" width=\"{1}\" height=\"48\" fontstyle=\"Reg16\" foreground=\"argb(255,255,255,255)\" alignment=\"center\">{2}</Text>",
                    size, size, EscapeXml(textLabel));

                mrml.Append("</Button>");

                leftPos += size + spacing;
                count++;
                if (count % maxPerRow == 0)
                {
                    leftPos = 60;
                    topPos += size + 48 + spacing;
                }
            }

            // --- Back button ---
            var backQs = HttpUtility.ParseQueryString(string.Empty);
            backQs["username"] = "";
            backQs["user_id"] = meId;
            backQs["userid"] = meId;
            backQs["selected_user_id"] = userId;

            string backUrl = "page:http://172.16.40.101/SETTEMediaroomApp/ViewProfile.aspx?" + backQs.ToString();
            backUrl = EscapeXml(backUrl);

            mrml.AppendFormat(
                "<Button id=\"BackButton\" left=\"1040\" top=\"640\" width=\"180\" height=\"56\" href=\"{0}\">Back</Button>",
                backUrl
            );

            mrml.Append("</Panel></MrmlPage></uidescription>");
            Response.Write(mrml.ToString());
            Response.Flush();
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }

        private string EscapeXml(string s)
        {
            if (s == null) s = "";
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;");
        }
    }
}