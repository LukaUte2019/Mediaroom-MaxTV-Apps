using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.Script.Serialization;
using System.Linq;
using Newtonsoft.Json.Linq;

public partial class SendTo : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        // --- incoming query params ---
        string userId = Request.QueryString["userid"];
        string message = Request.QueryString["message"];
        string videoNameParam = Request.QueryString["video_name"]; // may contain a friendly name or URL
        string threadId = Request.QueryString["thread_id"];
        string rawSearch = (GetSearchFromRequest() ?? "").Trim();
        string pageStr = Request.QueryString["page"];
        string debugStr = Request.QueryString["debug"];
        bool debug = !string.IsNullOrEmpty(debugStr) && debugStr == "1";

        // normalize / trim
        message = (message ?? "").Trim();
        videoNameParam = (videoNameParam ?? "").Trim();
        string providedVideoName = string.IsNullOrEmpty(videoNameParam) ? null : HttpUtility.UrlDecode(videoNameParam).Trim();

        // --- Determine how we will display and what we'll include in thread hrefs ---
        // We want MRML to show providedVideoName when present.
        // For href/message param: if original message is MP4, keep message param = mp4 URL (so clicking thread will send the video).
        // Otherwise if providedVideoName exists, use that as message param for href (so clicking thread will send that text).
        string decodedOriginalMessage = string.IsNullOrEmpty(message) ? "" : HttpUtility.UrlDecode(message);
        string originalMp4Url, originalMp4Name;
        bool originalIsMp4 = IsMp4Url(decodedOriginalMessage, out originalMp4Url, out originalMp4Name);

        // defaultMessageForDisplay is what appears under the header (MRML)
        string defaultMessageForDisplay;
        // messageParamForHref is what we include as &message= when building per-thread links
        string messageParamForHref;

        if (originalIsMp4)
        {
            // If original message is a .mp4 URL:
            // - display: video_name param if present, otherwise clean filename from URL
            defaultMessageForDisplay = providedVideoName ?? CleanVideoName(originalMp4Name);
            // - when selecting thread we must keep message param as the mp4 URL so send flow knows to send a video
            messageParamForHref = originalMp4Url;
        }
        else
        {
            // Not an mp4 URL
            if (!string.IsNullOrEmpty(providedVideoName))
            {
                // If a video_name was given, show that and use it as the message param (text) for hrefs
                defaultMessageForDisplay = providedVideoName;
                messageParamForHref = providedVideoName;
            }
            else
            {
                // otherwise show/send original message
                defaultMessageForDisplay = string.IsNullOrEmpty(decodedOriginalMessage) ? "" : decodedOriginalMessage;
                messageParamForHref = message; // could be empty
            }
        }

        // If message was empty but video_name provided, we don't overwrite the original message variable here
        // — we use messageParamForHref and providedVideoName where needed so behavior is explicit.

        // If we're submitting now (thread + message present) — send it.
        if (!string.IsNullOrEmpty(threadId) && !string.IsNullOrEmpty(messageParamForHref) && !string.IsNullOrEmpty(userId))
        {
            // Determine what to actually send based on the incoming message param in this request.
            // The current Request.QueryString["message"] may be the mp4 URL or the friendly name (depending on how page was called).
            string incomingMsgRaw = (Request.QueryString["message"] ?? "").Trim();
            string incomingDecoded = string.IsNullOrEmpty(incomingMsgRaw) ? "" : HttpUtility.UrlDecode(incomingMsgRaw);

            string sendConfDisplay = defaultMessageForDisplay; // what to show on confirmation
            string tmpUrl2, tmpName2;
            bool isVideoMessageNow = IsMp4Url(incomingDecoded, out tmpUrl2, out tmpName2);

            if (isVideoMessageNow)
            {
                // If it's a video URL, we want to send the MP4 URL, but use providedVideoName (if present) as video_name
                // SendMessagePostJson expects (userId, threadId, message, videoNameParam)
                bool sent = SendMessagePostJson(userId, threadId, tmpUrl2, providedVideoName);
                RenderConfirmation(sent ? "Message sent!" : "Failed to send message.", sendConfDisplay, threadId, userId, true);
                return;
            }
            else
            {
                // Not a video URL: send text. If providedVideoName exists we should send that text; otherwise send incomingDecoded.
                string textToSend = !string.IsNullOrEmpty(providedVideoName) ? providedVideoName : incomingDecoded;
                bool sent = SendMessagePostJson(userId, threadId, textToSend, null);
                RenderConfirmation(sent ? "Message sent!" : "Failed to send message.", textToSend, threadId, userId, false);
                return;
            }
        }

        if (string.IsNullOrEmpty(userId))
        {
            RenderError("Missing userid.");
            return;
        }

        string searchTerm = (rawSearch ?? "").Trim();

        string apiDebug;
        List<MessageThread> threads = GetThreads(userId, searchTerm, debug, out apiDebug);

        int page = 1;
        int pageSize = 6;
        int pageParsed;
        if (int.TryParse(pageStr, out pageParsed) && pageParsed > 0)
            page = pageParsed;

        int totalThreads = threads.Count;
        int totalPages = (int)Math.Ceiling(totalThreads / (double)pageSize);
        var pagedThreads = threads.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        // Use defaultMessageForDisplay for MRML message text
        string displayMessage = defaultMessageForDisplay;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"<MrmlPage id=""SendToList"" appid=""lukatube.dm/1.0"" width=""1280"" height=""720"">");

        string submitBase = "http://172.16.40.101/SETTEMediaroomApp/SendTo.aspx";
        var submitQs = HttpUtility.ParseQueryString(string.Empty);
        submitQs["userid"] = userId;
        // put the messageParamForHref into default submit message so search action preserves it
        if (!string.IsNullOrEmpty(messageParamForHref))
            submitQs["message"] = messageParamForHref;
        if (!string.IsNullOrEmpty(videoNameParam))
            submitQs["video_name"] = videoNameParam;
        string submitUrlWithPlaceholder = submitBase + "?" + submitQs.ToString() + (submitQs.Count > 0 ? "&" : "");

        sb.AppendLine(@"<Actions>");
        sb.AppendLine(string.Format(
            @"  <Action name=""SearchThreads"" type=""submit"" data=""SearchSendTo"" method=""GET"" url=""page:{0}"" />",
            HttpUtility.HtmlAttributeEncode(submitUrlWithPlaceholder)
        ));
        sb.AppendLine(@"</Actions>");

        sb.AppendLine(@"<Panel>");
        sb.AppendLine(@"  <Text top=""20"" left=""40"" width=""1200"" height=""36"" fontstyle=""Reg28"" foreground=""argb(255,255,255,255)"">Send To</Text>");

        // show displayMessage — this will be video_name if provided, otherwise cleaned filename or original message
