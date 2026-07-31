using System;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace PFTvBills
{
    public partial class ViewProfilePage : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
            Response.Cache.SetNoStore();

            string hostAbsolute = "http://172.16.40.101";
            string username = Request.QueryString["username"];
            if (string.IsNullOrEmpty(username)) username = "luka.utevski_thcffan";

            // Accept either user_id or userid (viewer)
            string userId = Request.QueryString["user_id"];
            if (string.IsNullOrEmpty(userId))
                userId = Request.QueryString["userid"];
            if (string.IsNullOrEmpty(userId)) userId = "1003";

            // Accept either selected_user_id or target_userid (profile being viewed)
            string selectedUserId = Request.QueryString["selected_user_id"];
            if (string.IsNullOrEmpty(selectedUserId))
                selectedUserId = Request.QueryString["target_userid"];
            if (string.IsNullOrEmpty(selectedUserId)) selectedUserId = userId;

            // **bio**: toggle full bio view via query param show_full_bio=1
            bool showFullBio = false;
            if (!string.IsNullOrEmpty(Request.QueryString["show_full_bio"]))
            {
                bool.TryParse(Request.QueryString["show_full_bio"], out showFullBio);
            }

            int start = 0;
            int postsToShow = 5;
            if (!string.IsNullOrEmpty(Request.QueryString["start"]))
            {
                int.TryParse(Request.QueryString["start"], out start);
            }

            // --- Fetch profile info ---
            string apiUrl = string.Format(
                "http://172.16.40.100/get_profile_info.php?username={0}&user_id={1}&query_secret=supersecure123",
                HttpUtility.UrlEncode(username),
                HttpUtility.UrlEncode(selectedUserId)
            );

            JObject profileData = null;

            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    string json = wc.DownloadString(apiUrl);
                    profileData = JObject.Parse(json);
                }
            }
            catch
            {
                profileData = new JObject();
            }

            // --- Defaults ---
            string profilePicture = "AppImages/default_profile.png";
            string fullName = "Unknown";
            string user = username;
            JArray posts = new JArray();
            bool isFollowing = false;

            // **bio**
            string bio = "";

            // instagram field (raw)
            string instagram = "";

            // additional fields
            string gymName = "";
            string github = "";
            string gymLocation = "";
            string dateOfBirth = "";
            string gender = "";

            if (profileData != null)
            {
                JToken token;

                if (profileData.TryGetValue("profile_picture_url", out token) && token != null)
                {
                    profilePicture = token.ToString();
                    profilePicture = profilePicture.Replace("https://lukaserver.ddns.net", "http://172.16.40.100")
                                                   .Replace("http://lukaserver.ddns.net", "http://172.16.40.100");
                }

                if (profileData.TryGetValue("full_name", out token) && token != null)
                    fullName = token.ToString();

                if (profileData.TryGetValue("username", out token) && token != null)
                    user = token.ToString();

                if (profileData.TryGetValue("posts", out token) && token != null && token.Type == JTokenType.Array)
                    posts = (JArray)token;

                if (profileData.TryGetValue("is_following", out token) && token != null)
                    bool.TryParse(token.ToString(), out isFollowing);

                // **bio**: read if present (raw), will be truncated & escaped later
                if (profileData.TryGetValue("bio", out token) && token != null)
                    bio = token.ToString();

                // **instagram**: read if present
                if (profileData.TryGetValue("instagram", out token) && token != null)
                    instagram = token.ToString();

                // Additional fields: gym_name, github, gym_location, date_of_birth, gender
                if (profileData.TryGetValue("gym_name", out token) && token != null)
                    gymName = token.ToString();
                if (profileData.TryGetValue("github", out token) && token != null)
                    github = token.ToString();
                if (profileData.TryGetValue("gym_location", out token) && token != null)
                    gymLocation = token.ToString();
                if (profileData.TryGetValue("date_of_birth", out token) && token != null)
                    dateOfBirth = token.ToString();
                if (profileData.TryGetValue("gender", out token) && token != null)
                    gender = token.ToString();

                // If instagram missing in JSON, use username as fallback (keep plain, no @)
                if (string.IsNullOrEmpty(instagram) && !string.IsNullOrEmpty(user))
                {
                    instagram = user;
                }
            }

          // Проверка дали корисникот постои
