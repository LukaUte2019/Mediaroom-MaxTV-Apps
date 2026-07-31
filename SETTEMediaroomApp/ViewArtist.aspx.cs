using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;

public partial class ViewArtist : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string artistName = (Request.QueryString["artist"] ?? "").Trim();
        string deviceGuid = Request.QueryString["DeviceGuid"];

        // Album paging
        int albumIndex = 0;
        if (!string.IsNullOrEmpty(Request.QueryString["albumIndex"]))
            int.TryParse(Request.QueryString["albumIndex"], out albumIndex);

        // Song paging within the album
        int songStartIndex = 0;
        if (!string.IsNullOrEmpty(Request.QueryString["songStartIndex"]))
            int.TryParse(Request.QueryString["songStartIndex"], out songStartIndex);

        const int SONGS_PER_PAGE = 7;

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        if (string.IsNullOrEmpty(artistName))
        {
            Response.Write("<Text>No artist specified</Text>");
            Response.End();
            return;
        }

        List<AlbumInfo> albums = GetArtistAlbums(artistName);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("<MrmlPage id=\"ViewArtistPage\" appid=\"lukatube.artist/1.0\" width=\"1280\" height=\"720\">");
        sb.AppendLine("<Panel>");

        sb.AppendLine("<Text top=\"20\" left=\"40\" width=\"900\" height=\"36\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">" +
                      HttpUtility.HtmlEncode(artistName) + "</Text>");

        string searchArtistUrl = "LukifyMusic.aspx?search=" + HttpUtility.UrlEncode(artistName);
        if (!string.IsNullOrEmpty(deviceGuid))
            searchArtistUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

        sb.AppendLine("<Button id=\"btnSearchArtist\" top=\"18\" left=\"980\" width=\"240\" height=\"44\" href=\"" +
                      HttpUtility.HtmlAttributeEncode(searchArtistUrl) + "\">" +
                      "<Text top=\"7\" left=\"12\" width=\"220\" height=\"28\" fontstyle=\"Reg20\" foreground=\"argb(255,255,255,255)\">Search in Lukify Music</Text>" +
                      "</Button>");

        int topPos = 70;

        if (albums.Count == 0)
        {
            sb.AppendLine("<Text top=\"" + topPos + "\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg28\" foreground=\"argb(255,255,60,60)\">No albums found</Text>");
        }
        else
        {
            // Clamp albumIndex
            if (albumIndex < 0) albumIndex = 0;
            if (albumIndex >= albums.Count) albumIndex = albums.Count - 1;

            var album = albums[albumIndex];
            string albumTitle = string.IsNullOrEmpty(album.album) ? "Single / Unnamed Album" : album.album;

            sb.AppendLine("<Text top=\"" + topPos + "\" left=\"40\" width=\"1200\" height=\"28\" fontstyle=\"Reg24\" foreground=\"argb(255,200,200,255)\">" +
                          HttpUtility.HtmlEncode(albumTitle) + " (" + album.song_count + " songs)</Text>");
            topPos += 32;

            int endIndex = Math.Min(songStartIndex + SONGS_PER_PAGE, album.songs.Count);

            for (int i = songStartIndex; i < endIndex; i++)
            {
                var song = album.songs[i];

                string hrefUrl = "page:http://172.16.40.101/SETTEMediaroomApp/GetVideoFromSong.aspx?song_url=" +
                                 HttpUtility.UrlEncode(song.file_url);
                if (!string.IsNullOrEmpty(deviceGuid))
                    hrefUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

                string safeId = "btn_" + SanitizeId(song.title);

                // Replace lukaserver.ddns.net with 172.16.40.100 in cover URL
                string coverUrl = song.cover ?? "";
                coverUrl = coverUrl.Replace("lukaserver.ddns.net", "172.16.40.100");

                sb.AppendLine("<Button id=\"" + HttpUtility.HtmlAttributeEncode(safeId) + "\" top=\"" + topPos + "\" left=\"60\" width=\"1180\" height=\"70\" href=\"" + HttpUtility.HtmlAttributeEncode(hrefUrl) + "\">" +
                              "<Image top=\"5\" left=\"5\" width=\"60\" height=\"60\" url=\"" + HttpUtility.HtmlAttributeEncode(coverUrl) + "\"/>" +
                              "<Text top=\"5\" left=\"75\" alignment=\"left\" justification=\"left\" fontstyle=\"Reg20\" foreground=\"argb(255,255,255,255)\">" + HttpUtility.HtmlEncode(song.title) + "</Text>" +
                              "<Text top=\"28\" left=\"75\" alignment=\"left\" justification=\"left\" fontstyle=\"Reg16\" foreground=\"argb(255,200,200,200)\">" + HttpUtility.HtmlEncode(song.artist) + "</Text>" +
                              "</Button>");

                topPos += 75;
            }

            // Load more songs
            if (endIndex < album.songs.Count)
            {
                string loadMoreUrl = "ViewArtist.aspx?artist=" + HttpUtility.UrlEncode(artistName) +
                                     "&albumIndex=" + albumIndex +
                                     "&songStartIndex=" + endIndex;
                if (!string.IsNullOrEmpty(deviceGuid))
                    loadMoreUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

                sb.AppendLine("<Button id=\"btnLoadMore\" top=\"" + (topPos + 20) + "\" left=\"40\" width=\"200\" height=\"50\" href=\"" + HttpUtility.HtmlAttributeEncode(loadMoreUrl) + "\">" +
                              "<Text top=\"5\" left=\"10\" fontstyle=\"Reg20\" foreground=\"argb(255,255,255,255)\">Load More Songs</Text>" +
                              "</Button>");
                topPos += 70;
            }

            // Album navigation
            if (albumIndex > 0)
            {
                string prevUrl = "ViewArtist.aspx?artist=" + HttpUtility.UrlEncode(artistName) + "&albumIndex=" + (albumIndex - 1);
                if (!string.IsNullOrEmpty(deviceGuid))
                    prevUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

                sb.AppendLine("<Button id=\"btnPrev\" top=\"" + (topPos + 20) + "\" left=\"40\" width=\"200\" height=\"50\" href=\"" + HttpUtility.HtmlAttributeEncode(prevUrl) + "\">" +
                              "<Text top=\"5\" left=\"10\" fontstyle=\"Reg20\" foreground=\"argb(255,255,255,255)\">Previous Album</Text>" +
                              "</Button>");
            }

            if (albumIndex < albums.Count - 1)
            {
                string nextUrl = "ViewArtist.aspx?artist=" + HttpUtility.UrlEncode(artistName) + "&albumIndex=" + (albumIndex + 1);
                if (!string.IsNullOrEmpty(deviceGuid))
                    nextUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

                sb.AppendLine("<Button id=\"btnNext\" top=\"" + (topPos + 20) + "\" left=\"1040\" width=\"200\" height=\"50\" href=\"" + HttpUtility.HtmlAttributeEncode(nextUrl) + "\">" +
                              "<Text top=\"5\" left=\"10\" fontstyle=\"Reg20\" foreground=\"argb(255,255,255,255)\">Next Album</Text>" +
                              "</Button>");
            }
        }

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    private List<AlbumInfo> GetArtistAlbums(string artistId)
    {
        List<AlbumInfo> result = new List<AlbumInfo>();
        string url = "http://172.16.40.100/youtubeclone/radio/searchalbumartist.php?artist=" + HttpUtility.UrlEncode(artistId);

        try
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 10000;
            req.UserAgent = "LukaTube/1.0";

            string respBody;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                respBody = sr.ReadToEnd();
            }

            JObject j = JObject.Parse(respBody);
            JArray albumsArray = (JArray)j["albums"];

            if (albumsArray != null)
            {
                foreach (var a in albumsArray)
                {
                    AlbumInfo album = new AlbumInfo();
                    album.album = (a["album"] != null) ? a["album"].ToString() : "";
                    album.album_cover = (a["album_cover"] != null) ? a["album_cover"].ToString() : "";
                    album.song_count = (a["song_count"] != null) ? Convert.ToInt32(a["song_count"]) : 0;
                    album.songs = new List<SongInfo>();

                    JArray songsArray = (JArray)a["songs"];
                    if (songsArray != null)
                    {
                        foreach (var s in songsArray)
                        {
                            SongInfo song = new SongInfo();
                            song.title = (s["title"] != null) ? s["title"].ToString() : "";
                            song.file_url = (s["file_url"] != null) ? s["file_url"].ToString() : "";
                            song.cover = (s["cover"] != null) ? s["cover"].ToString() : "";
                            song.artist = (s["artist"] != null) ? s["artist"].ToString() : "";

                            album.songs.Add(song);
                        }
                    }

                    result.Add(album);
                }
            }
        }
        catch
        {
            // optionally log
        }

        return result;
    }

    private string SanitizeId(string input)
    {
        if (string.IsNullOrEmpty(input)) return "unknown";
        StringBuilder sb = new StringBuilder();
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else
                sb.Append('_');
        }
        return sb.Length <= 60 ? sb.ToString() : sb.ToString().Substring(0, 60);
    }

    private class AlbumInfo
    {
        public string album;
        public string album_cover;
        public int song_count;
        public List<SongInfo> songs;
    }

    private class SongInfo
    {
        public string title;
        public string file_url;
        public string cover;
        public string artist;
    }
}