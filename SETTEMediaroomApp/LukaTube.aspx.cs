using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Linq;
using Newtonsoft.Json.Linq;

public partial class LukaTube : Page
{
    private Dictionary<string, Dictionary<string, string>> Translations = new Dictionary<string, Dictionary<string, string>>()
    {
        { "en", new Dictionary<string,string> {
            { "SearchHint", "Search videos..." },
            { "SearchButton", "Search LukaTube" },
            { "ResumeButton", "Resume Video" },
            { "LoadMore", "Load more videos..." },
            { "TitleDefault", "LukaTube - Videos" },
            { "ConnectSTB", "Connect STB to Lukify" },
            { "NewVideosButton", "New Videos" },
            { "DefaultVideosButton", "Videos" },
            { "PlayedByButton", "Played By" },
            { "MostPlayedTitle", "Most Played Videos" },
            { "GlobalServerButton", "Global Online Server" },
            { "LocalServerButton", "Local Server" }
        }},
        { "mk", new Dictionary<string,string> {
            { "SearchHint", "Prebaraj videa..." },
            { "SearchButton", "Prebaraj LukaTube" },
            { "ResumeButton", "Prodolzi video" },
            { "LoadMore", "Vchitaj poveke videa..." },
            { "TitleDefault", "LukaTube - Videa" },
            { "ConnectSTB", "Povrzi STB so Lukify" },
            { "NewVideosButton", "Novi videa" },
            { "DefaultVideosButton", "Videa" },
            { "PlayedByButton", "Pushteni Videa" },
            { "MostPlayedTitle", "Najpustani videa" },
            { "GlobalServerButton", "Global Online Server" },
            { "LocalServerButton", "Lokal Server" }
        }}
    };

    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        // Language
        string lang = "en";
        if (Request.UserLanguages != null && Request.UserLanguages.Length > 0 &&
            Request.UserLanguages[0].ToLower().StartsWith("mk"))
        {
            lang = "mk";
        }
        Dictionary<string, string> T = Translations[lang];

        // Query params
        string deviceGuid = (Request.QueryString["DeviceGuid"] ?? "").Trim();
        string rawSearch = (Request.QueryString["SearchLukaTube"] ?? "").Trim();
        string sortMode = (Request.QueryString["sort"] ?? "").Trim().ToLowerInvariant();
        bool sortNew = (sortMode == "new");
        bool sortPlayedBy = (sortMode == "playedby");

        bool globalOnlineServer = false;
        if (!string.IsNullOrEmpty(Request.QueryString["GlobalOnlineServer"]))
        {
            string gos = Request.QueryString["GlobalOnlineServer"].Trim().ToLowerInvariant();
            if (gos == "true" || gos == "1" || gos == "yes")
                globalOnlineServer = true;
        }

        bool hideVideoInfo = false;
        if (!string.IsNullOrEmpty(Request.QueryString["HideVideoInfo"]))
        {
            string hvi = Request.QueryString["HideVideoInfo"].Trim().ToLowerInvariant();
            if (hvi == "true" || hvi == "1" || hvi == "yes")
                hideVideoInfo = true;
        }

        int pageSize = 12;
        int tmp;
        if (int.TryParse(Request.QueryString["pageSize"], out tmp) && tmp > 0)
            pageSize = tmp;

        int offset = 0;
        if (int.TryParse(Request.QueryString["offset"], out tmp) && tmp >= 0)
            offset = tmp;

        bool localFolder = false;
        if (!string.IsNullOrEmpty(Request.QueryString["LocalFolder"]))
        {
            string lf = Request.QueryString["LocalFolder"].Trim().ToLowerInvariant();
            if (lf == "true" || lf == "1" || lf == "yes")
                localFolder = true;
        }

        bool remoteFailed = false;

        // Base URLs
        string basePageUrl = Request.Url.GetLeftPart(UriPartial.Authority) + "/SETTEMediaroomApp/LukaTube.aspx";
        string connectUrl = "http://172.16.40.101/SETTEMediaroomApp/ConnectSTBToLukify.aspx";
        if (!string.IsNullOrEmpty(deviceGuid))
            connectUrl += "?DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