if (profileData == null || (profileData["username"] == null && profileData["full_name"] == null))
{
    // редирект кон ViewInstagramProfile.aspx со истиот username и user_id
    var qs = HttpUtility.ParseQueryString(string.Empty);
    qs["username"] = username;
    qs["selected_user_id"] = userId; 
    string redirectUrl = "ViewInstagramProfile.aspx?" + qs.ToString();
    
    Response.Redirect(redirectUrl, true);
    return;
}

            // --- Fetch friendship status (following / followed_by) ---
            bool isFollowedBy = false;

            try
            {
                string friendshipUrl = string.Format(
                    "http://172.16.40.100/friendship_status.php?query_secret=supersecure123&me_id={0}&user_id={1}",
                    HttpUtility.UrlEncode(userId),         // viewer ID (logged-in / me_id)
                    HttpUtility.UrlEncode(selectedUserId)  // target profile ID
                );

                using (WebClient wc = new WebClient())
                {
                    string json = wc.DownloadString(friendshipUrl);
                    JObject friendshipData = JObject.Parse(json);

                    // parse boolean values
                    if (friendshipData["following"] != null)
                        bool.TryParse(friendshipData["following"].ToString(), out isFollowing);
                    if (friendshipData["followed_by"] != null)
                        bool.TryParse(friendshipData["followed_by"].ToString(), out isFollowedBy);
                }
            }
            catch
            {
                // default false if any error occurs
                isFollowing = false;
                isFollowedBy = false;
            }

            // --- Fetch followers info ---
            JObject followedByData = null;
            try
            {
                string followedByUrl = string.Format(
                    "http://172.16.40.100/followed_by_text.php?target_user_id={0}",
                    HttpUtility.UrlEncode(selectedUserId)
                );
                using (WebClient wc = new WebClient())
                {
                    string json = wc.DownloadString(followedByUrl);
                    followedByData = JObject.Parse(json);
                }
            }
            catch
            {
                followedByData = new JObject();
            }

            string followersText = "";
            int followersCount = 0;

            if (followedByData != null)
            {
                if (followedByData["text"] != null)
                    followersText = EscapeXml(followedByData["text"].ToString());

                if (followedByData["followers_count"] != null)
                    int.TryParse(followedByData["followers_count"].ToString(), out followersCount);
            }

            JArray followersArray = new JArray();
            if (followedByData != null && followedByData["followed_by_user_info"] != null && followedByData["followed_by_user_info"].Type == JTokenType.Array)
            {
                followersArray = (JArray)followedByData["followed_by_user_info"];
            }

            // Escape XML for static fields
            profilePicture = EscapeXml(profilePicture);
            fullName = EscapeXml(fullName);
            user = EscapeXml(user);
            instagram = EscapeXml(instagram); // escape instagram value

            // Escape added fields
            gymName = EscapeXml(gymName);
            github = EscapeXml(github);
            gymLocation = EscapeXml(gymLocation);
            dateOfBirth = EscapeXml(dateOfBirth);
            gender = EscapeXml(gender);

            // normalize instagram handle to use for the button (guaranteed fallback to username)
            string igHandle = NormalizeInstagramHandle(HttpUtility.UrlDecode(instagram));
            if (string.IsNullOrEmpty(igHandle))
            {
                // fallback to username value (raw)
                igHandle = NormalizeInstagramHandle(HttpUtility.UrlDecode(user));
            }
            if (string.IsNullOrEmpty(igHandle))
            {
                // final fallback: the original query param
                igHandle = NormalizeInstagramHandle(username);
            }

            // **bio**: normalize and either truncate or show full, then escape
            string normalizedBio = "";
            string bioToShowEscaped = "";
            if (!string.IsNullOrEmpty(bio))
            {
                // normalize newlines to spaces and trim
                normalizedBio = bio.Replace("\r\n", " ").Replace("\n", " ").Trim();

                if (showFullBio)
                {
                    // full normalized bio (we will render inline with mentions)
                    bioToShowEscaped = EscapeXml(normalizedBio);
                }
                else
                {
                    // truncate to 120 chars (adjustable) with ellipsis
                    string truncatedRaw = Truncate(normalizedBio, 120);
                    bioToShowEscaped = EscapeXml(truncatedRaw);
                }
            }

            // find @mentions (unique) from normalizedBio, without the @ sign
            var mentions = new List<string>();
            if (!string.IsNullOrEmpty(normalizedBio))
            {
                try
                {
                    foreach (Match m in Regex.Matches(normalizedBio, @"@([A-Za-z0-9_.\-]+)"))
                    {
                        if (m.Groups.Count > 1)
                        {
                            string uname = m.Groups[1].Value;
                            if (!string.IsNullOrEmpty(uname))
                                mentions.Add(uname);
                        }
                    }
                    mentions = mentions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }
                catch
                {
                    mentions = new List<string>();
                }
            }

            // --- Fullscreen About with inline clickable @mentions (buttons sized like text) ---
            if (showFullBio)
            {
                StringBuilder fullMrml = new StringBuilder();
                fullMrml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                fullMrml.Append("<uidescription version=\"3.0\">");
                fullMrml.Append("<MrmlPage id=\"BioFullScreen\" width=\"1280\" height=\"720\">");
                fullMrml.Append("<Panel id=\"MainPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"720\">");

                // Title
                string title = !string.IsNullOrEmpty(fullName) && fullName != "Unknown" ? fullName + " - About" : "About @" + HttpUtility.HtmlEncode(username);
                fullMrml.AppendFormat("<Text left=\"40\" top=\"20\" fontstyle=\"Reg36\" foreground=\"argb(255,255,255,255)\">{0}</Text>", EscapeXml(title));

                // Full bio area: render inline with clickable mentions
                int bioLeft = 40;
                int fullBioTop = 80;
                int bioWidth = 1200;   // available width for bio area
                int curX = bioLeft;
                int curY = fullBioTop;
                int charWidth = 12;    // approximate char width for Reg20 in fullscreen (tune if needed)
                int lineHeight = 30;   // line height for Reg20

                // Use the full normalizedBio (unescaped for splitting)
                string displayRaw = normalizedBio ?? "";

                // Split into parts keeping mentions as separate tokens
                var parts = Regex.Split(displayRaw, @"(@[A-Za-z0-9_.\-]+)");

                string lineBuf = ""; // buffer for the current plain-text fragment on the current line
                int seg = 0;
                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part)) { seg++; continue; }

                    if (part.StartsWith("@"))
                    {
                        // Flush current text buffer before inserting mention
                        if (!string.IsNullOrEmpty(lineBuf))
                        {
                            int textW = lineBuf.Length * charWidth;
                            // if text doesn't fit current line, move to next line first
                            if (curX + textW > bioLeft + bioWidth)
                            {
                                curX = bioLeft;
                                curY += lineHeight;
                            }

                            fullMrml.AppendFormat("<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,230,230,230)\">{2}</Text>",
                                curX, curY, EscapeXml(lineBuf));
                            curX += textW + 4; // small spacing after text
                            lineBuf = "";
                        }

                        // Now place the mention button sized to match text
                        string mentionWithAt = part;         // e.g. "@luka"
                        string mentionName = part.Substring(1);
                        int mentionWidth = mentionWithAt.Length * charWidth; // width matching text characters
                        int mentionHeight = lineHeight; // height matching text line

                        // wrap if mention doesn't fit
                        if (curX + mentionWidth > bioLeft + bioWidth)
                        {
                            curX = bioLeft;
                            curY += lineHeight;
                        }

                        var q = HttpUtility.ParseQueryString(string.Empty);
                        q["username"] = mentionName;
                        q["user_id"] = userId;
                        q["userid"] = userId;
                        string href = "page:http://172.16.40.101/SETTEMediaroomApp/ViewProfile.aspx?" + q.ToString();
                        href = EscapeXml(href);

                        // Button text left as @mention; height and width match the text block
                        fullMrml.AppendFormat("<Button id=\"FullBioMention{0}\" left=\"{1}\" top=\"{2}\" width=\"{3}\" height=\"{4}\" href=\"{5}\">{6}</Button>",
                            seg, curX, curY, mentionWidth, mentionHeight, href, EscapeXml(mentionWithAt));

                        curX += mentionWidth + 4; // small spacing after mention
                    }
                    else
                    {
                        // Plain text: split into words and add to lineBuf with wrapping
                        var words = part.Split(new[] { ' ' }, StringSplitOptions.None);
                        foreach (var w in words)
                        {
                            if (string.IsNullOrEmpty(w))
                            {
                                // preserve spaces by adding a single space if there's already text
                                if (!string.IsNullOrEmpty(lineBuf)) lineBuf += " ";
                                continue;
                            }

                            string tryLine = string.IsNullOrEmpty(lineBuf) ? w : (lineBuf + " " + w);
                            int tryW = tryLine.Length * charWidth;

                            if (curX + tryW > bioLeft + bioWidth)
                            {
                                // flush existing lineBuf (if any)
                                if (!string.IsNullOrEmpty(lineBuf))
                                {
                                    int textW = lineBuf.Length * charWidth;
                                    // if it somehow doesn't fit alone, still place on new line
                                    if (curX + textW > bioLeft + bioWidth)
                                    {
                                        curX = bioLeft;
                                        curY += lineHeight;
                                    }
                                    fullMrml.AppendFormat("<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,230,230,230)\">{2}</Text>",
                                        curX, curY, EscapeXml(lineBuf));
                                    // move to next line
                                    curY += lineHeight;
                                    curX = bioLeft;
                                }
                                // start new buffer with current word
                                lineBuf = w;
                            }
                            else
                            {
                                lineBuf = tryLine;
                            }
                        }
                        // do NOT flush lineBuf here — keep it for next token (could be mention)
                    }

                    seg++;
                }

                // flush remaining lineBuf
                if (!string.IsNullOrEmpty(lineBuf))
                {
                    int textW = lineBuf.Length * charWidth;
                    if (curX + textW > bioLeft + bioWidth)
                    {
                        curX = bioLeft;
                        curY += lineHeight;
                    }
                    fullMrml.AppendFormat("<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,230,230,230)\">{2}</Text>",
                        curX, curY, EscapeXml(lineBuf));
                    curX += textW + 4;
                    lineBuf = "";
                }

                // --- Insert additional profile fields below bio block ---
                int infoTop = curY + lineHeight + 20; // place below rendered bio
                int infoLeft = bioLeft;
                int infoLineHeight = 28;

                if (!string.IsNullOrEmpty(gymName))
                {
                    fullMrml.AppendFormat("<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">Gym: {2}</Text>",
                        infoLeft, infoTop, gymName);
                    infoTop += infoLineHeight;
                }

                if (!string.IsNullOrEmpty(gymLocation))
                {
                    fullMrml.AppendFormat("<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">Location: {2}</Text>",
                        infoLeft, infoTop, gymLocation);
                    infoTop += infoLineHeight;
                }

