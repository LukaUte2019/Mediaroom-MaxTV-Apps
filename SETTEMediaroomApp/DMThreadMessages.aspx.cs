using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;
using System.Linq;
using Newtonsoft.Json.Linq;

public partial class DMThreadMessages : Page
{
    private const string MUSIC_EVENT_IMAGE_PROXY_BASE = "https://lukaserver.ddns.net/ig_pfp_loader.php?image_url=";

    protected void Page_Load(object sender, EventArgs e)
    {
        string threadTitle = Request.QueryString["thread_name"] ?? "";
        string encodedThreadId = Request.QueryString["thread_id"];
        string userId = Request.QueryString["userid"];

        if (string.IsNullOrEmpty(encodedThreadId) || string.IsNullOrEmpty(userId))
        {
            Response.StatusCode = 400;
            Response.End();
            return;
        }

        string deviceGuid = Request.QueryString["DeviceGuid"] ?? "";

        // Decode thread id for API call: URL-decode then Base64-decode if possible
        string threadId = encodedThreadId;
        try
        {
            string decodedUrl = HttpUtility.UrlDecode(encodedThreadId);
            byte[] bytes = Convert.FromBase64String(decodedUrl);
            threadId = Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            threadId = encodedThreadId;
        }

        // Send message if submitted
        string submittedMsg = Request.QueryString["txtMessage"];
        if (!string.IsNullOrEmpty(submittedMsg) && !string.IsNullOrEmpty(threadId))
        {
            SendMessageToThread(userId, threadId, submittedMsg);

            string redirectUrl = "DMThreadMessages.aspx?thread_id=" + HttpUtility.UrlEncode(encodedThreadId)
                   + "&userid=" + HttpUtility.UrlEncode(userId)
                   + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid)
                   + "&thread_name=" + HttpUtility.UrlEncode(threadTitle);

            Response.Redirect(redirectUrl);
            return;
        }

        // Paging
        int page = 1;
        int pageSize = 8;

        int pageParse;
        if (!string.IsNullOrEmpty(Request.QueryString["page"]) && int.TryParse(Request.QueryString["page"], out pageParse) && pageParse > 0)
            page = pageParse;

        List<Message> allMessages = GetMessages(threadId, userId) ?? new List<Message>();

        // newest first
        allMessages.Reverse();

        int total = allMessages.Count;
        int startIndex = (page - 1) * pageSize;
        if (startIndex < 0) startIndex = 0;
        if (startIndex > total) startIndex = total;