        if (globalOnlineServer)
            connectUrl += (connectUrl.Contains("?") ? "&" : "?") + "GlobalOnlineServer=true";

        if (hideVideoInfo)
            connectUrl += (connectUrl.Contains("?") ? "&" : "?") + "HideVideoInfo=true";

        string localRemoteBase = "http://172.16.40.100/youtubeclone/videos_mediaroom/";
        string globalRemoteBase = "http://lukaserver.ddns.net/youtubeclone/videos_mediaroom/";
        string remoteBase = globalOnlineServer ? globalRemoteBase : localRemoteBase;

        string remoteListingUrl = sortNew ? (remoteBase + "?C=M;O=D") : remoteBase;

        string loadMorePageUrl = BuildPageUrl(basePageUrl, deviceGuid, rawSearch, sortNew, sortPlayedBy, offset + pageSize, pageSize, localFolder, globalOnlineServer, hideVideoInfo);
        string newVideosPageUrl = BuildPageUrl(basePageUrl, deviceGuid, rawSearch, true, false, 0, pageSize, localFolder, globalOnlineServer, hideVideoInfo);
        string defaultVideosPageUrl = BuildPageUrl(basePageUrl, deviceGuid, rawSearch, false, false, 0, pageSize, localFolder, globalOnlineServer, hideVideoInfo);
        string playedByPageUrl = BuildPageUrl(basePageUrl, deviceGuid, rawSearch, false, true, 0, pageSize, localFolder, globalOnlineServer, hideVideoInfo);

        // FIX 1:
        // Do NOT include rawSearch in the submit target URL, otherwise some MRML runtimes
        // append the typed text to the existing query value.
        string searchActionUrl = BuildPageUrl(basePageUrl, deviceGuid, "", sortNew, sortPlayedBy, 0, pageSize, localFolder, globalOnlineServer, hideVideoInfo);

        // User info
        string username = "";
        string fullName = "";
        string profilePictureUrl = "";

        if (!string.IsNullOrEmpty(deviceGuid))
        {
            try
            {
                string apiUrl = "http://172.16.40.100/get_lukify_clientidforuserid.php?deviceguid="
                                + HttpUtility.UrlEncode(deviceGuid);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
                request.Method = "GET";
                request.Timeout = 3000;
                request.UserAgent = "LukaTube/1.0";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader stream = new StreamReader(response.GetResponseStream()))
                {
                    string json = stream.ReadToEnd();
                    if (!string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            JObject jobj = JObject.Parse(json);
                            if (jobj["status"] != null && jobj["status"].ToString().ToLower() == "success")
                            {
                                if (jobj["username"] != null) username = jobj["username"].ToString();
                                if (jobj["full_name"] != null) fullName = jobj["full_name"].ToString();
                                if (jobj["profile_picture_url"] != null) profilePictureUrl = jobj["profile_picture_url"].ToString();
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        // Play counts
        Dictionary<string, int> videoPlayCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (sortPlayedBy && !string.IsNullOrEmpty(deviceGuid))
        {
            videoPlayCounts = GetOrLoadVideoPlayCounts(deviceGuid);
        }

        // Fetch videos
        string videoDir = Server.MapPath("~/youtubeclone/videos_mediaroom/");
        List<string> files = new List<string>();

        if (!localFolder)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(remoteListingUrl);
                req.Timeout = 5000;
                req.UserAgent = "LukaTube/1.0";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                {
                    string html = sr.ReadToEnd();

                    MatchCollection matches = Regex.Matches(
                        html,
                        @"href\s*=\s*[""']([^""']+)[""']",
                        RegexOptions.IgnoreCase
                    );

                    HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    Uri baseUri = new Uri(remoteBase);

                    foreach (Match m in matches)
                    {
                        string href = (m.Groups[1].Value ?? "").Trim();
                        if (string.IsNullOrEmpty(href))
                            continue;

                        if (href == "../" || href == ".." || href.StartsWith("?") || href.StartsWith("#"))
                            continue;

                        string cleanHref = href;
                        int q = cleanHref.IndexOf('?');
                        if (q >= 0)
                            cleanHref = cleanHref.Substring(0, q);

                        int hash = cleanHref.IndexOf('#');
                        if (hash >= 0)
                            cleanHref = cleanHref.Substring(0, hash);

                        string lower = cleanHref.ToLowerInvariant();
                        if (!(lower.EndsWith(".mp4") || lower.EndsWith(".m4v") || lower.EndsWith(".mov")))
                            continue;

                        Uri absUri;
                        if (!Uri.TryCreate(cleanHref, UriKind.Absolute, out absUri))
                        {
                            absUri = new Uri(baseUri, cleanHref);
                        }

                        string abs = absUri.ToString();
                        if (seen.Add(abs))
                            files.Add(abs);
                    }
                }
            }
            catch
            {
                remoteFailed = true;
            }
        }

        if (localFolder || remoteFailed)
        {
            if (Directory.Exists(videoDir))
            {
                foreach (string f in Directory.GetFiles(videoDir))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".mp4" || ext == ".m4v" || ext == ".mov")
                    {
                        string videoUrl =
                            Request.Url.GetLeftPart(UriPartial.Authority)
                            + "/youtubeclone/videos_mediaroom/"
                            + HttpUtility.UrlEncode(Path.GetFileName(f));
                        files.Add(videoUrl);
                    }
                }
            }
        }