// --- Declare gym coordinates early ---
double gymLat = 0.0;
double gymLng = 0.0;

JToken token;

// Get gym latitude
if (profileData.TryGetValue("gym_lat", out token) && token != null)
{
    // If token is numeric
    gymLat = (double)token;
}

// Get gym longitude
if (profileData.TryGetValue("gym_lng", out token) && token != null)
{
    gymLng = (double)token;
}

// --- Gym Location Button ---
if (!string.IsNullOrEmpty(gymName) && gymLat != 0 && gymLng != 0)
{
    // Build the lukifymap URL
    string gymMapUrl = string.Format(
        "lukify://users_map?location_name={0}&location_latitude={1}&location_longitude={2}&",
        HttpUtility.UrlEncode(gymName),
        gymLat.ToString(System.Globalization.CultureInfo.InvariantCulture),
        gymLng.ToString(System.Globalization.CultureInfo.InvariantCulture)
    );

    // Encode it for passing as a query parameter
    string sendLinkUrl = string.Format(
        "SendLinkToPhone.aspx?user_id={0}&url={1}",
        HttpUtility.UrlEncode(userId),
        HttpUtility.UrlEncode(gymMapUrl)
    );

    // Add the button
    fullMrml.AppendFormat(
        "<Button id=\"SendGymMap\" left=\"{0}\" top=\"{1}\" width=\"300\" height=\"40\" href=\"{2}\">Send Gym Map to Phone</Button>",
        infoLeft, infoTop, EscapeXml(sendLinkUrl)
    );

// --- Waze Navigation Button ---
if (!string.IsNullOrEmpty(gymName) && gymLat != 0 && gymLng != 0)
{
    string wazeDeepLink = string.Format(
        "waze://?ll={0},{1}&navigate=yes",
        gymLat.ToString(System.Globalization.CultureInfo.InvariantCulture),
        gymLng.ToString(System.Globalization.CultureInfo.InvariantCulture)
    );

    string sendWazeLinkUrl = string.Format( // <-- сменето име
        "SendLinkToPhone.aspx?user_id={0}&url={1}",
        HttpUtility.UrlEncode(userId),
        HttpUtility.UrlEncode(wazeDeepLink)
    );

    fullMrml.AppendFormat(
        "<Button id=\"OpenGymInWaze\" left=\"{0}\" top=\"5\" width=\"300\" height=\"40\" href=\"{2}\">Open in Waze</Button>",
        infoLeft,
        infoTop + 50,
        EscapeXml(sendWazeLinkUrl)
    );
}
    // Optional: show coordinates
    fullMrml.AppendFormat(
        "<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg16\" foreground=\"argb(255,180,180,180)\">{2}</Text>",
        infoLeft + 310, infoTop + 10,
        EscapeXml(gymName)
    );
}
                if (!string.IsNullOrEmpty(dateOfBirth))
                {
                    fullMrml.AppendFormat("<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">DOB: {2}</Text>",
                        infoLeft, infoTop, dateOfBirth);
                    infoTop += infoLineHeight;
                }

                if (!string.IsNullOrEmpty(gender))
                {
                    fullMrml.AppendFormat("<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">Gender: {2}</Text>",
                        infoLeft, infoTop, gender);
                    infoTop += infoLineHeight;
                }

                // Github -> use SendLinkToPhone.aspx so it sends to phone (userstring + user_id + deviceguid placeholder + url)
                if (!string.IsNullOrEmpty(github))
                {
                    // normalize github link to absolute
                    string gh = github;
                    if (!gh.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !gh.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        gh = gh.Trim();
                        if (!gh.StartsWith("github.com", StringComparison.OrdinalIgnoreCase))
                            gh = "https://github.com/" + gh.Trim('/');
                        else
                            gh = "https://" + gh.Trim('/');
                    }

                    string ghEncoded = HttpUtility.UrlEncode(gh);
                    string sendLinkHref = string.Format(
                        "page:{0}/SETTEMediaroomApp/SendLinkToPhone.aspx?userstring={1}&user_id={2}&deviceguid={{DeviceGuid}}&url={3}",
                        hostAbsolute, HttpUtility.UrlEncode(user), HttpUtility.UrlEncode(userId), ghEncoded
                    );
                    sendLinkHref = EscapeXml(sendLinkHref);

                    fullMrml.AppendFormat("<Button id=\"GithubButton\" left=\"{0}\" top=\"{1}\" width=\"420\" height=\"34\" href=\"{2}\">Send GitHub to Phone</Button>",
                        infoLeft, infoTop, sendLinkHref);
                    infoTop += infoLineHeight;
                }

                // Back button and Instagram visit button
                int backLeft = 1040;
                int backTop = 640;
                var backQs = HttpUtility.ParseQueryString(string.Empty);
                backQs["username"] = username;
                backQs["user_id"] = userId;
                backQs["userid"] = userId;
                backQs["selected_user_id"] = selectedUserId;
                backQs["show_full_bio"] = "false";
                string backUrl = "page:http://172.16.40.101/SETTEMediaroomApp/ViewProfile.aspx?" + backQs.ToString();
                backUrl = EscapeXml(backUrl);

                // Always add Visit Instagram button (use igHandle computed earlier)
                if (!string.IsNullOrEmpty(igHandle))
                {
                    var igQs = HttpUtility.ParseQueryString(string.Empty);
                    igQs["username"] = igHandle;
                    igQs["user_id"] = userId;
                    igQs["userid"] = userId;
                    igQs["selected_user_id"] = selectedUserId;

                    string igUrlFull = "page:http://172.16.40.101/SETTEMediaroomApp/ViewInstagramProfile.aspx?" + igQs.ToString();
                    igUrlFull = EscapeXml(igUrlFull);

                    int visitLeft = backLeft - 200; // place left of Back
                    if (visitLeft < 40) visitLeft = 40;
                    int visitTop = backTop;
                    int visitWidth = 180;
                    int visitHeight = 56;

                    // label that includes the handle (no @)
                    string visitLabelFull = "Visit Instagram \"" + EscapeXml(igHandle) + "\"";

                    fullMrml.AppendFormat("<Button id=\"VisitInstagramFull\" left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" href=\"{4}\">{5}</Button>",
                        visitLeft, visitTop, visitWidth, visitHeight, igUrlFull, visitLabelFull);
                }

                fullMrml.AppendFormat("<Button id=\"BackFromBio\" left=\"{0}\" top=\"{1}\" width=\"180\" height=\"56\" href=\"{2}\">Back</Button>", backLeft, backTop, backUrl);

                fullMrml.Append("</Panel></MrmlPage></uidescription>");

                Response.Write(fullMrml.ToString());
                Response.Flush();
                HttpContext.Current.ApplicationInstance.CompleteRequest();
                return;
            }

            // Dynamic profile title
            string profileTitle = !string.IsNullOrEmpty(fullName) && fullName != "Unknown"
                ? fullName + "&#39;s Lukify Profile"
                : user + "&#39;s Lukify Profile";

            // --- Build MRML (compact view) ---
            StringBuilder mrml = new StringBuilder();
            mrml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            mrml.Append("<uidescription version=\"3.0\">");
            mrml.Append("<MrmlPage id=\"ViewProfilePage\" width=\"1280\" height=\"720\">");
            mrml.Append("<Panel id=\"MainPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"720\">");

            // Add DataSource + DeviceGuid so {DeviceGuid} is available for SendLinkToPhone links
            mrml.Append("<DataSource id=\"SystemInfo\" uri=\"local://system-info\" />");
            mrml.Append("<EditText id=\"DeviceGuid\" visible=\"false\" datasource=\"{Binding Source=SystemInfo,Path=DeviceId}\" />");

            // Profile title
            mrml.AppendFormat(
                "<Text left=\"60\" top=\"20\" fontstyle=\"Reg36\" foreground=\"argb(255,255,255,255)\">{0}</Text>",
                profileTitle
            );

            // Profile picture and name
            mrml.AppendFormat("<Image left=\"60\" top=\"60\" width=\"180\" height=\"180\" url=\"{0}\" />", profilePicture);
            mrml.AppendFormat("<Text left=\"260\" top=\"70\" fontstyle=\"Reg32\" foreground=\"argb(255,226,0,116)\">{0}</Text>", fullName);
            mrml.AppendFormat("<Text left=\"260\" top=\"115\" fontstyle=\"Reg26\" foreground=\"argb(255,255,255,255)\">@{0}</Text>", user);

            // **bio**: compact profile bio (plain/truncated text)
            int followersTop = 150;
            int bioTop = 140;

            if (!string.IsNullOrEmpty(bioToShowEscaped))
            {
                mrml.AppendFormat("<Text left=\"260\" top=\"{0}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">{1}</Text>", bioTop, bioToShowEscaped);
            }
            else
            {
                // if no bio, show a small placeholder (keeps layout consistent)
                mrml.AppendFormat("<Text left=\"260\" top=\"{0}\" fontstyle=\"Reg20\" foreground=\"argb(255,150,150,150)\">No bio provided.</Text>", bioTop);
            }

            // Build toggling URL using same base ViewProfile.aspx and current query params
            var bioQs = HttpUtility.ParseQueryString(string.Empty);
            bioQs["username"] = username;
            bioQs["user_id"] = userId;
            bioQs["userid"] = userId;
            bioQs["selected_user_id"] = selectedUserId;
            bioQs["show_full_bio"] = showFullBio ? "false" : "true";
            string toggleBioUrl = "page:http://172.16.40.101/SETTEMediaroomApp/ViewProfile.aspx?" + bioQs.ToString();
            toggleBioUrl = EscapeXml(toggleBioUrl);

            string toggleLabel = showFullBio ? "Show Less" : "Show More";

            // place the toggle button under the bio text (left-aligned with the bio)
            int toggleLeft = 260;                  // same left as the bio text
            int toggleTop = bioTop + 40;           // placed below bio
            int toggleWidth = 120;
            int toggleHeight = 30;

            mrml.AppendFormat(
                "<Button id=\"ToggleBioButton\" left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" href=\"{4}\">{5}</Button>",
                toggleLeft, toggleTop, toggleWidth, toggleHeight, toggleBioUrl, toggleLabel
            );

            // Compact Visit Instagram button next to toggle (use igHandle)
            if (!string.IsNullOrEmpty(igHandle))
            {
                var igQs = HttpUtility.ParseQueryString(string.Empty);
                igQs["username"] = igHandle;
                igQs["user_id"] = userId;
                igQs["userid"] = userId;
                igQs["selected_user_id"] = selectedUserId;

                string igUrl = "page:http://172.16.40.101/SETTEMediaroomApp/ViewInstagramProfile.aspx?" + igQs.ToString();
                igUrl = EscapeXml(igUrl);

                int igLeft = toggleLeft + toggleWidth + 10;
                int igTop = toggleTop;
                int igWidth = 160;
                int igHeight = 30;

                string visitLabelCompact = "Visit Instagram \"" + EscapeXml(igHandle) + "\"";

                mrml.AppendFormat(
                    "<Button id=\"VisitInstagramCompact\" left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" href=\"{4}\">{5}</Button>",
                    igLeft, igTop, igWidth, igHeight, igUrl, visitLabelCompact
                );
            }

            // Compact extra info under the bio (always show these fields, even if no bio)
            int compactTop = toggleTop + toggleHeight + 10;
            int compactLeft = 260;

            if (!string.IsNullOrEmpty(gymName))
            {
                mrml.AppendFormat("<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">Gym: {2}</Text>", compactLeft, compactTop, gymName);
                compactTop += 28;
            }

            if (!string.IsNullOrEmpty(gymLocation))
            {
                mrml.AppendFormat("<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">Location: {2}</Text>", compactLeft, compactTop, gymLocation);
                compactTop += 28;
            }

            if (!string.IsNullOrEmpty(dateOfBirth))
            {
                mrml.AppendFormat("<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">DOB: {2}</Text>", compactLeft, compactTop, dateOfBirth);
                compactTop += 28;
            }

            if (!string.IsNullOrEmpty(gender))
            {
                mrml.AppendFormat("<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">Gender: {2}</Text>", compactLeft, compactTop, gender);
                compactTop += 28;
            }

            // GitHub compact button: send GitHub link to phone via SendLinkToPhone.aspx (userstring + user_id + deviceguid + url)
            if (!string.IsNullOrEmpty(github))
            {
                string gh = github;
                if (!gh.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !gh.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    gh = gh.Trim();
                    if (!gh.StartsWith("github.com", StringComparison.OrdinalIgnoreCase))
                        gh = "https://github.com/" + gh.Trim('/');
                    else
                        gh = "https://" + gh.Trim('/');
                }

                string ghEncoded = HttpUtility.UrlEncode(gh);
                string sendLinkHrefCompact = string.Format(
                    "page:{0}/SETTEMediaroomApp/SendLinkToPhone.aspx?userstring={1}&user_id={2}&deviceguid={{DeviceGuid}}&url={3}",
                    hostAbsolute, HttpUtility.UrlEncode(user), HttpUtility.UrlEncode(userId), ghEncoded
                );
                sendLinkHrefCompact = EscapeXml(sendLinkHrefCompact);

                mrml.AppendFormat("<Button id=\"GithubCompact\" left=\"{0}\" top=\"{1}\" width=\"220\" height=\"30\" href=\"{2}\">Send GitHub to Phone</Button>", compactLeft + 300, toggleTop, sendLinkHrefCompact);
            }

            followersTop = compactTop + 10;

            // Followers text + count
            if (!string.IsNullOrEmpty(followersText))
            {
                mrml.AppendFormat(
                    "<Text left=\"260\" top=\"{0}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">{1}</Text>",
                    followersTop, followersText
                );

                if (followersCount > 0)
                {
                    // Build URL for followers page
                    var followersQs = HttpUtility.ParseQueryString(string.Empty);
                    followersQs["user_id"] = userId;
                    followersQs["userid"] = userId;
                    followersQs["selected_user_id"] = selectedUserId;
                    string followersPageUrl = "page:http://172.16.40.101/SETTEMediaroomApp/FollowersList.aspx?" + followersQs.ToString();
                    followersPageUrl = EscapeXml(followersPageUrl);

                    int followersBtnTop = followersTop + 25;
                    int followersBtnLeft = 260;
                    int followersBtnWidth = 200; // adjust width as needed
                    int followersBtnHeight = 30;

                    mrml.AppendFormat(
                        "<Button id=\"FollowersCountButton\" left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" href=\"{4}\">Followers: {5}</Button>",
                        followersBtnLeft, followersBtnTop, followersBtnWidth, followersBtnHeight, followersPageUrl, followersCount
                    );
                }

                // followers as clickable images
                int followerLeft = 260;
                int followerTop = followersTop + 50;
                int followerSize = 40;
                int followerSpacing = 10;
                int displayedFollowers = Math.Min(5, followersArray.Count);

                for (int i = 0; i < displayedFollowers; i++)
                {
                    JObject follower = followersArray[i] as JObject;
                    if (follower == null) continue;

                    string followerPic = follower["profile_picture_url"] != null ? follower["profile_picture_url"].ToString() : "";
                    followerPic = followerPic.Replace("https://lukaserver.ddns.net", "http://172.16.40.100")
                                               .Replace("http://lukaserver.ddns.net", "http://172.16.40.100");
                    followerPic = EscapeXml(followerPic);

                    string followerUsername = follower["username"] != null ? follower["username"].ToString() : "";
                    string followerUserId = follower["user_id"] != null ? follower["user_id"].ToString() : "";
                    if (string.IsNullOrEmpty(followerUserId) && follower["id"] != null) followerUserId = follower["id"].ToString();

                    var fQs = HttpUtility.ParseQueryString(string.Empty);
                    if (!string.IsNullOrEmpty(followerUsername)) fQs["username"] = followerUsername;
                    if (!string.IsNullOrEmpty(followerUserId)) fQs["selected_user_id"] = followerUserId;
                    fQs["user_id"] = userId;
                    fQs["userid"] = userId;

                    string followerHref = "page:http://172.16.40.101/SETTEMediaroomApp/ViewProfile.aspx?" + fQs.ToString();
                    followerHref = EscapeXml(followerHref);

                    mrml.AppendFormat(
                        "<Button id=\"FollowerBtn{0}\" left=\"{1}\" top=\"{2}\" width=\"{3}\" height=\"{3}\" href=\"{4}\">",
                        i, followerLeft, followerTop, followerSize, followerHref
                    );
                    mrml.AppendFormat("<Image left=\"0\" top=\"0\" width=\"{0}\" height=\"{0}\" url=\"{1}\" />", followerSize, followerPic);
                    mrml.Append("</Button>");

                    followerLeft += followerSize + followerSpacing;
                }
            }

            // Buttons: Follow/Unfollow + Message
            int buttonsTop = followersTop + 120;
            int buttonWidth = 120;
            int buttonHeight = 40;
            int buttonSpacing = 20;

            if (userId != selectedUserId)
            {
                // Follow / Unfollow
                string followTextBtn = isFollowing ? "Following" : "Follow";
                var followQs2 = HttpUtility.ParseQueryString(string.Empty);
                followQs2["userid"] = userId;
                followQs2["target_userid"] = selectedUserId;
                followQs2["action"] = isFollowing ? "unfollow" : "follow";

                string followUrl = "page:http://172.16.40.101/SETTEMediaroomApp/FollowAction.aspx?" + followQs2.ToString();
                followUrl = EscapeXml(followUrl);

                mrml.AppendFormat(
                    "<Button id=\"FollowButton\" left=\"260\" top=\"{0}\" width=\"{1}\" height=\"{2}\" href=\"{3}\">{4}</Button>",
                    buttonsTop, buttonWidth, buttonHeight, followUrl, followTextBtn
                );

                // Message
                var msgQs = HttpUtility.ParseQueryString(string.Empty);
                msgQs["userid"] = userId;
                msgQs["to_userid"] = selectedUserId;
                msgQs["to_username"] = HttpUtility.UrlEncode(user);
                msgQs["to_full_name"] = HttpUtility.UrlEncode(fullName);

                string messageUrl = "page:http://172.16.40.101/SETTEMediaroomApp/NewMessage.aspx?" + msgQs.ToString();
                messageUrl = EscapeXml(messageUrl);

                mrml.AppendFormat(
                    "<Button id=\"MessageButton\" left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" href=\"{4}\">Message</Button>",
                    260 + buttonWidth + buttonSpacing, buttonsTop, buttonWidth, buttonHeight, messageUrl
                );

            }

            // start positions and counters for posts
            int topOffset = buttonsTop + buttonHeight + 20;  // starting Y for posts
            int index = 0;                                   // post button counter

            // --- Compute how many posts to show and build header with songs/artists ---
            int totalPostsToShow = Math.Min(postsToShow, posts.Count - start);
            List<string> songList = new List<string>();

            for (int i = start; i < posts.Count && i < start + totalPostsToShow; i++)
            {
                JObject post = posts[i] as JObject;
                if (post == null) continue;

                JObject songObj = post["song"] as JObject;
                if (songObj != null && songObj["title"] != null)
                {
                    string songTitle = songObj["title"].ToString();
                    string songArtist = songObj["artist"] != null ? songObj["artist"].ToString() : "";
                    if (!string.IsNullOrEmpty(songTitle))
                    {
                        if (!string.IsNullOrEmpty(songArtist))
                            songList.Add(songTitle + " by " + songArtist);
                        else
                            songList.Add(songTitle);
                    }
                }
            }

            // --- Add Posts header ---
            string postsHeader = string.Format("Posts ({0})", totalPostsToShow);

            mrml.AppendFormat(
                "<Text id=\"PostsTitle\" top=\"{0}\" left=\"20\" width=\"900\" height=\"30\" fontstyle=\"Reg26\" foreground=\"argb(255,228,0,115)\">{1}</Text>",
                buttonsTop, EscapeXml(postsHeader)
            );

            // --- Posts rendering ---
            for (int i = start; i < posts.Count && index < postsToShow; i++)
            {
                JObject post = posts[i] as JObject;
                if (post == null) continue;

                // Caption
                string caption = post["caption"] != null ? post["caption"].ToString() : "Post";
                if (caption.StartsWith("video:", StringComparison.OrdinalIgnoreCase))
                    caption = caption.Substring(6).Trim();

                // Song info
                string songTitleLocal = "";
                string songArtistLocal = "";
                JObject songObj = post["song"] as JObject;
                if (songObj != null)
                {
                    if (songObj["title"] != null)
                        songTitleLocal = songObj["title"].ToString();
                    if (songObj["artist"] != null)
                        songArtistLocal = songObj["artist"].ToString();
                }

                if (!string.IsNullOrEmpty(songTitleLocal))
                {
                    if (!string.IsNullOrEmpty(songArtistLocal))
                        caption += " - " + songTitleLocal + " by " + songArtistLocal;
                    else
                        caption += " - " + songTitleLocal;
                }

                caption = EscapeXml(caption);

                // Video URL
                string videoUrl = post["video_url"] != null ? post["video_url"].ToString() : "";
                videoUrl = videoUrl.Replace("https://lukaserver.ddns.net", "http://172.16.40.100")
                                   .Replace("http://lukaserver.ddns.net", "http://172.16.40.100");
                videoUrl = EscapeXml(videoUrl);

                string videoIdBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(videoUrl));

                // Uploader info
                string uploaderUsername = EscapeXml(username);
                string uploaderFullname = EscapeXml(fullName);
                string uploaderAvatar = EscapeXml(profilePicture);

                JObject userObjLocal = post["user"] as JObject;
                if (userObjLocal != null)
                {
                    if (userObjLocal["username"] != null)
                        uploaderUsername = EscapeXml(userObjLocal["username"].ToString());
                    if (userObjLocal["full_name"] != null)
                        uploaderFullname = EscapeXml(userObjLocal["full_name"].ToString());
                    if (userObjLocal["profile_picture_url"] != null)
                    {
                        uploaderAvatar = EscapeXml(
                            userObjLocal["profile_picture_url"].ToString()
                                .Replace("https://lukaserver.ddns.net", "http://172.16.40.100")
                                .Replace("http://lukaserver.ddns.net", "http://172.16.40.100")
                        );
                    }
                }

                string usernameBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(uploaderUsername));
                string fullnameBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(uploaderFullname));
                string avatarBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(uploaderAvatar));
                string songNameBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(songTitleLocal));

                string hrefUrl = string.Format(
                    "page:http://172.16.40.101/SETTEMediaroomApp/PlayLukifyVideo.aspx?" +
                    "videoId={0}&videoName={1}&mode=following&me_id={2}&songName={3}&user_username={4}&user_fullname={5}&user_avatar={6}&PlayVideoPage=",
                    HttpUtility.UrlEncode(videoIdBase64),
                    HttpUtility.UrlEncode(Convert.ToBase64String(Encoding.UTF8.GetBytes(caption))),
                    HttpUtility.UrlEncode(userId),
                    HttpUtility.UrlEncode(songNameBase64),
                    HttpUtility.UrlEncode(usernameBase64),
                    HttpUtility.UrlEncode(fullnameBase64),
                    HttpUtility.UrlEncode(avatarBase64)
                );

                hrefUrl = EscapeXml(hrefUrl);

                mrml.AppendFormat(
                    "<Button id=\"PostButton{0}\" left=\"60\" top=\"{1}\" width=\"1160\" height=\"50\" justification=\"left\" href=\"{2}\">{3}</Button>",
                    index, topOffset, hrefUrl, caption
                );

                topOffset += 60;
                index++;
            }

            // Load More
            if (start + postsToShow < posts.Count)
            {
                var loadQs = HttpUtility.ParseQueryString(string.Empty);
                loadQs["username"] = username;
                loadQs["user_id"] = userId;
                loadQs["userid"] = userId;
                loadQs["start"] = (start + postsToShow).ToString();
                loadQs["selected_user_id"] = selectedUserId;
                loadQs["target_userid"] = selectedUserId;

                string loadMoreUrl = "page:http://172.16.40.101/SETTEMediaroomApp/ViewProfile.aspx?" + loadQs.ToString();
                loadMoreUrl = EscapeXml(loadMoreUrl);

                mrml.AppendFormat(
                    "<Button id=\"LoadMoreButton\" left=\"60\" top=\"{0}\" width=\"1160\" height=\"50\" href=\"{1}\">Load More</Button>",
                    topOffset, loadMoreUrl
                );
            }

            mrml.Append("</Panel></MrmlPage></uidescription>");

            Response.Write(mrml.ToString());
            Response.Flush();
            HttpContext.Current.ApplicationInstance.CompleteRequest();
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

        // Normalize instagram value to a handle (remove @ or extract last path segment from URL)
        private string NormalizeInstagramHandle(string instagramRaw)
        {
            if (string.IsNullOrEmpty(instagramRaw)) return "";

            string v = instagramRaw.Trim();

            // remove @ prefix
            if (v.StartsWith("@")) v = v.Substring(1);

            // if looks like URL, try to extract last path segment
            if (v.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || v.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || v.Contains("instagram.com"))
            {
                try
                {
                    Uri u = new Uri(v.Contains("://") ? v : ("https://" + v));
                    string path = u.AbsolutePath ?? "";
                    if (!string.IsNullOrEmpty(path))
                    {
                        // split by / and take last non-empty segment
                        var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0) return parts[parts.Length - 1];
                    }
                }
                catch
                {
                    // fallback: remove trailing slash and take last chunk
                    v = v.TrimEnd('/');
                    var chunks = v.Split('/');
                    return chunks[chunks.Length - 1];
                }
            }

            // otherwise return cleaned handle
            return v;
        }

        // **bio**: helper to truncate text to a maximum number of characters and add an ellipsis
        private string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || max <= 0) return "";
            if (s.Length <= max) return s;
            return s.Substring(0, max).TrimEnd() + "...";
        }
    }
}