using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;

namespace PFTvBills
{
    public partial class DefaultPage : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string phone = Request.QueryString["phone"] ?? "070844299";
            string tab = (Request.QueryString["tab"] ?? "profile").ToLower();

            int playlistStart = GetInt("playlistStart", 0);
            int songStart = GetInt("songStart", 0);

            User user = new User();
            user.playlists = new List<Playlist>();

            try
            {
                string apiUrl =
                    "http://172.16.40.100/getUserByPhoneNumber.php?phone=" +
                    HttpUtility.UrlEncode(phone);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
                request.Method = "GET";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string json = reader.ReadToEnd();

                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    var data = serializer.Deserialize<Dictionary<string, object>>(json);

                    if (data != null && data.ContainsKey("success") && Convert.ToBoolean(data["success"]))
                    {
                        var userData = data["user"] as Dictionary<string, object>;
                        if (userData != null)
                        {
                            user = MergeUser(userData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("API error: " + ex.Message);
            }

            string mrml = BuildMRML(user, phone, tab, playlistStart, songStart);

            Response.ContentType = "application/xml";
            Response.Write(mrml);
            Response.End();
        }

        // ================= FIX JSON =================
        private User MergeUser(Dictionary<string, object> data)
        {
            User u = new User();

            u.username = Get(data, "username");
            u.full_name = Get(data, "full_name");
            u.profile_picture_url = FixImg(Get(data, "profile_picture_url"));
            u.bio = Get(data, "bio");
            u.gym_name = Get(data, "gym_name");
            u.gym_location = Get(data, "gym_location");
            u.age = Get(data, "age");

            u.playlists = new List<Playlist>();

            if (data.ContainsKey("playlists") && data["playlists"] is ArrayList)
            {
                ArrayList playlists = (ArrayList)data["playlists"];

                foreach (var p in playlists)
                {
                    Dictionary<string, object> pd = p as Dictionary<string, object>;
                    if (pd == null) continue;

                    Playlist pl = new Playlist();
                    pl.name = Get(pd, "name");
                    pl.cover_url = FixImg(Get(pd, "cover_url"));
                    pl.songs = new List<Song>();

                    if (pd.ContainsKey("songs") && pd["songs"] is ArrayList)
                    {
                        ArrayList songs = (ArrayList)pd["songs"];

                        foreach (var s in songs)
                        {
                            Dictionary<string, object> sd = s as Dictionary<string, object>;
                            if (sd == null) continue;

                            Song song = new Song();
                            song.title = Get(sd, "title");
                            song.artist = Get(sd, "artist");
                            song.url = Get(sd, "url");
                            song.cover_url = FixImg(Get(sd, "cover_url"));

                            pl.songs.Add(song);
                        }
                    }

                    u.playlists.Add(pl);
                }
            }

            return u;
        }

        // ================= MRML =================
        private string BuildMRML(User user, string phone, string tab, int playlistStart, int songStart)
        {
            StringBuilder sb = new StringBuilder();

            string baseUrl = Request.Url.GetLeftPart(UriPartial.Path);

            string profileTab = baseUrl + "?phone=" + HttpUtility.UrlEncode(phone) + "&tab=profile";
            string playlistsTab = baseUrl + "?phone=" + HttpUtility.UrlEncode(phone) + "&tab=playlists&playlistStart=0";

            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Append("<uidescription version=\"3.0\">");
            sb.Append("<MrmlPage id=\"ProfilePage\" width=\"1280\" height=\"720\">");
            sb.Append("<Panel width=\"1280\" height=\"720\">");

            // TABS
            sb.Append("<HorizontalFlowPanel top=\"60\" left=\"20\" width=\"1240\" height=\"44\">");
            sb.Append("<Button width=\"200\" height=\"44\" href=\"page:" + Xml(profileTab) + "\"><Text>Profile</Text></Button>");
            sb.Append("<Button width=\"200\" height=\"44\" href=\"page:" + Xml(playlistsTab) + "\"><Text>Playlists</Text></Button>");
            sb.Append("</HorizontalFlowPanel>");

            // PROFILE
            sb.Append("<Panel visible=\"" + (tab == "profile" ? "true" : "false") + "\" top=\"120\" left=\"20\" width=\"1240\" height=\"560\">");

            if (!string.IsNullOrEmpty(user.profile_picture_url))
                sb.Append("<Image width=\"160\" height=\"160\" url=\"" + Xml(user.profile_picture_url) + "\" />");

            sb.Append("<VerticalFlowPanel left=\"200\" width=\"1000\">");
            sb.Append("<Text>" + Xml(user.full_name) + "</Text>");
            sb.Append("<Text>@" + Xml(user.username) + "</Text>");
            sb.Append("<Text>" + Xml(user.bio) + "</Text>");
            sb.Append("<Text>Gym: " + Xml(user.gym_name) + "</Text>");
            sb.Append("<Text>Location: " + Xml(user.gym_location) + "</Text>");
            sb.Append("<Text>Age: " + Xml(user.age) + "</Text>");
            sb.Append("</VerticalFlowPanel>");

            sb.Append("</Panel>");

            // PLAYLISTS
            sb.Append("<VerticalFlowPanel visible=\"" + (tab == "playlists" ? "true" : "false") + "\" top=\"120\" left=\"20\" width=\"1240\" height=\"560\">");

            if (user.playlists == null || user.playlists.Count == 0)
            {
                sb.Append("<Text>No playlists found</Text>");
            }
            else
            {
                int end = Math.Min(playlistStart + 5, user.playlists.Count);

                for (int i = playlistStart; i < end; i++)
                {
                    Playlist pl = user.playlists[i];

                    sb.Append("<Text>Playlist: " + Xml(pl.name) + "</Text>");

                    if (pl.songs != null && pl.songs.Count > 0)
                    {
                        int songEnd = Math.Min(songStart + 5, pl.songs.Count);

                        for (int j = songStart; j < songEnd; j++)
                        {
                            Song song = pl.songs[j];

                            string playUrl =
                                "http://172.16.40.101/SETTEMediaroomApp/GetVideoFromSong.aspx?song_url=" +
                                HttpUtility.UrlEncode(song.url);

                            sb.Append("<HorizontalFlowPanel height=\"80\">");

                            if (!string.IsNullOrEmpty(song.cover_url))
                                sb.Append("<Image width=\"64\" height=\"64\" url=\"" + Xml(song.cover_url) + "\" />");

                            sb.Append("<VerticalFlowPanel width=\"800\">");
                            sb.Append("<Text>" + Xml(song.title) + "</Text>");
                            sb.Append("<Text>" + Xml(song.artist) + "</Text>");
                            sb.Append("</VerticalFlowPanel>");

                            sb.Append("<Button width=\"120\" height=\"40\" href=\"page:" + Xml(playUrl) + "\"><Text>Play</Text></Button>");

                            sb.Append("</HorizontalFlowPanel>");
                        }

                        // LOAD MORE SONGS
                        if (songEnd < pl.songs.Count)
                        {
                            string moreSongs =
                                "http://172.16.40.101/PFTvBills/default.aspx?phone=" +
                                HttpUtility.UrlEncode(phone) +
                                "&tab=playlists&playlistStart=" + playlistStart +
                                "&songStart=" + (songStart + 5);

                            sb.Append("<Button width=\"200\" height=\"40\" href=\"page:" + Xml(moreSongs) + "\"><Text>Load More Songs</Text></Button>");
                        }
                    }
                }

                // LOAD MORE PLAYLISTS
                if (end < user.playlists.Count)
                {
                    string morePlaylists =
                        "http://172.16.40.101/PFTvBills/default.aspx?phone=" +
                        HttpUtility.UrlEncode(phone) +
                        "&tab=playlists&playlistStart=" + (playlistStart + 5);

                    sb.Append("<Button width=\"220\" height=\"40\" href=\"page:" + Xml(morePlaylists) + "\"><Text>Load More Playlists</Text></Button>");
                }
            }

            sb.Append("</VerticalFlowPanel>");
            sb.Append("</Panel>");
            sb.Append("</MrmlPage>");
            sb.Append("</uidescription>");

            return sb.ToString();
        }

        // ================= HELPERS =================
        private int GetInt(string key, int def)
        {
            int v;
            return int.TryParse(Request.QueryString[key], out v) ? v : def;
        }

        private string FixImg(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            return url
                .Replace("http://lukaserver.ddns.net", "http://172.16.40.100")
                .Replace("https://lukaserver.ddns.net", "http://172.16.40.100");
        }

        private string Get(Dictionary<string, object> d, string k)
        {
            return (d != null && d.ContainsKey(k) && d[k] != null)
                ? d[k].ToString()
                : "";
        }

        private string Xml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;");
        }
    }

    // ================= MODELS =================
    public class User
    {
        public string username;
        public string full_name;
        public string profile_picture_url;
        public string bio;
        public string gym_name;
        public string gym_location;
        public string age;
        public List<Playlist> playlists;
    }

    public class Playlist
    {
        public string name;
        public string cover_url;
        public List<Song> songs;
    }

    public class Song
    {
        public string title;
        public string artist;
        public string url;
        public string cover_url;
    }
}