        // Search
        string searchLower = rawSearch.ToLowerInvariant();
        if (!string.IsNullOrEmpty(searchLower))
        {
            files = files.Where(f =>
            {
                string name = Path.GetFileName(f);
                try { name = HttpUtility.UrlDecode(name); } catch { }
                name = Path.GetFileNameWithoutExtension(name).Replace("_", " ").ToLowerInvariant();
                return name.Contains(searchLower);
            }).ToList();
        }

        // Sort by plays
        if (sortPlayedBy)
        {
            files = files
                .OrderByDescending(f => GetPlayCountForVideo(f, videoPlayCounts))
                .ThenBy(f =>
                {
                    string name = Path.GetFileName(f);
                    try { name = HttpUtility.UrlDecode(name); } catch { }
                    name = Path.GetFileNameWithoutExtension(name).Replace("_", " ").ToLowerInvariant();
                    return name;
                })
                .ToList();
        }

        // Pagination
        int total = files.Count;
        List<string> pageFiles = files.Skip(offset).Take(pageSize).ToList();
        string resumeUrl = null;

        if (pageFiles.Count > 0)
        {
            string firstVideo = pageFiles[0];

            string videoName = Path.GetFileName(firstVideo);
            try { videoName = HttpUtility.UrlDecode(videoName); } catch { }
            videoName = Regex.Replace(videoName, @"\.(mp4|m4v|mov)$", "", RegexOptions.IgnoreCase);

            resumeUrl = Request.Url.Scheme + "://" + Request.Url.Authority
                + "/SETTEMediaroomApp/PlayVideo.aspx"
                + "?video_url=" + HttpUtility.UrlEncode(firstVideo)
                + "&video_name=" + HttpUtility.UrlEncode(videoName)
                + "&newVideos=" + (sortNew ? "true" : "false");

            if (!string.IsNullOrEmpty(deviceGuid))
                resumeUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

            if (localFolder || remoteFailed)
                resumeUrl += "&LocalFolder=true";
            else
                resumeUrl += "&LocalFolder=false";

            if (globalOnlineServer)
                resumeUrl += "&GlobalOnlineServer=true";

            if (hideVideoInfo)
                resumeUrl += "&HideVideoInfo=true";
        }

