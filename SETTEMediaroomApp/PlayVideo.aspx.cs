using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Security;
using System.Linq;
using Newtonsoft.Json.Linq;

public partial class PlayVideo : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        bool globalOnlineServer = IsTrue(Request.QueryString["GlobalOnlineServer"]);
        bool openChannelTVHD = IsTrue(Request.QueryString["HideVideoInfo"]);

        string localBase = "http://172.16.40.101/youtubeclone/videos_mediaroom/";
        string remoteBase = GetRemoteBase(globalOnlineServer);
        string videoDirFs = Server.MapPath("~/youtubeclone/videos_mediaroom/");

        bool localFolder = IsTrue(Request.QueryString["LocalFolder"]);
        bool newVideosMode = IsTrue(Request.QueryString["newVideos"]);
        string searchQuery = Request.QueryString["SearchLukaTube"] ?? "";
        searchQuery = searchQuery.Trim();
        string sortMode = (Request.QueryString["sort"] ?? "").Trim().ToLowerInvariant();
        bool sortPlayedBy = (sortMode == "playedby");

        string remoteListingUrl = newVideosMode ? (remoteBase + "?C=M;O=D") : remoteBase;

        List<string> files = new List<string>();

        if (localFolder)
        {
            if (Directory.Exists(videoDirFs))
            {
                try
                {
                    string[] localFiles = Directory.GetFiles(videoDirFs);
                    for (int i = 0; i < localFiles.Length; i++)
                    {
                        string lf = localFiles[i];
                        if (Regex.IsMatch(lf, @"\.(mp4|m4v|mov)$", RegexOptions.IgnoreCase))
                        {
                            string name = Path.GetFileName(lf);
                            files.Add(localBase + HttpUtility.UrlEncode(name, Encoding.UTF8));
                        }
                    }

                    files.Sort(StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                }
            }
        }
        else
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(remoteListingUrl);
                req.Method = "GET";
                req.Timeout = 5000;
                req.UserAgent = "LukaTube/1.0";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string html = sr.ReadToEnd();
                    MatchCollection matches = Regex.Matches(html, "<a href=\"([^\"]+)\"", RegexOptions.IgnoreCase);

                    foreach (Match m in matches)
                    {
                        string fname = m.Groups[1].Value.Trim();
                        if (Regex.IsMatch(fname, @"\.(mp4|m4v|mov)$", RegexOptions.IgnoreCase))
                        {
                            files.Add(remoteBase + HttpUtility.UrlEncode(fname, Encoding.UTF8));
                        }
                    }

                    if (!newVideosMode)
                        files.Sort(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch
            {
            }
        }

        // Apply search filter only when SearchLukaTube exists
if (!string.IsNullOrEmpty(searchQuery))
{
    string searchLower = searchQuery.ToLowerInvariant();

    files = files.Where(delegate(string f)
    {
        string name = Path.GetFileName(f);

        try
        {
            name = HttpUtility.UrlDecode(name, Encoding.UTF8);
        }
        catch
        {
        }

        name = Path.GetFileNameWithoutExtension(name);
        name = name.Replace("_", " ");
        name = name.ToLowerInvariant();

        return name.Contains(searchLower);

    }).ToList();
}

        string videoUrl = Request.QueryString["video_url"];
        if (videoUrl == null) videoUrl = "";

        string videoName = Request.QueryString["video_name"];
        if (videoName == null) videoName = "";

        if (string.IsNullOrEmpty(videoUrl) && files.Count > 0)
        {
            videoUrl = files[0];
            string rawName = videoUrl.Split('/')[videoUrl.Split('/').Length - 1];
            videoName = HttpUtility.UrlDecode(rawName, Encoding.UTF8);
            videoName = Regex.Replace(videoName, @"\.(mp4|m4v|mov)$", "", RegexOptions.IgnoreCase);
        }

        videoName = (videoName ?? "").Replace("_", " ");
        videoName = Regex.Replace(videoName, @"\s+", " ").Trim();

        string artistName = "";
        string songTitle = "";
        SplitArtistAndSong(videoName, out artistName, out songTitle);

        if (string.IsNullOrEmpty(songTitle))
            songTitle = videoName;

        string sourceVideoUrl = videoUrl;

        string playbackVideoUrl = ResolveVideoUrlForPlayback(sourceVideoUrl, localFolder, localBase);
        if (string.IsNullOrEmpty(playbackVideoUrl))
            playbackVideoUrl = sourceVideoUrl;

        string searchSendTo = ExtractSearchSendTo(videoName);

        string videoChannel = GetVideoChannelFromApi(sourceVideoUrl);
        if (string.IsNullOrEmpty(videoChannel))
        {
            videoChannel = "";
        }

        List<string> artistVideos = new List<string>();

if (!string.IsNullOrEmpty(videoChannel))
{
    artistVideos = GetArtistVideosFromApi(videoChannel);
}

        string decodedVideoUrl = HttpUtility.UrlDecode(sourceVideoUrl ?? "");

        int idx = files.FindIndex(delegate (string f)
        {
            return string.Equals(
                HttpUtility.UrlDecode(f ?? ""),
                decodedVideoUrl,
                StringComparison.OrdinalIgnoreCase);
        });

        int safeIdx = (idx >= 0) ? idx : 0;

        string prevVideo = "";
        string nextVideo = "";
        string prevName = "";
        string nextName = "";

        if (files.Count > 0)
        {
            int prevIndex = (safeIdx - 1 + files.Count) % files.Count;
            int nextIndex = (safeIdx + 1) % files.Count;

            prevVideo = files[prevIndex];
            nextVideo = files[nextIndex];

            prevName = Path.GetFileNameWithoutExtension(prevVideo).Replace("_", " ");
            nextName = Path.GetFileNameWithoutExtension(nextVideo).Replace("_", " ");
        }

        int pageSize = 12;
        int tmpPageSize;
        if (int.TryParse(Request.QueryString["pageSize"], out tmpPageSize) && tmpPageSize > 0)
            pageSize = tmpPageSize;

        int offsetFromQuery;
        bool hasOffsetParam = int.TryParse(Request.QueryString["offset"], out offsetFromQuery) && offsetFromQuery >= 0;

        int indexFromQuery;
        bool hasIndexParam = int.TryParse(Request.QueryString["index"], out indexFromQuery) && indexFromQuery >= 0;

        int lukaOffset;
        if (hasOffsetParam)
            lukaOffset = offsetFromQuery;
        else if (hasIndexParam)
            lukaOffset = (indexFromQuery / pageSize) * pageSize;
        else
            lukaOffset = (safeIdx / pageSize) * pageSize;

        string basePathUrl = Request.Url.GetLeftPart(UriPartial.Path);
        string prevUrl = "";
        string nextUrl = "";

        if (!string.IsNullOrEmpty(prevVideo))
        {
            int prevIndex = (safeIdx - 1 + files.Count) % files.Count;
            int prevOffset = (prevIndex / pageSize) * pageSize;

            StringBuilder prevQs = new StringBuilder();
            prevQs.Append("?offset=").Append(prevOffset);
            prevQs.Append("&pageSize=").Append(pageSize);
            prevQs.Append("&index=").Append(prevIndex);
            prevQs.Append("&video_url=").Append(HttpUtility.UrlEncode(prevVideo));
            if (!string.IsNullOrEmpty(prevName))
                prevQs.Append("&video_name=").Append(HttpUtility.UrlEncode(prevName));

            prevQs.Append(GetDeviceQuerySuffix());
            prevQs.Append(GetNewVideosQuerySuffix(newVideosMode));
            prevQs.Append(GetSortQuerySuffix(sortMode));
            prevQs.Append(GetGlobalServerQuerySuffix(globalOnlineServer));
            if (!string.IsNullOrEmpty(searchQuery))
{
    prevQs.Append("&SearchLukaTube=");
    prevQs.Append(HttpUtility.UrlEncode(searchQuery));
}

            prevUrl = basePathUrl + prevQs.ToString();
        }

        if (!string.IsNullOrEmpty(nextVideo))
        {
            int nextIndex = (safeIdx + 1) % files.Count;
            int nextOffset = (nextIndex / pageSize) * pageSize;

            StringBuilder nextQs = new StringBuilder();
            nextQs.Append("?offset=").Append(nextOffset);
            nextQs.Append("&pageSize=").Append(pageSize);
            nextQs.Append("&index=").Append(nextIndex);
            nextQs.Append("&video_url=").Append(HttpUtility.UrlEncode(nextVideo));
            if (!string.IsNullOrEmpty(nextName))
                nextQs.Append("&video_name=").Append(HttpUtility.UrlEncode(nextName));

            nextQs.Append(GetDeviceQuerySuffix());
            nextQs.Append(GetNewVideosQuerySuffix(newVideosMode));
            nextQs.Append(GetSortQuerySuffix(sortMode));
            nextQs.Append(GetGlobalServerQuerySuffix(globalOnlineServer));
            if (!string.IsNullOrEmpty(searchQuery))
{
    nextQs.Append("&SearchLukaTube=");
    nextQs.Append(HttpUtility.UrlEncode(searchQuery));
}

            nextUrl = basePathUrl + nextQs.ToString();
        }

        string ua = Request.UserAgent ?? "";
        Func<string, string, string> ParseIPTVUserAgent = delegate (string uaStr, string language)
        {
            if (string.IsNullOrEmpty(uaStr))
                return (language == "mk-MK") ? "Непознат уред" : "Unknown device";

            string client = "";
            string clientVersion = "";
            string os = "";
            string mediaroomVersion = "";
            string vendor = "";
            string model = "";

            try
            {
                string[] parts = uaStr.Split(new char[] { '(' }, 2);
                string clientPart = parts[0].Trim();
                string[] clientParts = clientPart.Split('/');
                if (clientParts.Length > 0) client = clientParts[0].Trim();
                if (clientParts.Length > 1) clientVersion = clientParts[1].Trim();

                if (parts.Length > 1)
                {
                    string detailsPart = parts[1].TrimEnd(')');
                    string[] details = detailsPart.Split(';');
                    for (int i = 0; i < details.Length; i++) details[i] = details[i].Trim();

                    if (details.Length > 0) os = details[0];

                    for (int i = 0; i < details.Length; i++)
                    {
                        string d = details[i];
                        if (d.ToLower().StartsWith("mediaroom"))
                        {
                            mediaroomVersion = d.Substring("mediaroom".Length).Trim();
                            break;
                        }
                    }

                    if (details.Length > 0 && details[details.Length - 1].ToLower().EndsWith("hevc"))
                    {
                        Array.Resize(ref details, details.Length - 1);
                    }

                    if (details.Length >= 1) model = details[details.Length - 1];
                    if (details.Length >= 2) vendor = details[details.Length - 2];
                }
            }
            catch
            {
                return (uaStr.Length > 120) ? uaStr.Substring(0, 120) + "..." : uaStr;
            }

            string unknownStr = (language == "mk-MK") ? "Непознат уред" : "Unknown device";
            if (string.IsNullOrEmpty(os) && string.IsNullOrEmpty(mediaroomVersion) && string.IsNullOrEmpty(vendor) && string.IsNullOrEmpty(model))
                return unknownStr;

            client = client.Replace("_", " ");
            clientVersion = clientVersion.Replace("_", " ");
            os = os.Replace("_", " ");
            mediaroomVersion = mediaroomVersion.Replace("_", " ");
            vendor = vendor.Replace("_", " ");
            model = model.Replace("_", " ");

            return string.Format(
                "Client: {0} Version: {1} \nOS: {2} Mediaroom: {3} Vendor: {4} Model: {5}",
                client, clientVersion, os, mediaroomVersion, vendor, model
            );
        };

        string deviceInfo = ParseIPTVUserAgent(ua, "en-US").Replace("_", " ");

        string deviceGuid = Request.QueryString["DeviceGuid"];
        if (deviceGuid == null) deviceGuid = "";
        string userId = GetUserIdFromDeviceGuid(deviceGuid);

        Dictionary<string, int> playedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (sortPlayedBy && !string.IsNullOrEmpty(deviceGuid))
        {
            playedCounts = GetPlayedCountsFromStb(deviceGuid);
            if (playedCounts.Count > 0)
            {
                files.Sort(delegate (string a, string b)
                {
                    int ca = GetPlayCountForVideo(a, playedCounts);
                    int cb = GetPlayCountForVideo(b, playedCounts);

                    int cmp = cb.CompareTo(ca);
                    if (cmp != 0) return cmp;

                    string an = Path.GetFileNameWithoutExtension(a ?? "").Replace("_", " ").ToLowerInvariant();
                    string bn = Path.GetFileNameWithoutExtension(b ?? "").Replace("_", " ").ToLowerInvariant();
                    return string.Compare(an, bn, StringComparison.OrdinalIgnoreCase);
                });
            }
        }

        int viewCount = 0;
        if (!string.IsNullOrEmpty(deviceGuid) && !string.IsNullOrEmpty(sourceVideoUrl))
        {
            try
            {
                JObject payload = new JObject();
                payload.Add("deviceGuid", deviceGuid);
                payload.Add("localFolder", localFolder);
                payload.Add("videoUrl", sourceVideoUrl);
                payload.Add("videoName", videoName);
                payload.Add("playedAt", DateTime.UtcNow.ToString("o"));
                payload.Add("userAgent", Request.UserAgent ?? "");
                payload.Add("newVideos", newVideosMode);

                string apiUrl = "http://172.16.40.100/youtubeclone/storeplayedbyfromstb.php";
                HttpWebRequest apiReq = (HttpWebRequest)WebRequest.Create(apiUrl);
                apiReq.Method = "POST";
                apiReq.ContentType = "application/json; charset=utf-8";

                byte[] dataBytes = Encoding.UTF8.GetBytes(payload.ToString());
                apiReq.ContentLength = dataBytes.Length;

                using (Stream reqStream = apiReq.GetRequestStream())
                {
                    reqStream.Write(dataBytes, 0, dataBytes.Length);
                }

                using (HttpWebResponse apiResp = (HttpWebResponse)apiReq.GetResponse())
                using (StreamReader sr = new StreamReader(apiResp.GetResponseStream(), Encoding.UTF8))
                {
                    string respText = sr.ReadToEnd();
                    try
                    {
                        JObject respObj = JObject.Parse(respText);
                        if (respObj != null && respObj.Value<bool>("ok"))
                        {
                            JToken deviceToken = respObj["device"];
                            if (deviceToken != null)
                            {
                                JToken countsTok = deviceToken["videoPlayCounts"];
                                if (countsTok != null && countsTok.Type == JTokenType.Object)
                                {
                                    JObject countsObj = (JObject)countsTok;
                                    JToken vtok = countsObj[sourceVideoUrl];
                                    if (vtok == null)
                                    {
                                        string encodedKey = HttpUtility.UrlEncode(sourceVideoUrl);
                                        if (countsObj[encodedKey] != null) vtok = countsObj[encodedKey];
                                    }
                                    if (vtok != null)
                                    {
                                        int tmp;
                                        if (int.TryParse(vtok.ToString(), out tmp)) viewCount = tmp;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
                viewCount = 0;
            }
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"  <MrmlPage id=""TVPage"" appid=""lukatube.app/1.0"" width=""1280"" height=""720"">");

        sb.AppendLine("  <DataSource id=\"TVSource\" uri=\"local://tv\" autoload=\"true\"/>");
        sb.AppendLine("  <DataSource id=\"ZoetropeDataSource\" uri=\"local://zoetrope\" autoload=\"true\"/>");
        sb.AppendLine("  <DataSource id=\"ZoetropeDataSourceForActions\" uri=\"local://zoetrope\" autoload=\"true\"/>");
        sb.AppendLine("  <Scripts>");
        sb.AppendLine("    <Script src=\"../../Scripts/mrml/Play.crunch.js\"/>");
        sb.AppendLine("  </Scripts>");
        sb.AppendLine("    <Extensions>");
        sb.AppendLine("      <Extension name=\"fullscreenTVControls\" type=\"9\" urn=\"urn:microsoft:mediaroom:extension:fullscreentvcontrols:1\">");
        sb.AppendLine("        <Param name=\"video\" value=\"backgroundVideoPlayer\"/>");
        sb.AppendLine("        <Param name=\"browsebar\" value=\"on\"/>");
        sb.AppendLine("        <Param name=\"channelbar\" value=\"off\"/>");
        sb.AppendLine("        <Param name=\"channelentry\" value=\"on\"/>");
        sb.AppendLine("        <Param name=\"optionspanel\" value=\"on\"/>");
        sb.AppendLine("        <Param name=\"recentpanel\" value=\"on\"/>");
        sb.AppendLine("        <Param name=\"rosette\" value=\"on\"/>");
        sb.AppendLine("        <Param name=\"seekbar\" value=\"on\"/>");
        sb.AppendLine("      </Extension>");
        sb.AppendLine("    </Extensions>");
        sb.AppendLine(@"    <Header />");

        string frontPanelText = "";

        if (!string.IsNullOrEmpty(artistName) && !string.IsNullOrEmpty(songTitle))
            frontPanelText = artistName + " - " + songTitle;
        else if (!string.IsNullOrEmpty(songTitle))
            frontPanelText = songTitle;
        else if (!string.IsNullOrEmpty(videoName))
            frontPanelText = videoName;
        else
            frontPanelText = "LukaTube";

        frontPanelText = Regex.Replace(frontPanelText, @"\s+", " ").Trim();

        sb.AppendLine(@"    <FrontPanel id=""fp1""
    visible=""true""
    LoadAsync=""false""
    DefaultMessage=""" + EscapeXml(frontPanelText) + @""">");
        sb.AppendLine("      " + EscapeXml(frontPanelText));
        sb.AppendLine(@"    </FrontPanel>");

        string targetPage = "http://172.16.40.101/SETTEMediaroomApp/LukaTube.aspx";
        string targetPageWithQuery = BuildLukaTubeUrl(targetPage, Request.Url.Query, newVideosMode, sortMode, globalOnlineServer);

        string sendToUrl = null;
        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(sourceVideoUrl))
        {
            string sendToVideoUrl = Regex.Replace(playbackVideoUrl, @"^https?://172\.16\.40\.100", "https://lukaserver.ddns.net");
            sendToUrl = "http://172.16.40.101/SETTEMediaroomApp/SendTo.aspx?userid=" + HttpUtility.UrlEncode(userId)
                + "&message=" + HttpUtility.UrlEncode(sendToVideoUrl)
                + "&video_name=" + HttpUtility.UrlEncode(videoName)
                + GetDeviceQuerySuffix()
                + GetNewVideosQuerySuffix(newVideosMode)
                + GetSortQuerySuffix(sortMode)
                + GetGlobalServerQuerySuffix(globalOnlineServer);
        }

        sb.AppendLine("    <Actions>");

        sb.AppendLine("      <Action name=\"showRecordFail\" type=\"dialog\" data=\"Videata Od LukaTube nemozat da se snimaat\"/>");
        sb.AppendLine("      <Action name=\"showMediaError\" type=\"dialog\" data=\"LukaTube Videoto ne moze da se prikaze. Ve molime obidi se povtorno podocna.\"/>");

        sb.AppendLine("      <Action name=\"NextVideoInfo\" type=\"dialog\" data=\"" + EscapeXml(videoName) + "\"/>");

        if (!string.IsNullOrEmpty(nextUrl))
        {
            sb.AppendLine("      <Action name=\"NextVideo\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"page:" + EscapeXml(nextUrl) + "\" method=\"GET\"/>");
            sb.AppendLine("      <Event type=\"onkey:channelup\" action=\"NextVideo\"/>");
            sb.AppendLine("      <Event type=\"onkey:up\" action=\"NextVideo\"/>");
            sb.AppendLine("      <Event type=\"onkey:info\" action=\"NextVideoInfo\"/>");
            sb.AppendLine("      <Action name=\"finishedAction\" type=\"navigate\" data=\"back\"/>");
            sb.AppendLine("      <Event type=\"onmediaend\" action=\"NextVideo\"/>");
             sb.AppendLine("      <Event type=\"onmediaerror\" action=\"showMediaError NextVideo\"/>");
            sb.AppendLine(@"      <Action name=""NextVideo1"" type=""tune"" data=""" + EscapeXml(nextUrl) + @""" />");
        }

        if (!string.IsNullOrEmpty(prevUrl))
        {
            sb.AppendLine("      <Action name=\"PreviousVideo\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"page:" + EscapeXml(prevUrl) + "\" method=\"GET\"/>");
            sb.AppendLine("      <Event type=\"onkey:channeldown\" action=\"PreviousVideo\"/>");
            sb.AppendLine("      <Event type=\"onkey:down\" action=\"PreviousVideo\"/>");
        }

        sb.AppendLine("      <Action name=\"OpenLukaTube\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"page:" + EscapeXml(targetPageWithQuery) + "\" method=\"GET\"/>");
        
        sb.AppendLine("      <Event type=\"onkey:right\" action=\"OpenLukaTube\"/>");
        sb.AppendLine("      <Event type=\"onkey:vod\" action=\"OpenLukaTube\"/>");

        sb.AppendLine(@"      <Event type=""onkey:record"" action=""showRecordFail"" />");
        sb.AppendLine(@"      <Action name=""rc1"" type=""record"" data=""video=" + EscapeXml(playbackVideoUrl) + @""" />");

        if (!string.IsNullOrEmpty(sendToUrl))
        {
            sb.AppendLine("      <Action name=\"SendToAction\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"page:" + EscapeXml(sendToUrl) + "\" method=\"GET\"/>");
            sb.AppendLine("      <Event type=\"onkey:left\" action=\"SendToAction\"/>");
            sb.AppendLine("      <Event type=\"onkey:green\" action=\"SendToAction\"/>");
        }

        string lukaReturnUrl = BuildLukaTubeUrl(targetPage, "", newVideosMode, sortMode, globalOnlineServer);
        lukaReturnUrl = AppendQuery(lukaReturnUrl, "offset", lukaOffset.ToString());
        lukaReturnUrl = AppendQuery(lukaReturnUrl, "pageSize", pageSize.ToString());

        if (!string.IsNullOrEmpty(deviceGuid))
            lukaReturnUrl = AppendQuery(lukaReturnUrl, "DeviceGuid", deviceGuid);

        if (localFolder)
            lukaReturnUrl = AppendQuery(lukaReturnUrl, "LocalFolder", "true");

        if (globalOnlineServer)
            lukaReturnUrl = AppendQuery(lukaReturnUrl, "GlobalOnlineServer", "true");

        sb.AppendLine("      <Action name=\"BackToList\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"page:" + EscapeXml(lukaReturnUrl) + "\" method=\"GET\"/>");
        sb.AppendLine("      <Action name=\"OnEnterPage\" type=\"script\" script=\"function scriptTesting\"/>");
        sb.AppendLine("      <Event type=\"onkey:right\" action=\"BackToList\"/>");
        sb.AppendLine("  <Event type=\"onenter\" action=\"OnEnterPage\"/>");

    string resolvedUserId = GetUserIdFromDeviceGuid(deviceGuid);

string openArtistSearch = "page:http://172.16.40.101/SETTEMediaroomApp/MenuOrSearchArtist.aspx?";
string openMultipleArtistSearch = "page:http://172.16.40.101/SETTEMediaroomApp/SearchMultipleArtists.aspx?";
string openChannelsSearch = "page:http://172.16.40.101/SETTEMediaroomApp/SearchChannels.aspx?channel=";

if (!string.IsNullOrEmpty(resolvedUserId))
{
    openArtistSearch += "me_id=" + HttpUtility.UrlEncode(resolvedUserId);
    openArtistSearch += "&userid=" + HttpUtility.UrlEncode(resolvedUserId);

    openMultipleArtistSearch += "me_id=" + HttpUtility.UrlEncode(resolvedUserId);
    openMultipleArtistSearch += "&userid=" + HttpUtility.UrlEncode(resolvedUserId);

    openChannelsSearch += "&me_id=" + HttpUtility.UrlEncode(resolvedUserId);
    openChannelsSearch += "&userid=" + HttpUtility.UrlEncode(resolvedUserId);
}
else
{
    openChannelsSearch += "&me_id=";
    openChannelsSearch += "&userid=";
}

if (!string.IsNullOrEmpty(deviceGuid))
{
    openArtistSearch += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);
    openMultipleArtistSearch += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);
    openChannelsSearch += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);
}
else
{
    openChannelsSearch += "&DeviceGuid=";
}

if (!string.IsNullOrEmpty(searchSendTo))
{
    openArtistSearch += "&SearchSendTo=" + HttpUtility.UrlEncode(searchSendTo);
    openMultipleArtistSearch += "&SearchSendTo=" + HttpUtility.UrlEncode(searchSendTo);
}

if (!string.IsNullOrEmpty(videoChannel))
{
    openArtistSearch += "&video_channel=" + HttpUtility.UrlEncode(videoChannel);
    openMultipleArtistSearch += "&video_channel=" + HttpUtility.UrlEncode(videoChannel);
}

if (globalOnlineServer)
{
    openArtistSearch += "&GlobalOnlineServer=true";
    openMultipleArtistSearch += "&GlobalOnlineServer=true";
    openChannelsSearch += "&GlobalOnlineServer=true";
}
        sb.AppendLine("      <Event type=\"onkey:guide\" action=\"OpenLukaTubeChannels\"/>");
        sb.AppendLine("      <Action name=\"OpenLukaTubeChannels\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"" + EscapeXml(openChannelsSearch) + "\" method=\"GET\"/>");
        sb.AppendLine("      <Action name=\"OpenSearchArtists\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"" + EscapeXml(openArtistSearch) + "\" method=\"GET\"/>");
        sb.AppendLine("      <Action name=\"OpenSearchMultipleArtists\" type=\"submit\" data=\"lbltuneMainChannel\" url=\"" + EscapeXml(openMultipleArtistSearch) + "\" method=\"GET\"/>");
        sb.AppendLine("      <Event type=\"onkey:app5\" action=\"OpenSearchMultipleArtists\"/>");
        sb.AppendLine("      <Event type=\"onkey:blue\" action=\"OpenSearchMultipleArtists\"/>");
        sb.AppendLine("      <Event type=\"onkey:menu\" action=\"OpenSearchArtists\"/>");

        string ChannelTVHD = "page:file:///ChannelTVHD.xml";
        string DiagnosticsPage = "page:file:///Diagnostics.xml";
        sb.AppendLine("      <Action name=\"OpenHardDisk\" type=\"submit\" url=\"" + EscapeXml(ChannelTVHD) + "\" method=\"GET\"/>");
        sb.AppendLine("      <Action name=\"OpenDiagnostics\" type=\"submit\" url=\"" + EscapeXml(DiagnosticsPage) + "\" method=\"GET\"/>");

        if (openChannelTVHD)
        {
            sb.AppendLine("      <Event type=\"onenter\" action=\"OpenHardDisk\"/>");
        }
        else
        {
            sb.AppendLine("      <Event type=\"onkey:select\" action=\"OpenHardDisk\"/>");
        }

        sb.AppendLine("      <Event type=\"onkey:info\" action=\"OpenDiagnostics\"/>");
        sb.AppendLine("    <Action name=\"tuneToTimestampAction\" type=\"seek\" data=\"${@timestamp}\"/>");

        sb.AppendLine("    </Actions>");

        string playbackVideoUrlWithMpfevent = AppendMpfevent(playbackVideoUrl, "#urn:microsoft:mediaroom:event:media:state:seekbar");

        sb.AppendLine("      <Video SessionName=\"FULLSCREEN\" id=\"backgroundVideoPlayer\" width=\"1280\" height=\"720\" visible=\"true\" showcontrols=\"true\" showbusyindicator=\"true\" tuneurl=\"" + EscapeXml(playbackVideoUrlWithMpfevent) + "\">");
        sb.AppendLine("      </Video>");

        sb.AppendLine("      <Text id=\"VideoInfo\" alignment=\"left\" fontstyle=\"Reg18\" foreground=\"argb(255,255,255,255)\" margin=\"rect(20,20,0,0)\" width=\"1200\" height=\"140\">");
        sb.AppendLine("        " + EscapeXml("Device: " + deviceInfo));
        sb.AppendLine("        " + EscapeXml("Video: " + videoName));

        if (!string.IsNullOrEmpty(videoChannel))
        {
            sb.AppendLine("        " + EscapeXml("Channel: " + videoChannel));
        }

        sb.AppendLine("        " + EscapeXml("Views: " + viewCount));
        sb.AppendLine("        " + EscapeXml("Press UP/Down, Channel UP/Channel Down for Next/Previous, GUIDE for LukaTube, LEFT/Green to Send To, MENU/Blue/Music Button to search artist, Double Press SELECT for Video Controls"));
        sb.AppendLine("      </Text>");
        sb.AppendLine("  </MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private void SplitArtistAndSong(string title, out string artist, out string song)
    {
        artist = "";
        song = "";

        if (string.IsNullOrEmpty(title))
            return;

        string t = HttpUtility.HtmlDecode(title).Trim();
        t = t.Replace("_", " ");
        t = Regex.Replace(t, @"\s+", " ").Trim();
        t = Regex.Replace(t, @"\.(mp4|m4v|mov)$", "", RegexOptions.IgnoreCase).Trim();

        Match m = Regex.Match(t, @"^\s*(.+?)\s*[-–—]\s*(.+?)\s*$");
        if (m.Success)
        {
            artist = m.Groups[1].Value.Trim();
            song = m.Groups[2].Value.Trim();
            return;
        }

        song = t;
    }

    private bool IsTrue(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        string t = value.Trim().ToLowerInvariant();
        return (t == "1" || t == "true" || t == "yes");
    }

    private string GetRemoteBase(bool globalOnlineServer)
    {
        return globalOnlineServer
            ? "https://lukaserver.ddns.net/youtubeclone/videos_mediaroom/"
            : "http://172.16.40.100/youtubeclone/videos_mediaroom/";
    }

    private string ResolveVideoUrlForPlayback(string videoUrl, bool localFolder, string localBase)
    {
        if (string.IsNullOrEmpty(videoUrl))
            return videoUrl;

        if (localFolder)
            return videoUrl;

        try
        {
            string decoded = HttpUtility.UrlDecode(videoUrl, Encoding.UTF8);
            Uri uri;
            if (!Uri.TryCreate(decoded, UriKind.Absolute, out uri))
                return videoUrl;

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return videoUrl;
            }

            if (string.Equals(uri.Host, "172.16.40.100", StringComparison.OrdinalIgnoreCase))
                return videoUrl;

            string downloadedDirFs = Server.MapPath("~/youtubeclone/videos_mediaroom/downloaded/");
            Directory.CreateDirectory(downloadedDirFs);

            string fileName = GetSafeDownloadFileName(uri);
            string localFileFs = Path.Combine(downloadedDirFs, fileName);

            if (!File.Exists(localFileFs) || new FileInfo(localFileFs).Length == 0)
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add(HttpRequestHeader.UserAgent, "LukaTube/1.0");
                    wc.Headers.Add(HttpRequestHeader.Accept, "*/*");
                    wc.DownloadFile(uri, localFileFs);
                }
            }

            return localBase + "downloaded/" + HttpUtility.UrlEncode(fileName, Encoding.UTF8);
        }
        catch
        {
            return videoUrl;
        }
    }

    private string GetSafeDownloadFileName(Uri uri)
    {
        string name = "";

        try
        {
            name = Path.GetFileName(uri.LocalPath ?? "");
        }
        catch
        {
            name = "";
        }

        if (string.IsNullOrEmpty(name))
            name = "video.mp4";

        name = HttpUtility.UrlDecode(name, Encoding.UTF8);
        name = Regex.Replace(name, @"[^\w\-. ]+", "_");
        name = name.Replace(" ", "_");

        if (string.IsNullOrEmpty(Path.GetExtension(name)))
            name += ".mp4";

        string host = uri.Host ?? "remote";
        host = Regex.Replace(host, @"[^\w\-.]+", "_");

        return host + "_" + name;
    }

    private Dictionary<string, int> GetPlayedCountsFromStb(string stbClientId)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(stbClientId))
            return counts;

        try
        {
            string apiUrl = "http://172.16.40.100/getplayedfromstb.php?stbclientid=" + HttpUtility.UrlEncode(stbClientId);

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(apiUrl);
            req.Method = "GET";
            req.Timeout = 5000;
            req.UserAgent = "LukaTube/1.0";

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string json = sr.ReadToEnd();
                if (string.IsNullOrWhiteSpace(json))
                    return counts;

                JObject obj = JObject.Parse(json);
                JToken tok = obj["videoPlayCounts"];
                if (tok != null && tok.Type == JTokenType.Object)
                {
                    JObject countsObj = (JObject)tok;
                    foreach (JProperty prop in countsObj.Properties())
                    {
                        int val;
                        if (int.TryParse(prop.Value.ToString(), out val))
                        {
                            string key = prop.Name ?? "";
                            counts[key] = val;

                            string decodedKey = key;
                            try { decodedKey = HttpUtility.UrlDecode(key, Encoding.UTF8); } catch { }
                            if (!string.IsNullOrEmpty(decodedKey))
                                counts[decodedKey] = val;
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return counts;
    }

    private int GetPlayCountForVideo(string fullUrl, Dictionary<string, int> videoPlayCounts)
    {
        if (videoPlayCounts == null || videoPlayCounts.Count == 0)
            return 0;

        if (string.IsNullOrEmpty(fullUrl))
            return 0;

        int val;

        if (videoPlayCounts.TryGetValue(fullUrl, out val))
            return val;

        string decoded = fullUrl;
        try { decoded = HttpUtility.UrlDecode(fullUrl); } catch { }
        if (!string.IsNullOrEmpty(decoded) && videoPlayCounts.TryGetValue(decoded, out val))
            return val;

        string cleaned = decoded;
        int q = cleaned.IndexOf('?');
        if (q >= 0) cleaned = cleaned.Substring(0, q);
        int h = cleaned.IndexOf('#');
        if (h >= 0) cleaned = cleaned.Substring(0, h);

        if (!string.IsNullOrEmpty(cleaned) && videoPlayCounts.TryGetValue(cleaned, out val))
            return val;

        return 0;
    }

private List<string> GetArtistVideosFromApi(string artist)
{
    List<string> videos = new List<string>();

    if (string.IsNullOrEmpty(artist))
        return videos;

    try
    {
        string apiUrl =
            "http://172.16.40.100/youtubeclone/get_artists_from_videos.php?action=get_artist_videos&artist="
            + HttpUtility.UrlEncode(artist);

        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(apiUrl);
        req.Method = "GET";
        req.Timeout = 5000;
        req.UserAgent = "LukaTube/1.0";

        using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
        using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
        {
            string json = sr.ReadToEnd();

            JObject obj = JObject.Parse(json);

            JToken list = obj["videos"];

            if (list != null && list.Type == JTokenType.Array)
            {
                foreach (JToken v in list)
                {
                    string url = v.ToString();

                    if (!string.IsNullOrEmpty(url))
                        videos.Add(url);
                }
            }
        }
    }
    catch
    {
    }

    return videos;
}
    private string GetVideoChannelFromApi(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl))
            return "";

        try
        {
            string fileName = fileUrl;

            if (fileName.IndexOf("/") >= 0)
                fileName = fileName.Substring(fileName.LastIndexOf("/") + 1);

            if (fileName.IndexOf("\\") >= 0)
                fileName = fileName.Substring(fileName.LastIndexOf("\\") + 1);

            fileName = HttpUtility.UrlDecode(fileName, Encoding.UTF8);

            string apiUrl = "http://172.16.40.100/youtubeclone/get_artist_info.php?file=" + HttpUtility.UrlEncode(fileName);

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(apiUrl);
            req.Method = "GET";
            req.Timeout = 5000;
            req.UserAgent = "LukaTube/1.0";

            string respBody;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                respBody = sr.ReadToEnd();
            }

            JObject j = JObject.Parse(respBody);
            if (j["success"] != null && j["success"].ToString().ToLowerInvariant() == "true")
            {
                string artist = j["artist"] != null ? j["artist"].ToString() : "";
                artist = (artist ?? "").Trim();
                artist = artist.Replace("_", " ");
                artist = Regex.Replace(artist, @"\s+", " ").Trim();
                return artist;
            }
        }
        catch
        {
        }

        return "";
    }

    private string ExtractSearchSendTo(string videoTitle)
    {
        if (string.IsNullOrEmpty(videoTitle))
            return "";

        string title = HttpUtility.HtmlDecode(videoTitle).Trim();
        title = title.Replace("_", " ");
        title = Regex.Replace(title, @"\s+", " ").Trim();

        Match m = Regex.Match(title, @"^\s*([^\-–—]+?)\s*[-–—]\s*.+$");
        if (m.Success)
        {
            string artist = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(artist))
                return artist;
        }

        return title;
    }

    private string GetDeviceQuerySuffix()
    {
        string d = Request.QueryString["DeviceGuid"];
        if (d == null) d = "";
        string lfs = Request.QueryString["LocalFolder"];
        if (lfs == null) lfs = "";
        string suffix = "";
        if (!string.IsNullOrEmpty(d)) suffix += "&DeviceGuid=" + HttpUtility.UrlEncode(d);
        if (!string.IsNullOrEmpty(lfs)) suffix += "&LocalFolder=" + HttpUtility.UrlEncode(lfs);
        return suffix;
    }

    private string GetGlobalServerQuerySuffix(bool globalOnlineServer)
    {
        return globalOnlineServer ? "&GlobalOnlineServer=true" : "";
    }

    private string GetNewVideosQuerySuffix(bool newVideosMode)
    {
        return newVideosMode ? "&newVideos=true" : "";
    }

    private string GetSortQuerySuffix(string sortMode)
    {
        if (string.IsNullOrEmpty(sortMode))
            return "";

        if (sortMode == "new")
            return "&sort=new";

        if (sortMode == "playedby")
            return "&sort=playedby";

        return "";
    }

    private string BuildLukaTubeUrl(string baseUrl, string rawQuery, bool newVideosMode, string sortMode, bool globalOnlineServer)
    {
        string url = baseUrl;

        if (!string.IsNullOrEmpty(rawQuery))
        {
            string q = rawQuery.TrimStart('?');

            if (!string.IsNullOrEmpty(q))
            {
                string[] parts = q.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                List<string> kept = new List<string>();

                for (int i = 0; i < parts.Length; i++)
                {
                    string p = parts[i].Trim();
                    if (p.Length == 0) continue;

                    if (p.StartsWith("sort=", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (p.StartsWith("GlobalOnlineServer=", StringComparison.OrdinalIgnoreCase))
                        continue;

                    kept.Add(p);
                }

                if (kept.Count > 0)
                    url += "?" + string.Join("&", kept.ToArray());
            }
        }

        if (newVideosMode)
        {
            if (url.Contains("?"))
                url += "&sort=new";
            else
                url += "?sort=new";
        }
        else if (sortMode == "playedby")
        {
            if (url.Contains("?"))
                url += "&sort=playedby";
            else
                url += "?sort=playedby";
        }

        if (globalOnlineServer)
        {
            if (url.Contains("?"))
                url += "&GlobalOnlineServer=true";
            else
                url += "?GlobalOnlineServer=true";
        }

        return url;
    }

    private string AppendQuery(string url, string key, string value)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        string separator = url.Contains("?") ? "&" : "?";
        return url + separator + HttpUtility.UrlEncode(key) + "=" + HttpUtility.UrlEncode(value);
    }

    private string AppendMpfevent(string url, string mpfeventValue)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        string separator = url.Contains("?") ? "&" : "?";
        return url + separator + "__mpfevent=" + mpfeventValue;
    }

    private string EscapeXml(string s)
    {
        return s == null ? "" : SecurityElement.Escape(s);
    }

    private string GetUserIdFromDeviceGuid(string deviceGuid)
    {
        if (string.IsNullOrEmpty(deviceGuid)) return "";
        try
        {
            string url = "http://172.16.40.100/get_lukify_clientidforuserid.php?deviceguid=" + HttpUtility.UrlEncode(deviceGuid);
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 3000;

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
            {
                string json = sr.ReadToEnd();
                JObject obj = JObject.Parse(json);
                if (obj["status"] != null && obj["status"].ToString() == "success" && obj["userid"] != null)
                {
                    return obj["userid"].ToString();
                }
            }
        }
        catch
        {
        }
        return "";
    }
}