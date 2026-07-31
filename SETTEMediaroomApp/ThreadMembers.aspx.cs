using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;
using System.Linq;

public partial class ThreadMembers : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetNoStore();

        const string DEFAULT_QUERY_SECRET = "supersecure123";


        // Authenticated user ID (viewer)
        string queryUserId = Request.QueryString["userid"];
        string querySecret = Request.QueryString["query_secret"];
        int meId = 0;
        if (!string.IsNullOrEmpty(queryUserId))
        {
            int.TryParse(queryUserId, out meId);
            if (string.IsNullOrEmpty(querySecret))
                querySecret = DEFAULT_QUERY_SECRET;
        }

        // Thread ID (required)
        string threadId = Request.QueryString["thread_id"];
        // Thread name (optional)
        string threadName = Request.QueryString["thread_name"];
        if (string.IsNullOrEmpty(threadName))
            threadName = "Unknown Thread";
        if (string.IsNullOrEmpty(threadId))
        {
            Response.Write("<error>Missing thread_id</error>");
            Response.Flush();
            HttpContext.Current.ApplicationInstance.CompleteRequest();
            return;
        }

        // Paging handled by this ASPX page (not by API)
        int page = 0;
        int pageSize = 5;
        string pageQ = Request.QueryString["page"];
        if (!string.IsNullOrEmpty(pageQ))
            int.TryParse(pageQ, out page);
        if (page < 0) page = 0;
        int offset = page * pageSize;

        // Call PHP API to get all thread members (no paging params)
        string apiUrl = "http://172.16.40.100/thread_members.php";
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["thread_id"] = threadId;
        if (meId > 0) qs["userid"] = meId.ToString();
        if (!string.IsNullOrEmpty(querySecret)) qs["query_secret"] = querySecret;
        apiUrl += "?" + qs.ToString();

        string jsonResult = "{}";
        try
        {
            var req = (HttpWebRequest)WebRequest.Create(apiUrl);
            req.Method = "GET";
            req.Timeout = 5000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
            {
                jsonResult = sr.ReadToEnd();
            }
        }
        catch
        {
            jsonResult = "{\"error\":\"Cannot reach API\"}";
        }

        JObject threadData = null;
        try { threadData = JObject.Parse(jsonResult); }
        catch { threadData = new JObject(); }

        JArray members = threadData["members"] as JArray ?? new JArray();
        int totalMembers = members.Count;

        // Take the subset for this page
        var pageMembers = members.Skip(offset).Take(pageSize).ToList();

        // Build MRML
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("  <MrmlPage id=\"ThreadMembersPage\" width=\"1280\" height=\"720\">");
        sb.AppendLine("    <Panel id=\"MainPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"720\">");
      // ---------- HEADER ----------
sb.AppendLine(
    "      <Panel id=\"HeaderPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"100\" background=\"argb(255,25,25,25)\">"
);

// Top title
sb.AppendLine(
    "        <Text top=\"15\" left=\"40\" width=\"800\" height=\"40\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">Thread Details</Text>"
);

// Thread name below
sb.AppendLine(string.Format(
    "        <Text top=\"55\" left=\"40\" width=\"1000\" height=\"36\" fontstyle=\"Reg22\" foreground=\"argb(255,180,180,180)\">{0}</Text>",
    EscapeXml(threadName)
));