        int endIndex = Math.Min(startIndex + pageSize, total);
        List<Message> pageMessages = (endIndex > startIndex)
            ? allMessages.GetRange(startIndex, endIndex - startIndex)
            : new List<Message>();

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"<MrmlPage id=""DMThreadMessages"" appid=""lukatube.dm/1.0"" width=""1280"" height=""720"">");
        sb.AppendLine(@"<Panel>");

        // Send form
        string sendUrl =
            "http://172.16.40.101/SETTEMediaroomApp/DMThreadMessages.aspx"
            + "?userid=" + HttpUtility.UrlEncode(userId)
            + "&thread_id=" + HttpUtility.UrlEncode(encodedThreadId)
            + "&thread_name=" + HttpUtility.UrlEncode(threadTitle)
            + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

        sb.AppendLine(string.Format(
            @"<Text top=""10"" left=""40"" width=""1200"" height=""40"" fontstyle=""Reg32"" foreground=""argb(255,255,255,255)"">{0}</Text>",
            HttpUtility.HtmlEncode(threadTitle)
        ));

        sb.AppendLine("<EditText id=\"txtMessage\" top=\"44\" left=\"40\" width=\"900\" height=\"56\" fontstyle=\"Reg24\" background=\"argb(255,40,40,40)\" />");

        sb.AppendLine("<Actions>");
        sb.AppendLine("  <Action name=\"SendMessageToThread\" type=\"submit\" data=\"txtMessage\" method=\"GET\" url=\"page:" + HttpUtility.HtmlAttributeEncode(sendUrl) + "\" />");
        sb.AppendLine("</Actions>");

        sb.AppendLine("<Button top=\"44\" left=\"960\" width=\"220\" height=\"56\">");
        sb.AppendLine("  <Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">Isprati</Text>");
        sb.AppendLine("  <Actions>");
        sb.AppendLine("    <Event type=\"onclick\" action=\"SendMessageToThread\" />");
        sb.AppendLine("  </Actions>");
        sb.AppendLine("</Button>");

        // Thread members button
        string membersBase = "http://172.16.40.101/SETTEMediaroomApp/ThreadMembers.aspx";

        string threadNameRaw = null;
        try
        {
            var createdMsg = allMessages.FirstOrDefault(m => m.message_obj != null && !string.IsNullOrEmpty(m.message_obj.group_name));
            if (createdMsg != null)
                threadNameRaw = createdMsg.message_obj.group_name;
        }
        catch { }

        if (string.IsNullOrEmpty(threadNameRaw))
            threadNameRaw = threadId ?? encodedThreadId;

        var membersQs = new StringBuilder();
        membersQs.Append("userid=").Append(HttpUtility.UrlEncode(userId));
        membersQs.Append("&thread_id=").Append(HttpUtility.UrlEncode(threadId));
        membersQs.Append("&thread_name=").Append(HttpUtility.UrlEncode(threadNameRaw));
        membersQs.Append("&DeviceGuid=").Append(HttpUtility.UrlEncode(deviceGuid));

        string membersUrl = membersBase + "?" + membersQs.ToString();
        string safeMembersHref = HttpUtility.HtmlAttributeEncode(membersUrl);

        sb.AppendLine(string.Format(
            @"<Button id=""ThreadMembers"" top=""44"" left=""1188"" width=""72"" height=""56"" href=""page:{0}""><Text>Members</Text></Button>",
            safeMembersHref
        ));

        if (total == 0)
        {
            sb.AppendLine(@"<Text top=""120"" left=""40"" width=""900"" height=""60"" fontstyle=""Reg28"" foreground=""argb(255,255,60,60)"">No messages found</Text>");
        }
        else
        {
            sb.AppendLine(string.Format(
                @"<Text top=""120"" left=""40"" width=""1200"" height=""30"" fontstyle=""Reg26"">Showing {0} - {1} of {2} messages (newest first)</Text>",
                startIndex + 1, endIndex, total));

            int topPos = 160;
            int userInt = 0;
            int.TryParse(userId, out userInt);

            foreach (var m in pageMessages)
            {
                string prefix = (m.sender_id == userInt) ? "Me: " : "";
                string text = BuildMessageText(m);

                if (string.IsNullOrEmpty(text))
                    text = "[empty message]";

                bool renderedSpecialBlock = false;

                // MUSIC EVENT SHARE: compact card only
                if (m.message_obj != null &&
                    string.Equals(m.message_obj.type, "music_event_share", StringComparison.OrdinalIgnoreCase) &&
                    m.message_obj.music_event_share != null)
                {
                    RenderMusicEventShareCard(sb, m.message_obj.music_event_share, topPos);
                    topPos += 120;
                    renderedSpecialBlock = true;
                    continue;
                }

                // Inline message text + @mentions in the same line
                AppendInlineMessageWithMentions(sb, prefix + text, topPos, 40, userId);

                // --- video handling: check whether message has video object first ---
                string videoUrlRaw = null;
                string videoName = null;

                if (m.message_video != null)
                {
                    videoUrlRaw = ReplaceDdnsHost(m.message_video.video_url);
                    videoName = m.message_video.video_name;
                }
                else
                {
                    var urlMatch = Regex.Match(text ?? "", @"https?:\/\/[^\s'\""]+?\.mp4\b", RegexOptions.IgnoreCase);
                    if (urlMatch.Success)
                    {
                        videoUrlRaw = ReplaceDdnsHost(urlMatch.Value);
                    }
                    else
                    {
                        var filenameMatch = Regex.Match(text ?? "", @"([A-Za-z0-9_\-\(\)\s]+\.mp4)\b", RegexOptions.IgnoreCase);
                        if (filenameMatch.Success)
                        {
                            string filename = filenameMatch.Groups[1].Value.Trim();
                            videoUrlRaw = "http://172.16.40.100/youtubeclone/videos_mediaroom/" + HttpUtility.UrlEncode(filename);
                        }
                    }
                }

                if (!renderedSpecialBlock && !string.IsNullOrEmpty(videoUrlRaw))
                {
                    if (string.IsNullOrEmpty(videoName))
                    {
                        try
                        {
                            string filePart = Path.GetFileName(new Uri(videoUrlRaw).LocalPath);
                            videoName = Path.GetFileNameWithoutExtension(filePart);
                        }
                        catch
                        {
                            int q = videoUrlRaw.IndexOf('?');
                            string tmpUrl = (q >= 0) ? videoUrlRaw.Substring(0, q) : videoUrlRaw;
                            videoName = Path.GetFileNameWithoutExtension(tmpUrl);
                        }
                    }

                    string playBase = "http://172.16.40.101/SETTEMediaroomApp/PlayVideo.aspx";
                    string playQuery = "video_url=" + HttpUtility.UrlEncode(videoUrlRaw)
                                     + "&video_name=" + HttpUtility.UrlEncode(videoName)
                                     + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid)
                                     + "&LocalFolder=false";
                    string playUrl = playBase + "?" + playQuery;
                    string safePlayHref = HttpUtility.HtmlAttributeEncode(playUrl);

                    string btnId = "PlayVideo_" + Guid.NewGuid().ToString("N");

                    sb.AppendLine(string.Format(
                        @"<Button id=""{0}"" top=""{1}"" left=""1040"" width=""200"" height=""40"" href=""page:{2}""><Text>Play Video</Text></Button>",
                        HttpUtility.HtmlEncode(btnId),
                        topPos,
                        safePlayHref
                    ));
                }

                // song listen request
                if (!renderedSpecialBlock &&
                    m.message_obj != null &&
                    string.Equals(m.message_obj.type, "song_listen_request", StringComparison.OrdinalIgnoreCase) &&
                    m.message_obj.song_listen_request != null)
                {
                    var sreq = m.message_obj.song_listen_request;

                    string destBase = "http://172.16.40.101/SETTEMediaroomApp/GetVideoFromSong.aspx";
                    string destQuery;

                    if (!string.IsNullOrEmpty(sreq.song_url))
                    {
                        destQuery = "song_url=" + HttpUtility.UrlEncode(sreq.song_url)
                                  + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);
                    }
                    else
                    {
                        string artist = sreq.artist ?? sreq.display_artist ?? "";
                        string title = sreq.title ?? "";
                        destQuery = "artist=" + HttpUtility.UrlEncode(artist)
                                  + "&title=" + HttpUtility.UrlEncode(title)
                                  + "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);
                    }

                    string destUrl = destBase + "?" + destQuery;
                    string safeDestHref = HttpUtility.HtmlAttributeEncode(destUrl);

                    string btnSongId = "OpenSong_" + Guid.NewGuid().ToString("N");
                    string songTitleForButton = sreq.title ?? "Song";
                    string buttonText = "Play song \"" + songTitleForButton + "\"";
                    string safeSongTitle = HttpUtility.HtmlEncode(buttonText);

                    int rightSideLeft = 1040;
                    int buttonTop = topPos;
                    int buttonWidth = 300;
                    int buttonHeight = 50;

                    int imgWidth = 50;
                    int imgHeight = 50;

                    string coverUrl = sreq.cover_art_url ?? "";
                    if (!string.IsNullOrEmpty(coverUrl))
                        coverUrl = ReplaceDdnsHost(coverUrl);

                    sb.AppendLine(string.Format(
                        @"<Button id=""{0}"" top=""{1}"" left=""{2}"" width=""{3}"" height=""{4}"" href=""page:{5}"">
                                <Image top=""0"" left=""0"" width=""{6}"" height=""{7}"" url=""{8}"" />
                                <Text top=""0"" left=""{6}"" width=""{9}"" height=""{4}"" alignment=""left"" justification=""center"" fontstyle=""Reg18"" foreground=""argb(255,255,255,255)"">{10}</Text>
                              </Button>",
                        HttpUtility.HtmlEncode(btnSongId),
                        buttonTop,
                        rightSideLeft,
                        buttonWidth,
                        buttonHeight,
                        safeDestHref,
                        imgWidth,
                        imgHeight,
                        HttpUtility.HtmlAttributeEncode(coverUrl),
                        buttonWidth - imgWidth,
                        safeSongTitle
                    ));
                }

                // post activity with video
                if (!renderedSpecialBlock &&
                    m.message_obj != null &&
                    (string.Equals(m.message_obj.type, "post", StringComparison.OrdinalIgnoreCase) || m.message_obj.post != null))
                {
                    var p = m.message_obj.post;
                    if (p != null && !string.IsNullOrEmpty(p.video_url))
                    {
                        string postVideoUrl = ReplaceDdnsHost(p.video_url);

                        string videoIdBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(postVideoUrl));
                        string hrefUrl = string.Format(
                            "page:http://172.16.40.101/SETTEMediaroomApp/PlayLukifyVideo.aspx?videoId={0}&user_id={1}&userid={2}",
                            HttpUtility.UrlEncode(videoIdBase64),
                            HttpUtility.UrlEncode(userId),
                            HttpUtility.UrlEncode(userId)
                        );

                        string btnPostId = "PlayPostVideo_" + Guid.NewGuid().ToString("N");

                        string captionForLabel = !string.IsNullOrEmpty(p.caption) ? p.caption : "";
                        if (string.IsNullOrEmpty(captionForLabel))
                        {
                            try
                            {
                                string filePart = Path.GetFileName(new Uri(postVideoUrl).LocalPath);
                                captionForLabel = Path.GetFileNameWithoutExtension(filePart);
                            }
                            catch
                            {
                                captionForLabel = "Post video";
                            }
                        }

                        string safeCaptionLabel = HttpUtility.HtmlEncode(captionForLabel);
                        string safeHref = HttpUtility.HtmlAttributeEncode(hrefUrl);

                        sb.AppendLine(string.Format(
                            @"<Button id=""{0}"" top=""{1}"" left=""1040"" width=""300"" height=""40"" href=""{2}"">
                                        <Text>Play post &quot;{3}&quot;</Text>
                                      </Button>",
                            HttpUtility.HtmlEncode(btnPostId),
                            topPos,
                            safeHref,
                            safeCaptionLabel
                        ));
                    }
                }

                // Print button
                try
                {
                    string messageForPrint = (prefix ?? "") + (text ?? "");
                    string printBase = "http://172.16.40.101/SETTEMediaroomApp/ListPrinters.aspx";
                    string printQuery = "message=" + HttpUtility.UrlEncode(messageForPrint);
                    string printUrl = printBase + "?" + printQuery;
                    string safePrintHref = HttpUtility.HtmlAttributeEncode(printUrl);

                    string btnPrintId = "PrintMsg_" + Guid.NewGuid().ToString("N");

                    sb.AppendLine(string.Format(
                        @"<Button id=""{0}"" top=""{1}"" left=""1240"" width=""200"" height=""40"" href=""page:{2}""><Text>Print</Text></Button>",
                        HttpUtility.HtmlEncode(btnPrintId),
                        topPos,
                        safePrintHref
                    ));
                }
                catch
                {
                }

                topPos += 60;
            }

            if (endIndex < total)
            {
                int nextPage = page + 1;

                string basePage = "http://172.16.40.101/SETTEMediaroomApp/DMThreadMessages.aspx";
                string loadQuery =
                    "thread_id=" + HttpUtility.UrlEncode(encodedThreadId) +
                    "&userid=" + HttpUtility.UrlEncode(userId) +
                    "&thread_name=" + HttpUtility.UrlEncode(threadTitle) +
                    "&page=" + HttpUtility.UrlEncode(nextPage.ToString()) +
                    "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

                string loadUrl = basePage + "?" + loadQuery;
                string safeHref = HttpUtility.HtmlAttributeEncode(loadUrl);

                sb.AppendLine(string.Format(
                    @"<Button id=""LoadMore"" top=""{0}"" left=""40"" width=""300"" height=""50"" href=""page:{1}""><Text>Load more</Text></Button>",
                    topPos + 10,
                    safeHref
                ));
            }
        }

        sb.AppendLine(@"</Panel>");
        sb.AppendLine(@"</MrmlPage>");
        sb.AppendLine(@"</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    private string BuildMessageText(Message m)
    {
        if (m == null)
            return null;

        if (!string.IsNullOrEmpty(m.message_text))
            return m.message_text;

        if (m.message_video != null)
            return "sent a video: " + (m.message_video.video_name ?? "[video]");

        if (m.message_obj != null)
        {
            var obj = m.message_obj;

            if (obj.type == "created_group")
            {
                string parts = "";
                if (obj.details != null && obj.details.participants_added != null && obj.details.participants_added.Count > 0)
                {
                    parts = string.Join(", ", obj.details.participants_added);
                    parts = " — added: " + parts;
                }

                return string.Format("{0} created group '{1}'{2}",
                    obj.actor_name ?? "[unknown]",
                    obj.group_name ?? "[unknown]",
                    parts);
            }

            if (obj.type == "renamed_group")
            {
                string oldt = (obj.details != null) ? obj.details.old_title : null;
                string newt = (obj.details != null) ? obj.details.new_title : null;
                return string.Format("{0} renamed group from '{1}' to '{2}'",
                    obj.actor_name ?? "[unknown]",
                    oldt ?? "[unknown]",
                    newt ?? "[unknown]");
            }

            if (obj.type == "song_listen_request")
            {
                var s = obj.song_listen_request;
                if (s != null)
                {
                    string title = s.title ?? "[unknown title]";
                    string artist = s.artist ?? s.display_artist ?? "";
                    if (!string.IsNullOrEmpty(artist))
                        return string.Format("sent a song play request: '{0}' by {1}", title, artist);
                    return string.Format("sent a song play request: '{0}'", title);
                }
                return "[song play request]";
            }

            if (obj.type == "music_event_share" && obj.music_event_share != null)
            {
                var ev = obj.music_event_share;
                string name = ev.event_name ?? "music event";
                return string.Format("sent a music event: {0}", name);
            }

            if (string.Equals(obj.type, "post", StringComparison.OrdinalIgnoreCase) || obj.post != null)
            {
                var p = obj.post;
                if (p != null)
                {
                    string who = (p.user != null && !string.IsNullOrEmpty(p.user.full_name))
                        ? p.user.full_name
                        : (p.user != null ? "@" + p.user.username : (obj.actor_name ?? "[someone]"));

                    string caption = p.caption ?? "";
                    if (!string.IsNullOrEmpty(caption))
                        return string.Format("{0} posted: '{1}'", who, caption);

                    if (!string.IsNullOrEmpty(p.video_url))
                    {
                        string filePart = "";
                        try { filePart = Path.GetFileName(new Uri(p.video_url).LocalPath); } catch { filePart = p.video_url; }
                        return string.Format("{0} posted a video: {1}", who, filePart);
                    }

                    return string.Format("{0} posted an update", who);
                }

                return "[post activity]";
            }

            return string.Format("[{0} activity]", obj.type ?? "activity");
        }

        return null;
    }

    private void RenderMusicEventShareCard(StringBuilder sb, MusicEventShare share, int topPos)
    {
        if (share == null)
            return;

        string imageUrl = BuildMusicEventImageProxyUrl(share.event_image);
        string title = share.event_name ?? "music event";
        string date = share.event_date ?? "";
        string price = share.event_price ?? "";

        sb.AppendLine(string.Format(
            @"<Image top=""{0}"" left=""40"" width=""90"" height=""60"" url=""{1}"" />",
            topPos,
            HttpUtility.HtmlAttributeEncode(imageUrl)
        ));

        sb.AppendLine(string.Format(
            @"<Text top=""{0}"" left=""150"" width=""1020"" height=""26"" fontstyle=""Reg24"" foreground=""argb(255,255,255,255)"">sent a music event: {1}</Text>",
            topPos,
            HttpUtility.HtmlEncode(title)
        ));

        int y = topPos + 28;

        if (!string.IsNullOrEmpty(date))
        {
            sb.AppendLine(string.Format(
                @"<Text top=""{0}"" left=""150"" width=""1020"" height=""22"" fontstyle=""Reg18"" foreground=""argb(255,200,200,200)"">Date: {1}</Text>",
                y,
                HttpUtility.HtmlEncode(date)
            ));
            y += 22;
        }

        if (!string.IsNullOrEmpty(price))
        {
            sb.AppendLine(string.Format(
                @"<Text top=""{0}"" left=""150"" width=""1020"" height=""22"" fontstyle=""Reg18"" foreground=""argb(255,200,200,200)"">Price: {1} den.</Text>",
                y,
                HttpUtility.HtmlEncode(price)
            ));
        }
    }

    private string BuildMusicEventImageProxyUrl(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return "";

        string absoluteUrl = imageUrl;

        if (!imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            absoluteUrl = "https://www.karti.com.mk/" + imageUrl.TrimStart('/');
        }

        return MUSIC_EVENT_IMAGE_PROXY_BASE + HttpUtility.UrlEncode(absoluteUrl);
    }

    private List<Message> GetMessages(string threadId, string userId)
    {
        try
        {
            string url = "http://172.16.40.100/dm_api.php?action=list&query_secret=supersecure123&thread_id="
                         + HttpUtility.UrlEncode(threadId)
                         + "&userid=" + HttpUtility.UrlEncode(userId);

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 7000;

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
            {
                string json = sr.ReadToEnd();

                JavaScriptSerializer js = new JavaScriptSerializer();
                var data = js.Deserialize<MessageListResponse>(json);
                if (data == null || data.messages == null)
                    return new List<Message>();

                foreach (var m in data.messages)
                {
                    if (m.raw_message == null)
                        continue;

                    if (m.raw_message is string)
                    {
                        m.message_text = (string)m.raw_message;
                    }
                    else
                    {
                        try
                        {
                            string serialized = js.Serialize(m.raw_message);

                            try
                            {
                                var j = JObject.Parse(serialized);
                                var typeToken = j["type"];
                                if (typeToken != null &&
                                    typeToken.Type == JTokenType.String &&
                                    string.Equals((string)typeToken, "video", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        m.message_video = js.Deserialize<VideoMessage>(serialized);
                                        if (m.message_video != null)
                                        {
                                            m.message_video.video_url = ReplaceDdnsHost(m.message_video.video_url);
                                            m.message_text = "sent a video: " + (m.message_video.video_name ?? "[video]");
                                        }
                                    }
                                    catch
                                    {
                                        m.message_video = null;
                                        m.message_text = null;
                                    }

                                    continue;
                                }
                            }
                            catch
                            {
                            }

                            try
                            {
                                m.message_obj = js.Deserialize<ActivityMessage>(serialized);
                                if (m.message_obj != null && m.message_obj.post != null)
                                {
                                    m.message_obj.post.video_url = ReplaceDdnsHost(m.message_obj.post.video_url);
                                }
                            }
                            catch
                            {
                                m.message_obj = null;
                            }
                        }
                        catch
                        {
                            m.message_obj = null;
                        }
                    }
                }

                return data.messages;
            }
        }
        catch
        {
            return new List<Message>();
        }
    }

    private bool SendMessageToThread(string userId, string threadId, string message)
    {
        try
        {
            string url = "http://172.16.40.100/dm_api.php?action=send&query_secret=supersecure123&userid="
                         + HttpUtility.UrlEncode(userId);

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Timeout = 10000;

            var bodyObj = new
            {
                thread_id = threadId,
                message = message
            };

            string json = new JavaScriptSerializer().Serialize(bodyObj);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            req.ContentLength = bytes.Length;

            using (var stream = req.GetRequestStream())
                stream.Write(bytes, 0, bytes.Length);

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                return ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300);
        }
        catch
        {
            return false;
        }
    }

    private string ReplaceDdnsHost(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;

        try
        {
            return Regex.Replace(
                url,
                @"https?:\/\/(?:www\.)?lukaserver\.ddns\.net",
                "http://172.16.40.100",
                RegexOptions.IgnoreCase
            );
        }
        catch
        {
            return url;
        }
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

    private void AppendInlineMessageWithMentions(StringBuilder sb, string displayText, int topPos, int leftPos, string userId)
    {
        int x = leftPos;
        int y = topPos;

        // Split into plain text and @mention tokens
        string[] parts = Regex.Split(displayText ?? "", @"(@[A-Za-z0-9._]+)");

        foreach (string part in parts)
        {
            if (string.IsNullOrEmpty(part))
                continue;

            bool isMention = Regex.IsMatch(part, @"^@[A-Za-z0-9._]+$");

            if (isMention)
            {
                string cleanUsername = part.Substring(1);

                string profileUrl =
                    "http://172.16.40.101/SETTEMediaroomApp/ViewProfile.aspx?username="
                    + HttpUtility.UrlEncode(cleanUsername)
                    + "&selected_user_id=" + HttpUtility.UrlEncode(userId)
                    + "&user_id=" + HttpUtility.UrlEncode(userId);

                string safeHref = HttpUtility.HtmlAttributeEncode(profileUrl);
                string safeText = HttpUtility.HtmlEncode(part);

                int width = EstimateInlineWidth(part);

                sb.AppendLine(string.Format(
                    @"<Button top=""{0}"" left=""{1}"" width=""{2}"" height=""34"" href=""page:{3}"">
                        <Text fontstyle=""Reg28"" foreground=""argb(255,0,122,255)"">{4}</Text>
                      </Button>",
                    y, x, width, safeHref, safeText
                ));

                x += width + 4;
            }
            else
            {
                string safeText = HttpUtility.HtmlEncode(part);
                int width = EstimateInlineWidth(part);

                sb.AppendLine(string.Format(
                    @"<Text top=""{0}"" left=""{1}"" width=""{2}"" height=""34"" fontstyle=""Reg28"" foreground=""argb(255,255,255,255)"">{3}</Text>",
                    y, x, width, safeText
                ));

                x += width;
            }
        }
    }

    private int EstimateInlineWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 8;

        int width = text.Length * 14;

        if (text.Length <= 2) width = 28;
        if (text.Length <= 5) width = Math.Max(width, 60);
        if (text.StartsWith("@")) width += 10;

        return Math.Min(Math.Max(width, 20), 900);
    }

    private string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || max <= 0) return "";
        if (s.Length <= max) return s;
        return s.Substring(0, max).TrimEnd() + "...";
    }

    // ======= Models =======

    public class MessageListResponse
    {
        public List<Message> messages { get; set; }
    }

    public class Message
    {
        public int sender_id { get; set; }
        public long timestamp { get; set; }
        public bool is_activity { get; set; }

        public object message { get; set; }

        [System.Web.Script.Serialization.ScriptIgnore]
        public object raw_message
        {
            get { return this.message; }
            set { this.message = value; }
        }

        [System.Web.Script.Serialization.ScriptIgnore]
        public string message_text { get; set; }

        [System.Web.Script.Serialization.ScriptIgnore]
        public ActivityMessage message_obj { get; set; }

        [System.Web.Script.Serialization.ScriptIgnore]
        public VideoMessage message_video { get; set; }
    }

    public class ActivityMessage
    {
        public string type { get; set; }
        public string actor_name { get; set; }
        public string group_name { get; set; }
        public ActivityDetails details { get; set; }

        public SongListenRequest song_listen_request { get; set; }
        public PostActivity post { get; set; }
        public MusicEventShare music_event_share { get; set; }
    }

    public class ActivityDetails
    {
        public string old_title { get; set; }
        public string new_title { get; set; }
        public List<int> participants_added { get; set; }
    }

    public class SongListenRequest
    {
        public string cover_art_url { get; set; }
        public string title { get; set; }
        public string display_artist { get; set; }
        public string artist { get; set; }
        public string song_url { get; set; }
        public object message { get; set; }
    }

    public class PostActivity
    {
        public object id { get; set; }
        public PostUser user { get; set; }
        public string caption { get; set; }
        public string video_url { get; set; }
        public object song { get; set; }
        public string timestamp { get; set; }
    }

    public class PostUser
    {
        public int user_id { get; set; }
        public string username { get; set; }
        public string full_name { get; set; }
        public string profile_picture_url { get; set; }
    }

    public class MusicEventShare
    {
        public string event_image { get; set; }
        public string event_name { get; set; }
        public string event_description { get; set; }
        public string event_price { get; set; }
        public string event_location { get; set; }
        public string event_venue { get; set; }
        public string event_date { get; set; }
        public string event_buy_ticket_url { get; set; }
    }

    public class VideoMessage
    {
        public string type { get; set; }
        public string video_name { get; set; }
        public string video_url { get; set; }
    }
}