string finalDisplay = displayMessage;

// проверка дали е видео или има video_name
string tmpUrl;
string tmpName;
bool isVideo = IsMp4Url(displayMessage, out tmpUrl, out tmpName);

if (isVideo || !string.IsNullOrEmpty(videoNameParam))
{
    string nameToShow;

    if (!string.IsNullOrEmpty(videoNameParam))
        nameToShow = videoNameParam;      // користиме video_name од query
    else if (!string.IsNullOrEmpty(tmpName))
        nameToShow = tmpName;             // или име од IsMp4Url
    else
        nameToShow = "";                  // fallback ако нема име

    finalDisplay = string.IsNullOrEmpty(nameToShow) ? "Video" : "Video: " + nameToShow;
}

// додавање во MRML
sb.AppendLine(string.Format(
    @"  <Text top=""70"" left=""40"" width=""1200"" height=""36"" fontstyle=""Reg24"" foreground=""argb(255,200,200,200)"">{0}</Text>",
    HttpUtility.HtmlEncode(finalDisplay)));
        sb.AppendLine(string.Format(
@"  <EditText id=""SearchSendTo"" name=""SearchSendTo"" top=""110"" left=""40"" width=""900"" height=""48"" fontstyle=""Reg24"" value=""{0}""/>",
            HttpUtility.HtmlAttributeEncode(rawSearch)
        ));
        sb.AppendLine(string.Format(
            @"  <Button id=""btnSearch"" top=""110"" left=""960"" width=""280"" height=""48"">" +
              @"<Actions>" +
                @"<Event type=""onclick"" action=""SearchThreads"" />" +
              @"</Actions>" +
              @"<Text alignment=""center"" justification=""center"" fontstyle=""Reg24"" foreground=""argb(255,255,255,255)"">Search</Text>" +
            @"</Button>"
        ));

        int currentTop = 180;
        if (debug)
        {
            sb.AppendLine(string.Format(
                @"  <Text top=""{0}"" left=""40"" width=""1200"" height=""28"" fontstyle=""Reg20"" foreground=""argb(255,255,255,255)"">DEBUG: RawUrl: {1}</Text>",
                currentTop,
                HttpUtility.HtmlEncode(Request.RawUrl)
            ));
            currentTop += 36;
            sb.AppendLine(string.Format(
                @"  <Text top=""{0}"" left=""40"" width=""1200"" height=""20"" fontstyle=""Reg20"" foreground=""argb(255,255,200,100)"">HttpMethod: {1}</Text>",
                currentTop,
                HttpUtility.HtmlEncode(Request.HttpMethod)
            ));
            currentTop += 28;

            foreach (string key in Request.QueryString.AllKeys)
            {
                if (key == null) continue;
                string val = Request.QueryString[key] ?? "";
                sb.AppendLine(string.Format(
                    @"  <Text top=""{0}"" left=""40"" width=""1200"" height=""24"" fontstyle=""Reg20"" foreground=""argb(255,200,200,200)"">{1} = {2}</Text>",
                    currentTop,
                    HttpUtility.HtmlEncode(key),
                    HttpUtility.HtmlEncode(val)
                ));
                currentTop += 28;
            }

            if (Request.Form != null && Request.Form.Count > 0)
            {
                foreach (string fk in Request.Form.AllKeys)
                {
                    if (fk == null) continue;
                    string fv = Request.Form[fk] ?? "";
                    sb.AppendLine(string.Format(
                        @"  <Text top=""{0}"" left=""40"" width=""1200"" height=""20"" fontstyle=""Reg18"" foreground=""argb(255,180,180,180)"">{1} (FORM) = {2}</Text>",
                        currentTop,
                        HttpUtility.HtmlEncode(fk),
                        HttpUtility.HtmlEncode(fv)
                    ));
                    currentTop += 22;
                }
            }

            if (!string.IsNullOrEmpty(apiDebug))
            {
                string[] parts = apiDebug.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                string urlLine = parts.Length > 0 ? parts[0] : "";
                string respLine = parts.Length > 1 ? parts[1] : "";
                if (respLine.Length > 1000) respLine = respLine.Substring(0, 1000) + "...(truncated)";

                sb.AppendLine(string.Format(
                    @"  <Text top=""{0}"" left=""40"" width=""1200"" height=""20"" fontstyle=""Reg18"" foreground=""argb(255,200,255,200)"">API tried: {1}</Text>",
                    currentTop,
                    HttpUtility.HtmlEncode(urlLine)
                ));
                currentTop += 22;
                sb.AppendLine(string.Format(
                    @"  <Text top=""{0}"" left=""40"" width=""1200"" height=""60"" fontstyle=""Reg14"" foreground=""argb(255,200,200,200)"">{1}</Text>",
                    currentTop,
                    HttpUtility.HtmlEncode(respLine)
                ));
                currentTop += 68;
            }

            sb.AppendLine(string.Format(
                @"  <Text top=""{0}"" left=""40"" width=""1200"" height=""28"" fontstyle=""Reg20"" foreground=""argb(255,255,255,255)"">Received search: {1}</Text>",
                currentTop,
                HttpUtility.HtmlEncode(searchTerm)
            ));
            currentTop += 36;
        }

        int topPos = debug ? (currentTop) : 180;

        if (pagedThreads == null || pagedThreads.Count == 0)
        {
            sb.AppendLine(string.Format(
                @"  <Text top=""{0}"" left=""40"" width=""1200"" height=""36"" fontstyle=""Reg28"" foreground=""argb(255,255,60,60)"">No threads found</Text>",
                topPos
            ));
        }
        else
        {
            foreach (var t in pagedThreads)
            {
                string primaryText = HttpUtility.HtmlEncode(!string.IsNullOrEmpty(t.title) ? t.title : t.thread_id);

                string secondaryText = "";
                if (t.is_direct)
                {
                    if (!string.IsNullOrEmpty(t.thread_username))
                        secondaryText = HttpUtility.HtmlEncode(t.thread_username);
                    else
                        secondaryText = "";
                }
                else
                {
                    if (t.participants != null && t.participants.Count > 0)
                    {
                        var names = t.participants.Select(p => p.username ?? p.full_name ?? p.user_id.ToString()).ToList();
                        secondaryText = HttpUtility.HtmlEncode(string.Join(", ", names));
                    }
                    else
                    {
                        secondaryText = "";
                    }
                }

                string avatarTag = "";
                if (!string.IsNullOrEmpty(t.profile_picture_url))
                {
                    string avatarUrl = t.profile_picture_url;
                    if (avatarUrl.Contains("lukaserver.ddns.net"))
                    {
                        avatarUrl = avatarUrl
                            .Replace("https://lukaserver.ddns.net", "http://172.16.40.100")
                            .Replace("http://lukaserver.ddns.net", "http://172.16.40.100");
                    }
                    avatarUrl = HttpUtility.HtmlAttributeEncode(avatarUrl);

                    avatarTag = string.Format("<Image top=\"0\" left=\"0\" width=\"70\" height=\"70\" url=\"{0}\" />", avatarUrl);
                }

                // Build the href for this thread: include userid + thread_id + message + (video_name if present)
                string baseUrl = "http://172.16.40.101/SETTEMediaroomApp/SendTo.aspx";
                var hrefBuilder = new StringBuilder();
                hrefBuilder.Append(baseUrl);
                hrefBuilder.Append("?userid=").Append(HttpUtility.UrlEncode(userId));
                hrefBuilder.Append("&thread_id=").Append(HttpUtility.UrlEncode(t.thread_id));
                if (!string.IsNullOrEmpty(messageParamForHref))
                    hrefBuilder.Append("&message=").Append(HttpUtility.UrlEncode(messageParamForHref));
                if (!string.IsNullOrEmpty(videoNameParam))
                    hrefBuilder.Append("&video_name=").Append(HttpUtility.UrlEncode(videoNameParam));
                if (!string.IsNullOrEmpty(searchTerm))
                    hrefBuilder.Append("&SearchSendTo=").Append(HttpUtility.UrlEncode(searchTerm));

                string href = hrefBuilder.ToString();
                string safeId = "btn_" + SanitizeId(t.thread_id);

                string textLeft = string.IsNullOrEmpty(avatarTag) ? "40" : "120";

                sb.AppendLine(string.Format(
                    @"  <Button id=""{0}"" top=""{1}"" left=""40"" width=""1200"" height=""70"" href=""page:{2}"">
            {3}
            <Text top=""5"" left=""{4}"" alignment=""left"" justification=""left"" fontstyle=""Reg28"" foreground=""argb(255,255,255,255)"">{5}</Text>
            <Text top=""35"" left=""{4}"" alignment=""left"" justification=""left"" fontstyle=""Reg24"" foreground=""argb(255,200,200,200)"">{6}</Text>
        </Button>",
                    HttpUtility.HtmlAttributeEncode(safeId),
                    topPos,
                    HttpUtility.HtmlAttributeEncode(href),
                    avatarTag,
                    textLeft,
                    primaryText,
                    secondaryText
                ));

                topPos += 80;
            }

            if (page < totalPages)
            {
                string nextPageUrl = "http://172.16.40.101/SETTEMediaroomApp/SendTo.aspx"
                    + "?userid=" + HttpUtility.UrlEncode(userId)
                    + "&message=" + HttpUtility.UrlEncode(messageParamForHref)
                    + "&page=" + (page + 1);

                if (!string.IsNullOrEmpty(videoNameParam))
                    nextPageUrl += "&video_name=" + HttpUtility.UrlEncode(videoNameParam);

                if (!string.IsNullOrEmpty(searchTerm))
                    nextPageUrl += "&SearchSendTo=" + HttpUtility.UrlEncode(searchTerm);

                sb.AppendLine(string.Format(
                    @"  <Button id=""btn_load_more"" top=""{0}"" left=""40"" width=""1200"" height=""60"" href=""page:{1}"">" +
                    @"<Text alignment=""center"" justification=""center"" fontstyle=""Reg28"" foreground=""argb(255,255,255,255)"">Load More Chats</Text>" +
                    @"</Button>",
                    topPos,
                    HttpUtility.HtmlAttributeEncode(nextPageUrl)
                ));
            }
        }

        sb.AppendLine(@"</Panel>");
        sb.AppendLine(@"</MrmlPage>");
        sb.AppendLine(@"</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    // ---------- Helpers (unchanged) ----------

    private List<MessageThread> GetThreads(string userId, string search, bool debug, out string apiDebug)
    {
        // (same implementation as earlier full file)
        apiDebug = null;
        var result = new List<MessageThread>();
        string searchSafe = (search ?? "").Trim();

        int viewerId = 0;
        int.TryParse(userId, out viewerId);

        string baseUrl = "http://172.16.40.100/dm_api.php?action=list_threads&query_secret=supersecure123&userid=" + HttpUtility.UrlEncode(userId);

        string[] paramNames = new[] { "search", "query", "q", "SearchSendTo", "SearchLukaTube", "term", "s" };

        foreach (var pname in paramNames)
        {
            try
            {
                string url = baseUrl + "&" + pname + "=" + HttpUtility.UrlEncode(searchSafe);
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = 10000;
                req.UserAgent = "LukaTube/1.0";

                string respBody = null;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream()))
                {
                    respBody = sr.ReadToEnd();
                }

                if (debug)
                    apiDebug = url + "\r\n" + (respBody ?? "");

                if (string.IsNullOrEmpty(respBody)) continue;

                try
                {
                    var j = JObject.Parse(respBody);

                    JToken threadsToken = null;
                    if (j["threads"] != null && j["threads"].Type == JTokenType.Array)
                        threadsToken = j["threads"];
                    else if (j["data"] != null && j["data"]["threads"] != null && j["data"]["threads"].Type == JTokenType.Array)
                        threadsToken = j["data"]["threads"];
                    else if (j["results"] != null && j["results"].Type == JTokenType.Array)
                        threadsToken = j["results"];
                    else if (j["items"] != null && j["items"].Type == JTokenType.Array)
                        threadsToken = j["items"];
                    else if (j.Type == JTokenType.Array)
                        threadsToken = j;

                    if (threadsToken != null)
                    {
                        foreach (var it in threadsToken.Children())
                        {
                            try
                            {
                                var mt = new MessageThread();
                                mt.thread_id = it.Value<string>("thread_id") ?? it.Value<string>("id") ?? it.Value<string>("threadId");
                                mt.title = it.Value<string>("title") ?? it.Value<string>("name") ?? "";

                                string[] threadImageKeys = new[] { "profile_picture_url", "profile_picture", "avatar", "image", "picture", "photo", "thread_image", "image_url", "thumbnail", "thumb", "thread_pic" };
                                mt.profile_picture_url = "";
                                foreach (var k in threadImageKeys)
                                {
                                    try
                                    {
                                        var v = it.Value<string>(k);
                                        if (!string.IsNullOrEmpty(v))
                                        {
                                            mt.profile_picture_url = v;
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                                if (string.IsNullOrEmpty(mt.profile_picture_url) && it["thread"] != null && it["thread"].Type == JTokenType.Object)
                                {
                                    foreach (var k in threadImageKeys)
                                    {
                                        try
                                        {
                                            var v = it["thread"].Value<string>(k);
                                            if (!string.IsNullOrEmpty(v))
                                            {
                                                mt.profile_picture_url = v;
                                                break;
                                            }
                                        }
                                        catch { }
                                    }
                                }

                                string[] threadUsernameKeys = new[] { "thread_username", "other_username", "with_user", "peer_username", "participant_username", "thread_user", "username", "other_user" };
                                mt.thread_username = "";
                                foreach (var k in threadUsernameKeys)
                                {
                                    try
                                    {
                                        var v = it.Value<string>(k);
                                        if (!string.IsNullOrEmpty(v))
                                        {
                                            mt.thread_username = v;
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                                if (string.IsNullOrEmpty(mt.thread_username) && it["thread"] != null && it["thread"].Type == JTokenType.Object)
                                {
                                    foreach (var k in threadUsernameKeys)
                                    {
                                        try
                                        {
                                            var v = it["thread"].Value<string>(k);
                                            if (!string.IsNullOrEmpty(v))
                                            {
                                                mt.thread_username = v;
                                                break;
                                            }
                                        }
                                        catch { }
                                    }
                                }

                                var parts = new List<Participant>();
                                JToken ptoken = null;
                                if (it["participants"] != null && it["participants"].Type == JTokenType.Array)
                                    ptoken = it["participants"];
                                else if (it["members"] != null && it["members"].Type == JTokenType.Array)
                                    ptoken = it["members"];

                                if (ptoken != null)
                                {
                                    foreach (var p in ptoken.Children())
                                    {
                                        try
                                        {
                                            var part = new Participant();
                                            int uid = 0;
                                            int.TryParse((p.Value<string>("user_id") ?? p.Value<string>("id") ?? p.Value<string>("userId") ?? "0"), out uid);
                                            part.user_id = uid;
                                            part.username = p.Value<string>("username") ?? p.Value<string>("user_name") ?? "";
                                            part.full_name = p.Value<string>("full_name") ?? p.Value<string>("name") ?? "";
                                            part.profile_picture_url = p.Value<string>("profile_picture_url") ?? p.Value<string>("avatar") ?? p.Value<string>("image") ?? "";
                                            parts.Add(part);
                                        }
                                        catch { }
                                    }
                                }

                                mt.participants = parts;

                                bool isDirect = false;
                                if (parts.Count == 1)
                                {
                                    isDirect = true;
                                }
                                else if (parts.Count == 2 && viewerId > 0)
                                {
                                    if (parts.Any(p => p.user_id == viewerId))
                                        isDirect = true;
                                }
                                mt.is_direct = isDirect;

                                if (isDirect && string.IsNullOrEmpty(mt.thread_username))
                                {
                                    if (parts.Count == 1)
                                    {
                                        var other = parts[0];
                                        mt.thread_username = !string.IsNullOrEmpty(other.username) ? other.username : other.full_name;
                                    }
                                    else if (parts.Count == 2 && viewerId > 0)
                                    {
                                        var other = parts.FirstOrDefault(p => p.user_id != viewerId) ?? parts.FirstOrDefault();
                                        if (other != null)
                                            mt.thread_username = !string.IsNullOrEmpty(other.username) ? other.username : other.full_name;
                                    }
                                }

                                if (mt.is_direct && string.IsNullOrEmpty(mt.title) && !string.IsNullOrEmpty(mt.thread_username))
                                {
                                    mt.title = mt.thread_username;
                                }

                                if (string.IsNullOrEmpty(mt.thread_id))
                                    mt.thread_id = (mt.title ?? "thread") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);

                                result.Add(mt);
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        var firstArray = j.DescendantsAndSelf().OfType<JArray>().FirstOrDefault();
                        if (firstArray != null)
                        {
                            foreach (var it in firstArray.Children())
                            {
                                try
                                {
                                    var mt = new MessageThread();
                                    mt.thread_id = it.Value<string>("thread_id") ?? it.Value<string>("id") ?? "";
                                    mt.title = it.Value<string>("title") ?? "";

                                    string[] threadImageKeys = new[] { "profile_picture_url", "profile_picture", "avatar", "image", "picture", "photo", "thread_image", "image_url", "thumbnail", "thumb", "thread_pic" };
                                    mt.profile_picture_url = "";
                                    foreach (var k in threadImageKeys)
                                    {
                                        try
                                        {
                                            var v = it.Value<string>(k);
                                            if (!string.IsNullOrEmpty(v))
                                            {
                                                mt.profile_picture_url = v;
                                                break;
                                            }
                                        }
                                        catch { }
                                    }

                                    mt.participants = new List<Participant>();
                                    result.Add(mt);
                                }
                                catch { }
                            }
                        }
                    }

                    if (result.Count > 0)
                        return result;
                }
                catch (Exception)
                {
                    try
                    {
                        var js = new JavaScriptSerializer();
                        var obj = js.DeserializeObject(respBody);
                        if (obj is IDictionary<string, object>)
                        {
                            var dict = (IDictionary<string, object>)obj;
                            if (dict.ContainsKey("threads") && dict["threads"] is Array)
                            {
                                // continue - handled above if needed
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (WebException) { }
            catch (Exception) { }
        }

        return result;
    }

    private string GetSearchFromRequest()
    {
        string[] keys = new[] { "SearchSendTo", "SearchSendto", "search", "Search", "query", "Query", "q", "Q", "txtQuery" };
        foreach (var k in keys)
        {
            string v = Request.QueryString[k];
            if (!string.IsNullOrEmpty(v))
                return HttpUtility.UrlDecode(v);

            if (Request.Form != null)
            {
                string fv = Request.Form[k];
                if (!string.IsNullOrEmpty(fv))
                    return HttpUtility.HtmlDecode(fv);
            }
        }
        return "";
    }

    /// <summary>
    /// Send message to dm_api.php. If the message is a video URL, choose the video_name
    /// by preferring videoNameParam when present; otherwise extract the filename from mp4 URL.
    /// </summary>
    private bool SendMessagePostJson(string userId, string threadId, string message, string videoNameParam)
    {
        try
        {
            string url = "http://172.16.40.100/dm_api.php?action=send&query_secret=supersecure123&userid="
                         + HttpUtility.UrlEncode(userId);

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Timeout = 10000;

            string decodedMessage = string.IsNullOrEmpty(message) ? "" : HttpUtility.UrlDecode(message);

            object bodyObj;

            // declare out variables
            string mp4Url;
            string mp4Name;

            if (IsMp4Url(decodedMessage, out mp4Url, out mp4Name))
            {
                // If caller provided a videoNameParam, use it verbatim (decoded + trimmed) as video_name.
                // Otherwise, fall back to the filename from the mp4 URL (cleaned).
                string chosenName;
                if (!string.IsNullOrEmpty(videoNameParam))
                {
                    chosenName = HttpUtility.UrlDecode(videoNameParam).Trim();
                }
                else
                {
                    chosenName = mp4Name ?? "";
                    chosenName = CleanVideoName(chosenName);
                }

                bodyObj = new
                {
                    thread_id = threadId,
                    message = new
                    {
                        type = "video",
                        video_name = chosenName,
                        video_url = mp4Url ?? decodedMessage
                    }
                };
            }
            else
            {
                bodyObj = new
                {
                    thread_id = threadId,
                    message = decodedMessage
                };
            }

            string jsonBody = new JavaScriptSerializer().Serialize(bodyObj);
            byte[] bytes = Encoding.UTF8.GetBytes(jsonBody);
            req.ContentLength = bytes.Length;

            using (var reqStream = req.GetRequestStream())
                reqStream.Write(bytes, 0, bytes.Length);

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
            {
                int code = (int)resp.StatusCode;
                return code >= 200 && code < 300;
            }
        }
        catch
        {
            return false;
        }
    }

    private bool IsMp4Url(string s, out string url, out string name)
    {
        url = null;
        name = null;
        if (string.IsNullOrEmpty(s)) return false;

        // 1) valid absolute URI with path ending in .mp4
        Uri uri;
        if (Uri.TryCreate(s, UriKind.Absolute, out uri))
        {
            var path = uri.AbsolutePath ?? "";
            if (path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                url = s;
                try { name = Path.GetFileName(path); } catch { name = s; }
                return true;
            }
        }

        // 2) relative/local path ending with .mp4
        if (s.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            url = s;
            try { name = Path.GetFileName(s); } catch { name = s; }
            return true;
        }

        return false;
    }

    private string CleanVideoName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";

        int qidx = name.IndexOf('?');
        if (qidx >= 0) name = name.Substring(0, qidx);

        try { name = Path.GetFileName(name); } catch { }

        if (name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 4);

        // only replace underscores with space; KEEP original case
        name = name.Replace("_", " ");
        name = System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\s+", " ");

        return name;
    }

    private void RenderConfirmation(string text, string message, string threadId = null, string userId = null, bool isVideo = false, string videoName = null)
{
    string threadTitle = "Send To";
    if (!string.IsNullOrEmpty(threadId) && !string.IsNullOrEmpty(userId))
    {
        string tmpDebug;
        var threads = GetThreads(userId, "", false, out tmpDebug);
        var match = threads.FirstOrDefault(t => t.thread_id == threadId);
        if (match != null && !string.IsNullOrEmpty(match.title))
            threadTitle = match.title;
        else if (match != null && match.participants != null && match.participants.Count > 0)
        {
            var names = match.participants.Select(p => p.username ?? p.full_name ?? p.user_id.ToString()).ToList();
            threadTitle = string.Join(", ", names.Take(3));
        }
    }

    Response.Clear();
    Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
    Response.ContentEncoding = Encoding.UTF8;
    Response.Cache.SetNoStore();

    StringBuilder sb = new StringBuilder();
    sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
    sb.AppendLine(@"<uidescription version=""3.0"">");
    sb.AppendLine(@"<MrmlPage id=""SendToListConfirm"" appid=""lukatube.dm/1.0"" width=""1280"" height=""720"">");
    sb.AppendLine(@"<Panel>");
    sb.AppendLine(string.Format(
        @"<Text top=""50"" left=""40"" width=""1200"" height=""36"" fontstyle=""Reg28"" foreground=""argb(255,255,255,255)"">Message sent to: {0}</Text>",
        HttpUtility.HtmlEncode(threadTitle)
    ));

    // Show video name or link if isVideo
    if (isVideo)
    {
        string displayVideo = !string.IsNullOrEmpty(videoName) ? videoName : message;
        sb.AppendLine(string.Format(
            @"<Text top=""120"" left=""40"" width=""1200"" height=""36"" fontstyle=""Reg24"" foreground=""argb(255,200,200,200)"">Video: {0}</Text>",
            HttpUtility.HtmlEncode(displayVideo)));
    }
    else
    {
        sb.AppendLine(string.Format(
            @"<Text top=""120"" left=""40"" width=""1200"" height=""36"" fontstyle=""Reg24"" foreground=""argb(255,200,200,200)"">{0}</Text>",
            HttpUtility.HtmlEncode(message)));
    }

    sb.AppendLine(string.Format(
        @"<Text top=""380"" left=""40"" width=""1200"" height=""36"" fontstyle=""Reg28"" foreground=""argb(255,255,255,255)"">{0}</Text>",
        HttpUtility.HtmlEncode(text)));
    sb.AppendLine(@"</Panel>");
    sb.AppendLine(@"</MrmlPage>");
    sb.AppendLine(@"</uidescription>");

    Response.Write(sb.ToString());
    Response.End();
}

    private void RenderError(string text)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"<MrmlPage id=""SendToListError"" appid=""lukatube.dm/1.0"" width=""1280"" height=""720"">");
        sb.AppendLine(@"<Panel>");
        sb.AppendLine(string.Format(
            @"<Text top=""100"" left=""40"" width=""1200"" height=""36"" fontstyle=""Reg28"" foreground=""argb(255,255,60,60)"">{0}</Text>",
            HttpUtility.HtmlEncode(text)));
        sb.AppendLine(@"</Panel>");
        sb.AppendLine(@"</MrmlPage>");
        sb.AppendLine(@"</uidescription>");
        Response.Write(sb.ToString());
        Response.End();
    }

    private List<MessageThread> GetThreads_NoExceptions(string userId)
    {
        string dummy;
        return GetThreads(userId, "", false, out dummy);
    }

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

    // ======= Models =======
    public class DMThreadsResponse { public List<MessageThread> threads { get; set; } }
    public class MessageThread
    {
        public string thread_id { get; set; }
        public string title { get; set; }
        public string profile_picture_url { get; set; }
        public List<Participant> participants { get; set; }

        public string thread_username { get; set; }
        public bool is_direct { get; set; }
    }

    public class Participant { public int user_id { get; set; } public string username { get; set; } public string full_name { get; set; } public string profile_picture_url { get; set; } }
}