sb.AppendLine("      </Panel>");

        // Compact layout: left column starting at left=40, small buttons stacked with spacing
        int leftCol = 40;
        int compactWidth = 300;
        int compactHeight = 64;
        int spacingY = 16;
        int topPos = 120;

        // Action buttons layout (to the right of the compact member button)
        int actionSpacing = 12;
        int actionWidth = 100;
        int actionHeight = compactHeight;
        int actionLeftBase = leftCol + compactWidth + actionSpacing; // start position for action buttons

        foreach (var member in pageMembers)
        {
            string uname = member["username"] != null ? member["username"].ToString() : "unknown";
            string fullName = member["full_name"] != null ? member["full_name"].ToString() : "";
            string memberId = member["userid"] != null ? member["userid"].ToString() : (member["user_id"] != null ? member["user_id"].ToString() : "0");

            // Fallbacks
            if (string.IsNullOrEmpty(fullName))
                fullName = uname;
            if (string.IsNullOrEmpty(uname))
                uname = "unknown";

            string displayFull = EscapeXml(fullName);
            string displayUser = EscapeXml("@" + uname);

            // Detect following status from several possible JSON fields
            bool isFollowing = false;
            JToken followToken = member["is_following"] ?? member["following"] ?? member["you_follow"] ?? member["you_following"] ?? member["followed"] ?? member["follows"];
            if (followToken != null)
            {
                try
                {
                    if (followToken.Type == JTokenType.Boolean)
                        isFollowing = followToken.Value<bool>();
                    else if (followToken.Type == JTokenType.Integer)
                        isFollowing = followToken.Value<int>() != 0;
                    else
                    {
                        var s = (followToken.ToString() ?? "").Trim().ToLowerInvariant();
                        isFollowing = (s == "1" || s == "true" || s == "yes" || s == "following");
                    }
                }
                catch
                {
                    isFollowing = false;
                }
            }

            // --------- PROFILE PICTURE HANDLING ----------
            string pfp = "";
            // common keys
            string[] pfpKeys = new[] { "profile_picture_url", "profile_picture", "profile_pic", "avatar", "avatar_url", "image", "image_url", "picture", "photo", "thumbnail", "thumb" };
            foreach (var k in pfpKeys)
            {
                try
                {
                    var v = member[k];
                    if (v != null)
                    {
                        var s = v.Type == JTokenType.String ? v.ToString() : null;
                        if (!string.IsNullOrEmpty(s))
                        {
                            pfp = s;
                            break;
                        }
                    }
                }
                catch { }
            }

            // Normalize common internal hostnames to local IP (same replacement used elsewhere)
            if (!string.IsNullOrEmpty(pfp))
            {
                try
                {
                    if (pfp.Contains("lukaserver.ddns.net"))
                    {
                        pfp = pfp.Replace("https://lukaserver.ddns.net", "http://172.16.40.100").Replace("http://lukaserver.ddns.net", "http://172.16.40.100");
                    }
                    // scheme-less URL like //host/path -> add http:
                    if (pfp.StartsWith("//"))
                        pfp = "http:" + pfp;
                }
                catch { /* ignore normalization errors */ }
            }

            string avatarTag = "";
            int textLeftInner = 12; // default left inside button
            if (!string.IsNullOrEmpty(pfp))
            {
                // place image at left inside button (small square)
                var encodedUrl = HttpUtility.HtmlAttributeEncode(pfp);
                avatarTag = string.Format("<Image top=\"8\" left=\"8\" width=\"48\" height=\"48\" url=\"{0}\" />", encodedUrl);
                // shift text to the right of image (+ spacing)
                textLeftInner = 72;
            }
            // --------------------------------------------

            // Href to profile (compact)
            string profileHref = "page:http://172.16.40.101/SETTEMediaroomApp/ViewProfile.aspx?"
                     + "username=" + HttpUtility.UrlEncode(uname)
                     + "&user_id=" + HttpUtility.UrlEncode(meId.ToString())
                     + "&selected_user_id=" + HttpUtility.UrlEncode(memberId)
                     + "&thread_id=" + HttpUtility.UrlEncode(threadId);

            string hrefUrl = EscapeXml(profileHref);
            string btnId = "m_" + SanitizeId(memberId);

            // Compact button (profile)
            sb.AppendLine(string.Format(
                "      <Button id=\"{0}\" top=\"{1}\" left=\"{2}\" width=\"{3}\" height=\"{4}\" focusScale=\"1.05\" href=\"{5}\" background=\"argb(255,40,40,40)\">",
                HttpUtility.HtmlAttributeEncode(btnId),
                topPos,
                leftCol,
                compactWidth,
                compactHeight,
                hrefUrl
            ));

            // optionally add avatar image tag
            if (!string.IsNullOrEmpty(avatarTag))
                sb.AppendLine("        " + avatarTag);

            // Full name (larger) — placed with small padding (adjusted if avatar present)
            sb.AppendLine(string.Format(
                "        <Text top=\"6\" left=\"{0}\" width=\"{1}\" height=\"28\" fontstyle=\"Reg22\" foreground=\"argb(255,255,255,255)\">{2}</Text>",
                textLeftInner,
                compactWidth - textLeftInner - 12,
                displayFull
            ));

            // Username (smaller, muted)
            sb.AppendLine(string.Format(
                "        <Text top=\"34\" left=\"{0}\" width=\"{1}\" height=\"22\" fontstyle=\"Reg18\" foreground=\"argb(255,180,180,180)\">{2}</Text>",
                textLeftInner,
                compactWidth - textLeftInner - 12,
                displayUser
            ));

            // Following label (small, right-aligned in button) if applicable
            if (isFollowing)
            {
                int badgeWidth = 88;
                int badgeLeft = leftCol + compactWidth - badgeWidth - 8; // 8px right padding
                // place near top (aligned with full name)
                sb.AppendLine(string.Format(
                    "        <Text top=\"8\" left=\"{0}\" width=\"{1}\" height=\"20\" fontstyle=\"Reg16\" foreground=\"argb(255,150,255,150)\">Following</Text>",
                    badgeLeft,
                    badgeWidth
                ));
            }

            sb.AppendLine("      </Button>");

            // ------------------------------
            // ADD action buttons to the right
            // ------------------------------
            // Message button (always shown) — opens NewMessage.aspx with to_userid, to_username and to_full_name
           // Original:
           string memberThreadId = member["user_id"] != null ? member["user_id"].ToString() : "nothreaid"; // fallback to main thread ID if not provided per member

string messageHref = "page:http://172.16.40.101/SETTEMediaroomApp/NewMessage.aspx?"
                     + "to_userid=" + HttpUtility.UrlEncode(memberId)
                     + "&to_username=" + HttpUtility.UrlEncode(uname)
                     + "&to_full_name=" + HttpUtility.UrlEncode(fullName)
                     + "&userid=" + HttpUtility.UrlEncode(meId.ToString())
                     + "&thread_id=" + HttpUtility.UrlEncode(memberThreadId)
                     + "&thread_name=" + HttpUtility.UrlEncode(threadName);

            string msgBtnId = "msg_" + SanitizeId(memberId);
            sb.AppendLine(string.Format(
                "      <Button id=\"{0}\" top=\"{1}\" left=\"{2}\" width=\"{3}\" height=\"{4}\" focusScale=\"1.05\" href=\"{5}\" background=\"argb(255,30,30,60)\">",
                HttpUtility.HtmlAttributeEncode(msgBtnId),
                topPos,
                actionLeftBase,
                actionWidth,
                actionHeight,
                EscapeXml(messageHref)
            ));
            sb.AppendLine("        <Text top=\"18\" left=\"10\" width=\"80\" height=\"28\" fontstyle=\"Reg18\">Message</Text>");
            sb.AppendLine("      </Button>");

            // Follow / Unfollow button (only show if viewer is logged in)
            if (meId > 0 && meId.ToString() != memberId)
            {
                // NOTE: adjust FollowAction.aspx (or your API endpoint) as needed for your backend.
                string followAction = isFollowing ? "unfollow" : "follow";
                string followHref = "page:http://172.16.40.101/SETTEMediaroomApp/FollowAction.aspx?"
                                    + "userid=" + HttpUtility.UrlEncode(meId.ToString())
                                    + "&target_userid=" + HttpUtility.UrlEncode(memberId)
                                    + "&action=" + HttpUtility.UrlEncode(followAction)
                                    + (string.IsNullOrEmpty(querySecret) ? "" : "&query_secret=" + HttpUtility.UrlEncode(querySecret));

                string followBtnId = (isFollowing ? "unf_" : "f_") + SanitizeId(memberId);
                int followLeft = actionLeftBase + actionWidth + 8; // place to the right of message button

                sb.AppendLine(string.Format(
                    "      <Button id=\"{0}\" top=\"{1}\" left=\"{2}\" width=\"{3}\" height=\"{4}\" focusScale=\"1.05\" href=\"{5}\" background=\"argb(255,60,30,30)\">",
                    HttpUtility.HtmlAttributeEncode(followBtnId),
                    topPos,
                    followLeft,
                    actionWidth,
                    actionHeight,
                    EscapeXml(followHref)
                ));
                sb.AppendLine(string.Format(
                    "        <Text top=\"18\" left=\"10\" width=\"{0}\" height=\"28\" fontstyle=\"Reg18\">{1}</Text>",
                    actionWidth - 20,
                    isFollowing ? "Unfollow" : "Follow"
                ));
                sb.AppendLine("      </Button>");
            }
            else if (meId == 0)
            {
                // If not logged in, show a small "Login to message" hint button that opens the login/profile page
                string loginHref = "page:http://172.16.40.101/SETTEMediaroomApp/Login.aspx";
                string loginBtnId = "loginhint_" + SanitizeId(memberId);
                int loginLeft = actionLeftBase + actionWidth + 8;
                sb.AppendLine(string.Format(
                    "      <Button id=\"{0}\" top=\"{1}\" left=\"{2}\" width=\"{3}\" height=\"{4}\" focusScale=\"1.05\" href=\"{5}\" background=\"argb(255,80,80,80)\">",
                    HttpUtility.HtmlAttributeEncode(loginBtnId),
                    topPos,
                    loginLeft,
                    actionWidth,
                    actionHeight,
                    EscapeXml(loginHref)
                ));
                sb.AppendLine("        <Text top=\"18\" left=\"10\" width=\"80\" height=\"28\" fontstyle=\"Reg18\">Login</Text>");
                sb.AppendLine("      </Button>");
            }

            // move to next row
            topPos += compactHeight + spacingY;
        }

     // Load More button (handled by ASPX paging)
