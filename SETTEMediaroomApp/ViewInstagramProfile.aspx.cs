using System;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Web.Script.Serialization;

public partial class ViewInstagramProfile : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetNoStore();

        string username = Request.QueryString["username"];
        string artist = Request.QueryString["artist"];
        string lukifyuserid = Request.QueryString["selected_user_id"];
        string view = Request.QueryString["view"];
        string refresh = Request.QueryString["refresh"];

        if (!string.IsNullOrEmpty(artist))
        {
            string resolvedUsername;
            string artistError;

            if (!ResolveUsernameFromArtist(artist, out resolvedUsername, out artistError))
            {
                RenderErrorPage(username, lukifyuserid, !string.IsNullOrEmpty(artistError) ? artistError : "invalid username from artist", artist);
                return;
            }

            if (!string.IsNullOrEmpty(resolvedUsername))
                username = resolvedUsername;
        }

        if (string.IsNullOrEmpty(username))
            username = "dvabona";

        int start = 0;
        string startStr = Request.QueryString["start"];
        if (!string.IsNullOrEmpty(startStr))
            int.TryParse(startStr, out start);
        if (start < 0) start = 0;

        const int pageSize = 5;

        string apiUrl = "http://172.16.40.100/get_ig_profile_info.php?query_secret=my_super_secret_key&username="
                        + HttpUtility.UrlEncode(username);

        if (!string.IsNullOrEmpty(refresh) && refresh == "1")
            apiUrl += "&clear_cache=1";

        JObject profileData = null;
        string apiErrorMessage = null;

        try
        {
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                string json = wc.DownloadString(apiUrl);
                profileData = JObject.Parse(json);
            }

            apiErrorMessage = GetApiErrorMessage(profileData);
        }
        catch (WebException wex)
        {
            apiErrorMessage = GetWebExceptionMessage(wex);
        }
        catch (Exception ex)
        {
            apiErrorMessage = ex.Message;
        }

        if (!string.IsNullOrEmpty(apiErrorMessage))
        {
            RenderErrorPage(username, lukifyuserid, apiErrorMessage, artist);
            return;
        }

        if (profileData == null)
        {
            RenderErrorPage(username, lukifyuserid, "Empty API response", artist);
            return;
        }

        JObject dataObj = profileData["data"] as JObject;
        JObject user = null;
        if (dataObj != null)
            user = dataObj["user"] as JObject;
        if (user == null)
            user = new JObject();

        string fullName = user["full_name"] != null ? user["full_name"].ToString() : username;
        bool isVerified = IsProfileVerified(user);

        string bio = "";
        if (user["biography_with_entities"] != null && user["biography_with_entities"]["raw_text"] != null)
            bio = user["biography_with_entities"]["raw_text"].ToString();
        bio = CyrillicToLatin(bio);

        List<string> mentions = ExtractMentions(bio);

        string profilePicRaw = user["profile_pic_url_hd"] != null ? user["profile_pic_url_hd"].ToString() : "AppImages/default_profile.png";
        string profilePic = "http://172.16.40.100/ig_pfp_loader.php?image_url=" + HttpUtility.UrlEncode(profilePicRaw);

        string externalUrl = user["external_url"] != null ? user["external_url"].ToString() : "";
        string encodedExternalUrl = HttpUtility.UrlEncode(externalUrl ?? "");

        string selectedView = NormalizeView(view);

        JArray reelsPosts = GetPostsArray(user, "edge_owner_to_timeline_media");
        JArray videoPosts = GetPostsArray(user, "edge_felix_video_timeline");

        if (string.IsNullOrEmpty(selectedView))
        {
            if (reelsPosts != null && reelsPosts.Count > 0)
                selectedView = "reels";
            else if (videoPosts != null && videoPosts.Count > 0)
                selectedView = "videos";
            else
                selectedView = "reels";
        }

        JArray posts = selectedView == "videos" ? videoPosts : reelsPosts;
        if (posts == null) posts = new JArray();

        string sectionTitle = selectedView == "videos" ? "Videos" : "Reels";

        List<JObject> playablePosts = new List<JObject>();
        for (int i = 0; i < posts.Count; i++)
        {
            JToken edge = posts[i];
            JObject node = edge["node"] as JObject;
            if (node == null) continue;

            if (IsPlayablePost(node))
                playablePosts.Add(node);
        }

        StringBuilder mrml = new StringBuilder();
        mrml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        mrml.Append("<uidescription version=\"3.0\">");
        mrml.Append("<MrmlPage id=\"InstagramProfile\" width=\"1280\" height=\"720\">");
        mrml.Append("<Panel id=\"MainPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"720\">");

        mrml.Append("<DataSource id=\"SystemInfo\" uri=\"local://system-info\" />");
        mrml.Append("<EditText id=\"DeviceGuid\" visible=\"false\" datasource=\"{Binding Source=SystemInfo,Path=DeviceId}\" />");

        int top = 60;

        string profileLabel = (!string.IsNullOrEmpty(fullName) && fullName != "Unknown")
                                ? (fullName + " (@" + username + ")")
                                : ("@" + username);

        mrml.AppendFormat(
            "<Text left=\"20\" top=\"20\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">{0}'s Instagram Profile</Text>",
            EscapeXml(profileLabel)
        );

        string refreshUrl =
            "page:http://172.16.40.101/SETTEMediaroomApp/ViewInstagramProfile.aspx?"
            + "username=" + HttpUtility.UrlEncode(username)
            + "&selected_user_id=" + HttpUtility.UrlEncode(lukifyuserid ?? "")
            + "&view=" + HttpUtility.UrlEncode(selectedView)
            + "&start=" + HttpUtility.UrlEncode(start.ToString())
            + "&refresh=1";

        if (!string.IsNullOrEmpty(artist))
            refreshUrl += "&artist=" + HttpUtility.UrlEncode(artist);

        mrml.AppendFormat(
            "<Button left=\"1120\" top=\"18\" width=\"120\" height=\"40\" href=\"{0}\">Refresh</Button>",
            EscapeXml(refreshUrl)
        );

        mrml.AppendFormat("<Image left=\"60\" top=\"{0}\" width=\"180\" height=\"180\" url=\"{1}\" />", top, EscapeXml(profilePic));

        mrml.AppendFormat("<Text left=\"260\" top=\"{0}\" fontstyle=\"Reg32\" foreground=\"argb(255,226,0,116)\">{1}</Text>", top + 10, EscapeXml(fullName));

        string usernameLine = "@" + username;
        if (isVerified)
            usernameLine += " (Verified)";

        mrml.AppendFormat("<Text left=\"260\" top=\"{0}\" fontstyle=\"Reg26\" foreground=\"argb(255,255,255,255)\">{1}</Text>", top + 55, EscapeXml(usernameLine));

        int bioTop = top + 90;
        mrml.AppendFormat("<Text left=\"260\" top=\"{0}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">{1}</Text>", bioTop, EscapeXml(Truncate(bio, 400)));

        if (mentions != null && mentions.Count > 0)
        {
            int mentionsTop = bioTop + 60;
            int mentionLeft = 260;
            int mentionBtnW = 220;
            int mentionBtnH = 36;
            int mentionSpacing = 12;

            mrml.AppendFormat("<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">Mentions:</Text>", 260, mentionsTop - 28);

            string userId = Request.QueryString["selected_user_id"] ?? "";

            for (int m = 0; m < mentions.Count; m++)
            {
                string mUser = mentions[m];
                string cleanUser = mUser.StartsWith("@") ? mUser.Substring(1) : mUser;

                string link = "page:http://172.16.40.101/SETTEMediaroomApp/ViewInstagramProfile.aspx?username="
                    + HttpUtility.UrlEncode(cleanUser)
                    + "&selected_user_id=" + HttpUtility.UrlEncode(userId)
                    + "&view=" + HttpUtility.UrlEncode(selectedView)
                    + "&start=0";

                if (!string.IsNullOrEmpty(artist))
                    link += "&artist=" + HttpUtility.UrlEncode(artist);

                mrml.AppendFormat(
                    "<Button left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" href=\"{4}\">{5}</Button>",
                    mentionLeft, mentionsTop, mentionBtnW, mentionBtnH, EscapeXml(link), EscapeXml(mUser)
                );

                mentionLeft += mentionBtnW + mentionSpacing;

                if (mentionLeft + mentionBtnW > 1280 - 60)
                {
                    mentionLeft = 260;
                    mentionsTop += mentionBtnH + mentionSpacing;
                }
            }

            top = mentionsTop + mentionBtnH + 20 - 60;
        }

        string instagramProfileUrl = "https://www.instagram.com/" + HttpUtility.UrlEncode(username) + "/";
        string encodedInstagramProfileUrl = HttpUtility.UrlEncode(instagramProfileUrl);

        string sendProfileHref = string.Format(
            "page:http://172.16.40.101/SETTEMediaroomApp/SendLinkToPhone.aspx?user_id={0}&deviceguid={{DeviceGuid}}&url={1}",
            HttpUtility.UrlEncode(lukifyuserid ?? ""),
            encodedInstagramProfileUrl
        );

        mrml.AppendFormat(
            "<Button left=\"260\" top=\"{0}\" width=\"320\" height=\"44\" href=\"{1}\">Send profile to phone</Button>",
            top + 140, EscapeXml(sendProfileHref)
        );

        JArray bioLinks = user["bio_links"] as JArray;
        if (bioLinks != null && bioLinks.Count > 0)
        {
            int linkTop = top + 190;
            int linkLeft = 260;
            int rowStartLeft = 260;
            int linkGapX = 10;
            int linkGapY = 10;
            int linkHeight = 44;
            int maxRight = 1280 - 60;

            mrml.AppendFormat(
                "<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">Profile links:</Text>",
                linkLeft, linkTop - 28
            );

            for (int i = 0; i < bioLinks.Count; i++)
            {
                JObject linkObj = bioLinks[i] as JObject;
                if (linkObj == null) continue;

                string title = linkObj["title"] != null ? linkObj["title"].ToString() : ("Link " + (i + 1).ToString());
                string url = linkObj["url"] != null ? linkObj["url"].ToString() : "";
                string lynxUrl = linkObj["lynx_url"] != null ? linkObj["lynx_url"].ToString() : "";

                if (string.IsNullOrEmpty(url))
                    url = lynxUrl;

                if (string.IsNullOrEmpty(url))
                    continue;

                string sendLinkHref = string.Format(
                    "page:http://172.16.40.101/SETTEMediaroomApp/SendLinkToPhone.aspx?user_id={0}&deviceguid={{DeviceGuid}}&url={1}",
                    HttpUtility.UrlEncode(lukifyuserid ?? ""),
                    HttpUtility.UrlEncode(url)
                );

                int buttonWidth = Math.Min(280, Math.Max(170, title.Length * 10 + 40));
                if (linkLeft + buttonWidth > maxRight)
                {
                    linkLeft = rowStartLeft;
                    linkTop += linkHeight + linkGapY;
                }

                mrml.AppendFormat(
                    "<Button left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" href=\"{4}\">{5}</Button>",
                    linkLeft, linkTop, buttonWidth, linkHeight, EscapeXml(sendLinkHref), EscapeXml(title)
                );

                linkLeft += buttonWidth + linkGapX;
            }
        }
        else if (!string.IsNullOrEmpty(externalUrl))
        {
            string sendExternalHref = string.Format(
                "page:http://172.16.40.101/SETTEMediaroomApp/SendLinkToPhone.aspx?user_id={0}&deviceguid={{DeviceGuid}}&url={1}",
                HttpUtility.UrlEncode(lukifyuserid ?? ""),
                encodedExternalUrl
            );

            mrml.AppendFormat(
                "<Button left=\"600\" top=\"{0}\" width=\"420\" height=\"44\" href=\"{1}\">Send link to phone / Visit</Button>",
                top + 140, EscapeXml(sendExternalHref)
            );
        }

        string reelsTabUrl =
            "page:http://172.16.40.101/SETTEMediaroomApp/ViewInstagramProfile.aspx?"
            + "username=" + HttpUtility.UrlEncode(username)
            + "&selected_user_id=" + HttpUtility.UrlEncode(lukifyuserid ?? "")
            + "&view=reels";

        string videosTabUrl =
            "page:http://172.16.40.101/SETTEMediaroomApp/ViewInstagramProfile.aspx?"
            + "username=" + HttpUtility.UrlEncode(username)
            + "&selected_user_id=" + HttpUtility.UrlEncode(lukifyuserid ?? "")
            + "&view=videos";

        if (!string.IsNullOrEmpty(artist))
        {
            reelsTabUrl += "&artist=" + HttpUtility.UrlEncode(artist);
            videosTabUrl += "&artist=" + HttpUtility.UrlEncode(artist);
        }

        int tabTop = top + 205;
        mrml.AppendFormat(
            "<Button left=\"60\" top=\"{0}\" width=\"160\" height=\"42\" href=\"{1}\">Reels</Button>",
            tabTop, EscapeXml(reelsTabUrl)
        );
        mrml.AppendFormat(
            "<Button left=\"230\" top=\"{0}\" width=\"160\" height=\"42\" href=\"{1}\">Videos</Button>",
            tabTop, EscapeXml(videosTabUrl)
        );

        if (playablePosts != null && playablePosts.Count > 0)
        {
            int compactTop = tabTop + 60;
            int compactLeft = 60;
            int compactBtnW = 1040;
            int compactBtnH = 112;
            int compactSpacing = 10;

            mrml.AppendFormat(
                "<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg22\" foreground=\"argb(255,200,200,200)\">{2}</Text>",
                compactLeft, compactTop - 30, EscapeXml(sectionTitle)
            );

            int visibleCount = 0;
            int index = start;

            while (index < playablePosts.Count && visibleCount < pageSize)
            {
                JObject node = playablePosts[index];
                if (node == null)
                {
                    index++;
                    continue;
                }

                string postTitle = GetPostTitle(node);
                string postAuthor = GetPostAuthorUsername(node, username);

                string displayTitle = "@" + postAuthor + " - " + postTitle;
                displayTitle = Truncate(displayTitle, 78);

                string postViews = GetPostViews(node);
                string postLikes = GetPostLikes(node);
                string postDate = GetPostUploadDate(node);
                string postDuration = GetPostDuration(node);
                string postFrom = GetPostFromIgtv(node);

                string postMusic = "";
                if (selectedView == "reels")
                    postMusic = GetClipsMusic(node);

                string thumbRaw = node["thumbnail_src"] != null ? node["thumbnail_src"].ToString() : "";
                string thumbSmall = "http://172.16.40.100/ig_pfp_loader.php?image_url=" + HttpUtility.UrlEncode(thumbRaw);

                string openVideoUrl = BuildOpenVideoUrl(
                    node,
                    username,
                    selectedView,
                    lukifyuserid,
                    index,
                    artist
                );

                mrml.AppendFormat(
                    "<Button left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" href=\"{4}\">",
                    compactLeft, compactTop, compactBtnW, compactBtnH, EscapeXml(openVideoUrl)
                );

                mrml.AppendFormat(
                    "<Text left=\"{0}\" top=\"{1}\" width=\"620\" height=\"18\" fontstyle=\"Reg18\" foreground=\"argb(255,255,255,255)\">{2}</Text>",
                    12, 8, EscapeXml(displayTitle)
                );

                mrml.AppendFormat(
                    "<Text left=\"{0}\" top=\"{1}\" width=\"620\" height=\"16\" fontstyle=\"Reg16\" foreground=\"argb(255,200,200,200)\">{2}</Text>",
                    12, 30, EscapeXml(postDate)
                );

                if (!string.IsNullOrEmpty(postDuration))
                {
                    mrml.AppendFormat(
                        "<Text left=\"{0}\" top=\"{1}\" width=\"620\" height=\"16\" fontstyle=\"Reg16\" foreground=\"argb(255,200,200,200)\">{2}</Text>",
                        12, 48, EscapeXml(postDuration)
                    );
                }

                if (!string.IsNullOrEmpty(postMusic))
                {
                    mrml.AppendFormat(
                        "<Text left=\"{0}\" top=\"{1}\" width=\"620\" height=\"16\" fontstyle=\"Reg16\" foreground=\"argb(255,180,180,255)\">{2}</Text>",
                        12, 66, EscapeXml(postMusic)
                    );
                }

                if (!string.IsNullOrEmpty(postFrom))
                {
                    mrml.AppendFormat(
                        "<Text left=\"{0}\" top=\"{1}\" width=\"620\" height=\"16\" fontstyle=\"Reg16\" foreground=\"argb(255,200,200,200)\">{2}</Text>",
                        12, 84, EscapeXml(postFrom)
                    );
                }

                int statsLeft = 700;
                mrml.AppendFormat(
                    "<Text left=\"{0}\" top=\"{1}\" width=\"220\" height=\"18\" fontstyle=\"Reg16\" foreground=\"argb(255,200,200,200)\" justification=\"right\">{2}</Text>",
                    statsLeft, 28, EscapeXml(postViews)
                );

                mrml.AppendFormat(
                    "<Text left=\"{0}\" top=\"{1}\" width=\"220\" height=\"18\" fontstyle=\"Reg16\" foreground=\"argb(255,200,200,200)\" justification=\"right\">{2}</Text>",
                    statsLeft, 48, EscapeXml(postLikes)
                );

                int smallImgWidth = 52;
                int smallImgHeight = 52;
                int smallImgLeft = compactBtnW - smallImgWidth - 12;
                int smallImgTop = 28;

                mrml.AppendFormat(
                    "<Image left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" url=\"{4}\" />",
                    smallImgLeft, smallImgTop, smallImgWidth, smallImgHeight, EscapeXml(thumbSmall)
                );

                mrml.Append("</Button>");

                compactTop += compactBtnH + compactSpacing;
                visibleCount++;
                index++;
            }

            int navTop = compactTop + 10;
            int navLeft = 60;

            string encodedUserId = HttpUtility.UrlEncode(lukifyuserid ?? "");

            if (start > 0)
            {
                int prevStart = Math.Max(0, start - pageSize);

                string prevPageUrl =
                    "page:http://172.16.40.101/SETTEMediaroomApp/ViewInstagramProfile.aspx?"
                    + "username=" + HttpUtility.UrlEncode(username)
                    + "&start=" + prevStart
                    + "&selected_user_id=" + encodedUserId
                    + "&view=" + HttpUtility.UrlEncode(selectedView);

                if (!string.IsNullOrEmpty(artist))
                    prevPageUrl += "&artist=" + HttpUtility.UrlEncode(artist);

                mrml.AppendFormat(
                    "<Button left=\"{0}\" top=\"{1}\" width=\"160\" height=\"44\" href=\"{2}\">Previous</Button>",
                    navLeft, navTop, EscapeXml(prevPageUrl)
                );

                navLeft += 180;
            }

            if (index < playablePosts.Count)
            {
                string loadMoreUrl =
                    "page:http://172.16.40.101/SETTEMediaroomApp/ViewInstagramProfile.aspx?"
                    + "username=" + HttpUtility.UrlEncode(username)
                    + "&start=" + index
                    + "&selected_user_id=" + encodedUserId
                    + "&view=" + HttpUtility.UrlEncode(selectedView);

                if (!string.IsNullOrEmpty(artist))
                    loadMoreUrl += "&artist=" + HttpUtility.UrlEncode(artist);

                mrml.AppendFormat(
                    "<Button left=\"{0}\" top=\"{1}\" width=\"200\" height=\"44\" href=\"{2}\">Load more</Button>",
                    navLeft, navTop, EscapeXml(loadMoreUrl)
                );
            }
        }
        else
        {
            int noPostsTop = top + 320;
            mrml.AppendFormat(
                "<Text left=\"{0}\" top=\"{1}\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">No {2} found.</Text>",
                60, noPostsTop, EscapeXml(sectionTitle.ToLowerInvariant())
            );
        }

        mrml.Append("</Panel></MrmlPage></uidescription>");

        Response.Write(mrml.ToString());
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private bool ResolveUsernameFromArtist(string artist, out string username, out string errorMessage)
    {
        username = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(artist))
        {
            errorMessage = "invalid username from artist";
            return false;
        }

        string url = "http://172.16.40.100/get_username_from_artist.php?artist=" + HttpUtility.UrlEncode(artist);

        try
        {
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                string json = wc.DownloadString(url);
                JObject obj = JObject.Parse(json);

                bool success = false;
                if (obj["success"] != null)
                {
                    bool.TryParse(obj["success"].ToString(), out success);
                    if (!success && obj["success"].Type == JTokenType.Integer)
                        success = obj["success"].ToString() == "1";
                }

                if (!success)
                {
                    if (obj["message"] != null)
                    {
                        string msg = obj["message"].ToString().Trim();
                        if (!string.IsNullOrEmpty(msg))
                        {
                            errorMessage = msg;
                            return false;
                        }
                    }

                    errorMessage = "invalid username from artist";
                    return false;
                }

                if (obj["username"] != null)
                {
                    username = obj["username"].ToString().Trim();

                    if (string.IsNullOrEmpty(username))
                    {
                        errorMessage = "artist username not found";
                        return false;
                    }

                    if (!IsValidInstagramUsername(username))
                    {
                        errorMessage = "invalid username from artist";
                        return false;
                    }

                    return true;
                }

                errorMessage = "artist username not found";
                return false;
            }
        }
        catch (WebException wex)
        {
            string msg = GetWebExceptionMessage(wex);
            if (!string.IsNullOrEmpty(msg))
                errorMessage = msg;
            else
                errorMessage = "invalid username from artist";
            return false;
        }
        catch (Exception)
        {
            errorMessage = "invalid username from artist";
            return false;
        }
    }

    private bool IsValidInstagramUsername(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim();

        if (value.Length < 1 || value.Length > 30)
            return false;

        return Regex.IsMatch(value, @"^[A-Za-z0-9._]+$");
    }

    private string BuildOpenVideoUrl(
        JObject node,
        string username,
        string selectedView,
        string lukifyuserid,
        int startIndex,
        string artist)
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
        if (selectedView == "reels")
        {
            audioArtist = GetClipsMusicArtist(node);
            audioTitle = GetClipsMusicSong(node);
        }

        string mode = selectedView == "reels" ? "reel" : "video";
        string postId = GetPostId(node, startIndex);

        StringBuilder url = new StringBuilder();
        url.Append("page:http://172.16.40.101/SETTEMediaroomApp/OpenInstagramVideo.aspx?");
        url.Append("video_url=").Append(HttpUtility.UrlEncode(videoUrl ?? ""));
        url.Append("&video_name=").Append(HttpUtility.UrlEncode(videoName ?? ""));
        url.Append("&ig_username=").Append(HttpUtility.UrlEncode(username ?? ""));
        url.Append("&owner_username=").Append(HttpUtility.UrlEncode(postAuthor ?? ""));
        url.Append("&post_share_link=").Append(HttpUtility.UrlEncode(postShareLink ?? ""));
        url.Append("&post_share_id=").Append(HttpUtility.UrlEncode(postShareId ?? ""));
        url.Append("&selected_user_id=").Append(HttpUtility.UrlEncode(lukifyuserid ?? ""));
        url.Append("&thumbnail_url=").Append(HttpUtility.UrlEncode(thumbSmall ?? ""));
        url.Append("&view=").Append(HttpUtility.UrlEncode(selectedView ?? ""));
        url.Append("&mode=").Append(HttpUtility.UrlEncode(mode));
        url.Append("&start=").Append(HttpUtility.UrlEncode(startIndex.ToString()));
        url.Append("&artist_name=").Append(HttpUtility.UrlEncode(audioArtist ?? ""));
        url.Append("&title=").Append(HttpUtility.UrlEncode(audioTitle ?? ""));
        url.Append("&views=").Append(HttpUtility.UrlEncode(postViews ?? ""));
        url.Append("&likes=").Append(HttpUtility.UrlEncode(postLikes ?? ""));
        url.Append("&uploaded_date=").Append(HttpUtility.UrlEncode(postDate ?? ""));
        url.Append("&post_id=").Append(HttpUtility.UrlEncode(postId ?? ""));

        if (!string.IsNullOrEmpty(artist))
            url.Append("&artist=").Append(HttpUtility.UrlEncode(artist));

        return url.ToString();
    }

    private string GetPostId(JObject node, int fallbackIndex)
    {
        if (node == null)
            return fallbackIndex.ToString();

        try
        {
            if (node["id"] != null)
            {
                string v = node["id"].ToString().Trim();
                if (!string.IsNullOrEmpty(v))
                    return v;
            }

            if (node["pk"] != null)
            {
                string v = node["pk"].ToString().Trim();
                if (!string.IsNullOrEmpty(v))
                    return v;
            }

            if (node["shortcode"] != null)
            {
                string v = node["shortcode"].ToString().Trim();
                if (!string.IsNullOrEmpty(v))
                    return v;
            }
        }
        catch
        {
        }

        return fallbackIndex.ToString();
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

    private void RenderErrorPage(string username, string lukifyuserid, string message, string artist = null)
    {
        StringBuilder mrml = new StringBuilder();
        mrml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        mrml.Append("<uidescription version=\"3.0\">");
        mrml.Append("<MrmlPage id=\"InstagramProfileError\" width=\"1280\" height=\"720\">");
        mrml.Append("<Panel id=\"MainPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"720\">");

        mrml.Append("<Text left=\"20\" top=\"20\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">Instagram Profile</Text>");
        mrml.AppendFormat(
            "<Text left=\"20\" top=\"70\" fontstyle=\"Reg26\" foreground=\"argb(255,226,0,116)\">@{0}</Text>",
            EscapeXml(username ?? "")
        );

        mrml.Append("<Text left=\"60\" top=\"120\" fontstyle=\"Reg32\" foreground=\"argb(255,226,0,116)\">Error</Text>");
        mrml.Append("<Text left=\"60\" top=\"165\" fontstyle=\"Reg22\" foreground=\"argb(255,255,255,255)\">Message:</Text>");

        mrml.AppendFormat(
            "<Text left=\"60\" top=\"200\" width=\"1160\" height=\"240\" fontstyle=\"Reg22\" foreground=\"argb(255,255,255,255)\">{0}</Text>",
            EscapeXml(Truncate(message ?? "Unknown error", 900))
        );

        if (!string.IsNullOrWhiteSpace(artist))
        {
            mrml.AppendFormat(
                "<Text left=\"60\" top=\"455\" width=\"1160\" height=\"36\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">Artist: {0}</Text>",
                EscapeXml(artist)
            );

            mrml.AppendLine("<Actions>");
            mrml.AppendFormat(
                "  <Action name=\"showartistnotfounnddialog\" type=\"dialog\" data=\"Artist not found: {0}\" />",
                EscapeXml(artist)
            );
            mrml.AppendLine("<Event type=\"onkey:menu\" action=\"showartistnotfounnddialog\" />");
            mrml.AppendLine("</Actions>");
        }

        string retryUrl =
            "page:http://172.16.40.101/SETTEMediaroomApp/ViewInstagramProfile.aspx?"
            + "username=" + HttpUtility.UrlEncode(username ?? "")
            + "&selected_user_id=" + HttpUtility.UrlEncode(lukifyuserid ?? "")
            + "&refresh=1";

        if (!string.IsNullOrEmpty(artist))
            retryUrl += "&artist=" + HttpUtility.UrlEncode(artist);

        mrml.AppendFormat(
            "<Button left=\"60\" top=\"520\" width=\"180\" height=\"44\" href=\"{0}\">Try again</Button>",
            EscapeXml(retryUrl)
        );

        mrml.Append("</Panel></MrmlPage></uidescription>");

        Response.Write(mrml.ToString());
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private string GetApiErrorMessage(JObject profileData)
    {
        if (profileData == null)
            return null;

        try
        {
            string status = profileData["status"] != null ? profileData["status"].ToString() : "";

            if (status.Equals("error", StringComparison.OrdinalIgnoreCase))
            {
                if (profileData["message"] != null)
                    return profileData["message"].ToString();

                if (profileData["error"] != null)
                    return profileData["error"].ToString();

                return "API returned error";
            }

            if (status.Equals("fail", StringComparison.OrdinalIgnoreCase))
            {
                if (profileData["message"] != null)
                    return profileData["message"].ToString();

                return "Please wait a few minutes before you try again.";
            }

            if (profileData["require_login"] != null && profileData["require_login"].Type != JTokenType.Null)
            {
                bool requireLogin = false;
                bool.TryParse(profileData["require_login"].ToString(), out requireLogin);
                if (requireLogin)
                {
                    if (profileData["message"] != null)
                        return profileData["message"].ToString();

                    return "Please wait a few minutes before you try again.";
                }
            }

            if (profileData["message"] != null)
            {
                string msg = profileData["message"].ToString();
                string lower = msg.ToLowerInvariant();

                if (lower.Contains("please wait") ||
                    lower.Contains("try again") ||
                    lower.Contains("login") ||
                    lower.Contains("challenge"))
                {
                    return msg;
                }
            }

            if (profileData["error"] != null && profileData["error"].Type != JTokenType.Null)
            {
                string err = profileData["error"].ToString();
                if (!string.IsNullOrEmpty(err))
                    return err;
            }
        }
        catch
        {
        }

        return null;
    }

    private string GetWebExceptionMessage(WebException wex)
    {
        try
        {
            if (wex != null && wex.Response != null)
            {
                using (var stream = wex.Response.GetResponseStream())
                {
                    if (stream != null)
                    {
                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            string body = reader.ReadToEnd();
                            if (!string.IsNullOrEmpty(body))
                            {
                                try
                                {
                                    JObject obj = JObject.Parse(body);
                                    string msg = GetApiErrorMessage(obj);
                                    if (!string.IsNullOrEmpty(msg))
                                        return msg;

                                    if (obj["message"] != null)
                                        return obj["message"].ToString();
                                }
                                catch
                                {
                                    return body;
                                }
                            }
                        }
                    }
                }
            }

            if (wex != null)
                return wex.Message;
        }
        catch
        {
        }

        return "Failed to fetch API data";
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

    private string GetPostDuration(JObject node)
    {
        if (node == null)
            return "";

        try
        {
            string[] candidateKeys = new string[]
            {
                "video_duration",
                "duration",
                "playback_duration"
            };

            foreach (string key in candidateKeys)
            {
                if (node[key] == null)
                    continue;

                string raw = node[key].ToString().Trim();
                if (string.IsNullOrEmpty(raw))
                    continue;

                double seconds;
                if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out seconds))
                    return "Duration: " + FormatDuration(seconds);

                long secsInt;
                if (long.TryParse(raw, out secsInt))
                    return "Duration: " + FormatDuration(secsInt);
            }
        }
        catch
        {
        }

        return "";
    }

    private string GetPostFromIgtv(JObject node)
    {
        if (node == null)
            return "";

        try
        {
            string productType = node["product_type"] != null ? node["product_type"].ToString().Trim() : "";
            if (productType.Equals("igtv", StringComparison.OrdinalIgnoreCase))
                return "Post from: IGTV";
        }
        catch
        {
        }

        return "";
    }

    private string GetClipsMusic(JObject node)
    {
        string artist = GetClipsMusicArtist(node);
        string song = GetClipsMusicSong(node);

        if (!string.IsNullOrEmpty(artist) || !string.IsNullOrEmpty(song))
            return "Music: " + artist + " - " + song;

        return "";
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

    private string FormatDuration(double totalSeconds)
    {
        if (totalSeconds < 0)
            totalSeconds = 0;

        TimeSpan ts = TimeSpan.FromSeconds(totalSeconds);

        if (ts.TotalHours >= 1)
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);

        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", ts.Minutes, ts.Seconds);
    }

    private List<string> ExtractMentions(string text)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(text)) return list;

        var regex = new Regex(@"@([A-Za-z0-9._]+)", RegexOptions.Compiled);
        var matches = regex.Matches(text);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in matches)
        {
            if (m.Groups.Count > 1)
            {
                string raw = "@" + m.Groups[1].Value;
                if (!seen.Contains(raw))
                {
                    seen.Add(raw);
                    list.Add(raw);
                }
            }
        }
        return list;
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

    private string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || max <= 0) return "";
        if (s.Length <= max) return s;
        return s.Substring(0, max).TrimEnd() + "...";
    }

    private string CyrillicToLatin(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var map = new Dictionary<string, string>(50)
        {
            {"А","A"},{"Б","B"},{"В","V"},{"Г","G"},{"Д","D"},{"Ѓ","Gj"},{"Е","E"},{"Ж","Zh"},{"З","Z"},{"Ѕ","Dz"},
            {"И","I"},{"Ј","J"},{"К","K"},{"Ќ","Kj"},{"Л","L"},{"Љ","Lj"},{"М","M"},{"Н","N"},{"Њ","Nj"},{"О","O"},
            {"П","P"},{"Р","R"},{"С","S"},{"Т","T"},{"У","U"},{"Ф","F"},{"Х","H"},{"Ц","C"},{"Ч","Ch"},{"Џ","Dzh"},{"Ш","Sh"},
            {"а","a"},{"б","b"},{"в","v"},{"г","g"},{"д","d"},{"ѓ","gj"},{"е","e"},{"ж","zh"},{"з","z"},{"ѕ","dz"},
            {"и","i"},{"ј","j"},{"к","k"},{"ќ","kj"},{"л","l"},{"љ","lj"},{"м","m"},{"н","n"},{"њ","nj"},{"о","o"},
            {"п","p"},{"р","r"},{"с","s"},{"т","t"},{"у","u"},{"ф","f"},{"х","h"},{"ц","c"},{"ч","ch"},{"џ","dzh"},{"ш","sh"}
        };

        StringBuilder sb = new StringBuilder(text.Length * 2);
        for (int i = 0; i < text.Length; i++)
        {
            string key = text.Substring(i, 1);
            string val;
            if (map.TryGetValue(key, out val))
                sb.Append(val);
            else
                sb.Append(key);
        }
        return sb.ToString();
    }

    private string NormalizeView(string view)
    {
        string v = (view ?? "").Trim().ToLowerInvariant();
        if (v == "reel") return "reels";
        if (v == "video") return "videos";
        if (v != "reels" && v != "videos") return "";
        return v;
    }

    private bool IsProfileVerified(JObject user)
    {
        if (user == null)
            return false;

        try
        {
            string[] keys = new string[]
            {
                "is_verified",
                "verified"
            };

            foreach (string key in keys)
            {
                if (user[key] != null && user[key].Type != JTokenType.Null)
                {
                    bool val;
                    if (bool.TryParse(user[key].ToString(), out val))
                        return val;

                    string raw = user[key].ToString().Trim();
                    if (raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }
}