        // Build MRML
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"  <MrmlPage AllowScreenSaver=""true"" id=""LukaTubeList"" appid=""lukatube.app/1.0"" width=""1280"" height=""720"">");

        sb.AppendLine(@"    <Actions>");
        sb.Append(@"      <Action name=""SearchLukaTube"" type=""submit"" data=""SearchLukaTube"" url=""page:");
        sb.Append(EscapeXml(searchActionUrl));
        sb.AppendLine(@""" method=""GET"" />");
        sb.AppendLine(@"    </Actions>");

        sb.AppendLine(@"    <Header />");
        sb.AppendLine(@"    <Panel id=""MainPanel"" left=""0"" top=""0"" width=""1280"" height=""720"">");

        string titleText = T["TitleDefault"];

        if (globalOnlineServer)
            titleText += " - From Global lukaserver.ddns.net";

        if (hideVideoInfo)
            titleText += " - No Video Info";

        if (sortNew)
            titleText = titleText + " - Newest";
        else if (sortPlayedBy)
            titleText = T["MostPlayedTitle"];

        if (total > 0)
        {
            int end = Math.Min(offset + pageSize, total);
            titleText = titleText + " (showing " + (offset + 1) + "-" + end + " of " + total + ")";
        }

        sb.AppendLine(@"      <Text id=""Title"" top=""10"" left=""20"" width=""900"" height=""30"" fontstyle=""Reg26"" foreground=""argb(255,228,0,115)"">" + EscapeXml(titleText) + "</Text>");
        sb.AppendLine(@"      <Text id=""Time"" top=""10"" left=""0"" width=""1280"" height=""30"" fontstyle=""Reg20"" justification=""right"" foreground=""argb(255,200,200,200)"">{Time}</Text>");
        sb.AppendLine(@"      <Image id=""EriccsonLogo"" top=""680"" left=""1230"" width=""50"" height=""30"" url=""file:///watermark.png"" />");

        // FIX 2:
        // Keep the current search text visible, but submit to a clean URL so it does not append.
        sb.AppendLine(@"      <EditText id=""SearchLukaTube"" top=""50"" left=""20"" width=""400"" height=""40"" visible=""true"" hint=""" + EscapeXml(T["SearchHint"]) + @""">" + EscapeXml(rawSearch) + "</EditText>");
        sb.AppendLine(@"      <Button id=""SearchButton"" top=""50"" left=""430"" fontstyle=""Reg20"" justification=""center"">");
        sb.AppendLine(@"        <Text>" + EscapeXml(T["SearchButton"]) + @"</Text>");
        sb.AppendLine(@"        <Actions><Event type=""onclick"" action=""SearchLukaTube""/></Actions>");
        sb.AppendLine(@"      </Button>");

        string toggleButtonText = sortNew ? T["DefaultVideosButton"] : T["NewVideosButton"];
        string toggleButtonUrl = sortNew ? defaultVideosPageUrl : newVideosPageUrl;

        sb.AppendLine(@"      <Button id=""NewVideosButton"" top=""50"" left=""580"" width=""160"" height=""40"" justification=""center"" href=""page:" + EscapeXml(toggleButtonUrl) + @""" focusScale=""1.05"">");
        sb.AppendLine(@"        <Text>" + EscapeXml(toggleButtonText) + @"</Text>");
        sb.AppendLine(@"      </Button>");

        string playedByButtonText = sortPlayedBy ? T["DefaultVideosButton"] : T["PlayedByButton"];
        string playedByButtonUrl = sortPlayedBy ? defaultVideosPageUrl : playedByPageUrl;

        sb.AppendLine(@"      <Button id=""PlayedByButton"" top=""50"" left=""750"" width=""160"" height=""40"" justification=""center"" href=""page:" + EscapeXml(playedByButtonUrl) + @""" focusScale=""1.05"">");
        sb.AppendLine(@"        <Text>" + EscapeXml(playedByButtonText) + @"</Text>");
        sb.AppendLine(@"      </Button>");

        string globalServerText = globalOnlineServer ? T["LocalServerButton"] : T["GlobalServerButton"];
        string globalServerUrl = BuildPageUrl(basePageUrl, deviceGuid, rawSearch, sortNew, sortPlayedBy, offset, pageSize, localFolder, !globalOnlineServer, hideVideoInfo);

        sb.AppendLine(@"      <Button id=""GlobalOnlineServerButton"" top=""95"" left=""20"" width=""260"" height=""40"" justification=""center"" href=""page:" + EscapeXml(globalServerUrl) + @""" focusScale=""1.05"">");
        sb.AppendLine(@"        <Text>" + EscapeXml(globalServerText) + @"</Text>");
        sb.AppendLine(@"      </Button>");

        string videoInfoToggleText = hideVideoInfo ? "Show video info" : "Dont show video info";
        string videoInfoToggleUrl = BuildPageUrl(basePageUrl, deviceGuid, rawSearch, sortNew, sortPlayedBy, offset, pageSize, localFolder, globalOnlineServer, !hideVideoInfo);

        sb.AppendLine(@"      <Button id=""VideoInfoToggleButton"" top=""95"" left=""980"" width=""260"" height=""40"" justification=""center"" href=""page:" + EscapeXml(videoInfoToggleUrl) + @""" focusScale=""1.05"">");
        sb.AppendLine(@"        <Text>" + EscapeXml(videoInfoToggleText) + @"</Text>");
        sb.AppendLine(@"      </Button>");

        if (!string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(fullName))
        {
            string displayText;
            if (!string.IsNullOrEmpty(fullName))
                displayText = string.Format("Connected to {0} ({1})", fullName, username);
            else
                displayText = "Connected to " + username;

            string showConnectedUrl = "http://172.16.40.101/SETTEMediaroomApp/ShowTVConnected.aspx";
            if (!string.IsNullOrEmpty(deviceGuid))
                showConnectedUrl += "?DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

            if (globalOnlineServer)
                showConnectedUrl += (showConnectedUrl.Contains("?") ? "&" : "?") + "GlobalOnlineServer=true";

            if (hideVideoInfo)
                showConnectedUrl += (showConnectedUrl.Contains("?") ? "&" : "?") + "HideVideoInfo=true";

            sb.AppendLine(@"      <Button id=""UserInfoButton"" top=""50"" left=""930"" width=""310"" height=""40"" justification=""center"" href=""page:" + EscapeXml(showConnectedUrl) + @""" focusScale=""1.05"">");

            if (!string.IsNullOrEmpty(profilePictureUrl))
            {
                sb.AppendLine(@"        <Image id=""ProfilePic"" top=""4"" left=""4"" width=""32"" height=""32"" url=""" + EscapeXml(profilePictureUrl) + @""" />");
                sb.AppendLine(@"        <Text top=""8"" left=""44"" width=""260"" height=""24"" fontstyle=""Reg16"" alignment=""right"">" + EscapeXml(displayText) + @"</Text>");
            }
            else
            {
                sb.AppendLine(@"        <Text>" + EscapeXml(displayText) + @"</Text>");
            }

            sb.AppendLine(@"      </Button>");
        }
        else
        {
            sb.AppendLine(@"      <Button id=""ConnectSTBButton"" top=""50"" left=""930"" width=""300"" height=""40"" justification=""center"" href=""page:" + EscapeXml(connectUrl) + @""">");
            sb.AppendLine(@"        <Text>" + EscapeXml(T["ConnectSTB"]) + @"</Text>");
            sb.AppendLine(@"      </Button>");
        }

        if (!string.IsNullOrEmpty(resumeUrl))
        {
            string resumeWithGuid = resumeUrl;
            if (!string.IsNullOrEmpty(deviceGuid))
                resumeWithGuid += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);
            if (localFolder || remoteFailed)
                resumeWithGuid += "&LocalFolder=true";
            else
                resumeWithGuid += "&LocalFolder=false";
            if (globalOnlineServer)
                resumeWithGuid += "&GlobalOnlineServer=true";
            if (hideVideoInfo)
                resumeWithGuid += "&HideVideoInfo=true";

            sb.AppendLine(@"      <Button id=""ResumeButton"" top=""550"" left=""20"" width=""200"" height=""40"" clipChildren=""true"" justification=""right"" href=""page:" + EscapeXml(resumeWithGuid) + @""">");
            sb.AppendLine(@"        <Text>" + EscapeXml(T["ResumeButton"]) + @"</Text>");
            sb.AppendLine(@"      </Button>");
        }

        // News ticker
        sb.AppendLine(@"      <Ticker id=""NewsTicker""
              visible=""true""
              flowDirection=""righttoleft""
              duration=""20""
              repeatBehavior=""forever""
              itemsPadding=""20""
              snap=""center"">");

        sb.AppendLine(@"        <Text id=""Item1"">Dobredojdovte vo LukaTube TV.</Text>");
        if (!string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(fullName))
        {
            sb.AppendLine(@"        <Text id=""Item1"">Povrzan si na Lukify Music. Stisni Pushteni Videa za da gi vidis pustenite videa.</Text>");
        }
        else
        {
            sb.AppendLine(@"        <Text id=""Item1"">Povrzi STB so Lukify Music za da gi vidis pustenite videa.</Text>");
        }
        sb.AppendLine(@"        <Text id=""Item4"">Prebaraj videa so Search.</Text>");
        sb.AppendLine(@"        <Text id=""Item5"">Izberi Novi Videa za da gi vidis najnovite videa.</Text>");
        sb.AppendLine(@"        <Text id=""Item6"">Uzivaj vo LukaTube TV.</Text>");

        sb.AppendLine(@"      </Ticker>");

        sb.AppendLine(BuildNetflixGridFiles(pageFiles, offset, Request, deviceGuid, localFolder || remoteFailed, sortNew, sortPlayedBy, globalOnlineServer, hideVideoInfo, videoPlayCounts));

        if (offset + pageSize < total)
        {
            sb.AppendLine(@"      <Panel id=""loadmorePanel"" width=""1200"" height=""80"" top=""690"" left=""40"">");
            sb.AppendLine(@"        <Button id=""loadMoreBtn"" top=""10"" left=""0"" width=""600"" height=""40"" fontstyle=""Reg26"" href=""page:" + EscapeXml(loadMorePageUrl) + @""">");
            sb.AppendLine(@"          <Text top=""0"" left=""8"" width=""584"" height=""40"">" + EscapeXml(T["LoadMore"]) + @"</Text>");
            sb.AppendLine(@"        </Button>");
            sb.AppendLine(@"      </Panel>");
        }

        sb.AppendLine(@"    <Actions>");
        sb.AppendLine(@"      <Event type=""onkey:exit"" action=""showExitDialog"" />");
        sb.AppendLine(@"      <Event type=""onkey:record"" action=""rc1"" />");
        sb.AppendLine(@"      <Action name=""rc1"" type=""record"" data=""channel=1""/>");
        sb.AppendLine(@"      <Action name=""showExitDialog"" type=""dialog"" data=""Dali Sakate da izlezite od Aplikacijata LukaTube?"" button1=""Da"" onclick1=""action:exitApp"" button2=""Ne"" onclick2=""cancelExit""/>");
        sb.AppendLine(@"      <Action name=""exitApp"" type=""submit"" url=""ExitTune.aspx"" method=""GET""/>");
        sb.AppendLine(@"      <Action name=""cancelExit"" type=""none"" />");
        sb.AppendLine(@"      <Action name=""OpenProfile"" type=""submit"" data=""lbltuneMainChannel"" url=""lbltuneMainChannel"" method=""GET""/>");
        sb.AppendLine(@"      <Event type=""onkey:green"" action=""OpenProfile""/>");
        sb.AppendLine(@"    </Actions>");

        sb.AppendLine(@"    <Scripts>");
        sb.AppendLine(@"      <Script>");
        sb.AppendLine(@"      <![CDATA[");
        sb.AppendLine(@"        var doExit = false;");
        sb.AppendLine(@"        function js_resetExitFlag() {");
        sb.AppendLine(@"          doExit = false;");
        sb.AppendLine(@"        }");
        sb.AppendLine(@"        function js_forceExitNow() {");
        sb.AppendLine(@"          doExit = true;");
        sb.AppendLine(@"        }");
        sb.AppendLine(@"        function js_handleAppLeave() {");
        sb.AppendLine(@"          invokeAction(""LukaTubeList"", ""showDialog"");");
        sb.AppendLine(@"          invokeAction(""LukaTubeList"", ""FullScreenEnter"");");
        sb.AppendLine(@"        }");
        sb.AppendLine(@"      ]]>"); 
        sb.AppendLine(@"      </Script>");
        sb.AppendLine(@"    </Scripts>");

        sb.AppendLine(@"    </Panel>");
        sb.AppendLine(@"  </MrmlPage>");
        sb.AppendLine(@"</uidescription>");

        Response.Write(sb.ToString());
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private string BuildPageUrl(string basePageUrl, string deviceGuid, string rawSearch, bool sortNew, bool sortPlayedBy, int offset, int pageSize, bool localFolder, bool globalOnlineServer, bool hideVideoInfo)
    {
        List<string> parts = new List<string>();

        if (!string.IsNullOrEmpty(deviceGuid))
            parts.Add("DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid));

        if (!string.IsNullOrEmpty(rawSearch))
            parts.Add("SearchLukaTube=" + HttpUtility.UrlEncode(rawSearch));

        if (sortNew)
            parts.Add("sort=new");
        else if (sortPlayedBy)
            parts.Add("sort=playedby");

        if (offset > 0)
            parts.Add("offset=" + offset.ToString());

        if (pageSize > 0)
            parts.Add("pageSize=" + pageSize.ToString());

        if (localFolder)
            parts.Add("LocalFolder=true");

        if (globalOnlineServer)
            parts.Add("GlobalOnlineServer=true");

        if (hideVideoInfo)
            parts.Add("HideVideoInfo=true");

        if (parts.Count == 0)
            return basePageUrl;

        return basePageUrl + "?" + string.Join("&", parts.ToArray());
    }

    private string EscapeXml(string s)
    {
        if (s == null) return "";
        return System.Security.SecurityElement.Escape(s);
    }

    private Dictionary<string, int> GetOrLoadVideoPlayCounts(string deviceGuid)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(deviceGuid))
            return counts;

        string cacheKey = "LukaTube_PlayCounts_" + deviceGuid.Trim().ToLowerInvariant();

        try
        {
            object cached = Cache[cacheKey];
            if (cached is Dictionary<string, int>)
            {
                return (Dictionary<string, int>)cached;
            }
        }
        catch
        {
        }

        try
        {
            string playedApi = "http://172.16.40.100/getplayedfromstb.php?stbclientid="
                               + HttpUtility.UrlEncode(deviceGuid);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(playedApi);
            request.Method = "GET";
            request.Timeout = 7000;
            request.UserAgent = "LukaTube/1.0";

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader stream = new StreamReader(response.GetResponseStream()))
            {
                string json = stream.ReadToEnd();
                counts = ParseVideoPlayCounts(json);
            }

            try
            {
                Cache.Insert(
                    cacheKey,
                    counts,
                    null,
                    DateTime.Now.AddMinutes(10),
                    System.Web.Caching.Cache.NoSlidingExpiration
                );
            }
            catch
            {
            }
        }
        catch
        {
        }

        return counts;
    }

    private Dictionary<string, int> ParseVideoPlayCounts(string json)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
            return counts;

        try
        {
            JObject obj = JObject.Parse(json);

            if (obj["videoPlayCounts"] != null && obj["videoPlayCounts"].Type == JTokenType.Object)
            {
                JObject vp = (JObject)obj["videoPlayCounts"];

                foreach (var prop in vp.Properties())
                {
                    int val;
                    if (int.TryParse(prop.Value.ToString(), out val))
                    {
                        string key = prop.Name.Trim();
                        counts[key] = val;
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

    private string BuildNetflixGridFiles(
        List<string> filesArray,
        int startOffset,
        HttpRequest req,
        string deviceGuid,
        bool useLocal,
        bool newVideosMode,
        bool playedByMode,
        bool globalOnlineServer,
        bool hideVideoInfo,
        Dictionary<string, int> videoPlayCounts)
    {
        const int ITEMS_PER_ROW = 5;
        const int CARD_WIDTH = 200;
        const int CARD_HEIGHT = 120;
        const int CARD_SPACING = 20;

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("      <VerticalFlowPanel id=\"VideoGrid\" top=\"140\" left=\"40\" width=\"1200\" height=\"530\" clipsChildren=\"true\" itemSpacing=\"" + CARD_SPACING + "\">");

        for (int i = 0; i < filesArray.Count; i += ITEMS_PER_ROW)
        {
            sb.AppendLine("        <HorizontalFlowPanel height=\"" + CARD_HEIGHT + "\" itemSpacing=\"" + CARD_SPACING + "\">");

            int sliceEnd = Math.Min(i + ITEMS_PER_ROW, filesArray.Count);
            for (int j = i; j < sliceEnd; j++)
            {
                string fullUrl = filesArray[j];
                int globalIndex = startOffset + j;

                string name = Path.GetFileName(fullUrl) ?? ("video" + (globalIndex + 1));
                try { name = HttpUtility.UrlDecode(name); } catch { }
                name = Regex.Replace(name, @"\.(mp4|m4v|mov)$", "", RegexOptions.IgnoreCase);
                string safeName = EscapeXml(name.Replace("_", " "));

                int playCount = playedByMode ? GetPlayCountForVideo(fullUrl, videoPlayCounts) : 0;

              string currentSearch = req.QueryString["SearchLukaTube"] ?? "";

string playUrl = req.Url.Scheme + "://" + req.Url.Authority
                 + "/SETTEMediaroomApp/PlayVideo.aspx?video_url="
                 + HttpUtility.UrlEncode(fullUrl)
                 + "&video_name=" + HttpUtility.UrlEncode(name)
                 + "&newVideos=" + (newVideosMode ? "true" : "false");

if (!string.IsNullOrEmpty(currentSearch))
{
    playUrl += "&SearchLukaTube=" + HttpUtility.UrlEncode(currentSearch);
}

                if (!string.IsNullOrEmpty(deviceGuid))
                    playUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

                if (useLocal)
                    playUrl += "&LocalFolder=true";
                else
                    playUrl += "&LocalFolder=false";

                if (playedByMode)
                    playUrl += "&sort=playedby";

                if (globalOnlineServer)
                    playUrl += "&GlobalOnlineServer=true";

                if (hideVideoInfo)
                    playUrl += "&HideVideoInfo=true";

                sb.AppendLine("          <Button id=\"video_" + globalIndex + "\" width=\"" + CARD_WIDTH + "\" height=\"" + CARD_HEIGHT + "\" focusScale=\"1.08\" backgroundFocus=\"argb(255,40,40,40)\" justification=\"center\" href=\"page:" + EscapeXml(playUrl) + "\">");
                sb.AppendLine("            <Text top=\"10\" width=\"" + CARD_WIDTH + "\" height=\"" + (CARD_HEIGHT - (playedByMode ? 35 : 20)) + "\" fontstyle=\"Reg18\" lines=\"3\" alignment=\"center\" ellipsize=\"end\">" + safeName + "</Text>");

                if (playedByMode)
                    sb.AppendLine("            <Text top=\"88\" width=\"" + CARD_WIDTH + "\" height=\"22\" fontstyle=\"Reg14\" alignment=\"center\" foreground=\"argb(255,200,200,200)\">Played: " + playCount.ToString() + "</Text>");

                sb.AppendLine("            <Actions><Event type=\"onclick\" action=\"navigate\" url=\"page:" + EscapeXml(playUrl) + "\" /></Actions>");
                sb.AppendLine("          </Button>");
            }

            sb.AppendLine("        </HorizontalFlowPanel>");
        }

        sb.AppendLine("      </VerticalFlowPanel>");
        return sb.ToString();
    }
}