int shownSoFar = offset + pageMembers.Count;
if (shownSoFar < totalMembers)
{
    int nextPage = page + 1;

    // Добави thread_name во URL-то (URL encode + XML escape)
    string loadMoreHref = "page:http://172.16.40.101/SETTEMediaroomApp/ThreadMembers.aspx?"
                          + "userid=" + HttpUtility.UrlEncode(meId.ToString())
                          + "&thread_id=" + HttpUtility.UrlEncode(threadId)
                          + "&thread_name=" + HttpUtility.UrlEncode(threadName) // <-- thread name
                          + "&page=" + HttpUtility.UrlEncode(nextPage.ToString());

    sb.AppendLine(string.Format(
        "      <Button id=\"LoadMore\" top=\"{0}\" left=\"{1}\" width=\"{2}\" height=\"{3}\" focusScale=\"1.05\" href=\"{4}\">",
        topPos, leftCol, compactWidth, 56, EscapeXml(loadMoreHref)
    ));
    sb.AppendLine("        <Text top=\"10\" left=\"10\" width=\"280\" height=\"36\" fontstyle=\"Reg24\">Load More</Text>");
    sb.AppendLine("      </Button>");
}
        else
        {
            sb.AppendLine(string.Format(
                "      <Text top=\"{0}\" left=\"{1}\" width=\"{2}\" height=\"36\" fontstyle=\"Reg20\">No more members</Text>",
                topPos, leftCol, compactWidth
            ));
        }

        sb.AppendLine("    </Panel>");
        sb.AppendLine("  </MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private string EscapeXml(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
    }

    // Helper to produce safe element IDs (letters, digits, underscore)
    private string SanitizeId(string input)
    {
        if (string.IsNullOrEmpty(input)) return "unknown";
        var sb = new StringBuilder();
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
}