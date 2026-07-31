using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SETTEMediaroomApp
{
    public partial class YouTubeStream : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.BufferOutput = false;
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.ContentType = "video/mp4";

            string videoId = Request.QueryString["videoId"];
            if (string.IsNullOrEmpty(videoId))
                videoId = "dQw4w9WgXcQ";

            string ffmpegPath = Server.MapPath("~/bin/ffmpeg.exe");
            int bitrate = 1500;

            string downloadUrl = null;

            try
            {
                // Get download URL from LukaTube Downloader API
                string youtubeUrl = "https://www.youtube.com/watch?v=" + HttpUtility.UrlEncode(videoId);
                string apiUrl =
                    "https://lukaserver.ddns.net/LukaTube-Downloader-API/index.php?url=" +
                    HttpUtility.UrlEncode(youtubeUrl);

                string respJson = HttpGet(apiUrl);

                JObject o = JObject.Parse(respJson);

                if (o["video_url"] != null)
                    downloadUrl = o["video_url"].ToString();
                else if (o["url"] != null)
                    downloadUrl = o["url"].ToString();
                else if (o["download_url"] != null)
                    downloadUrl = o["download_url"].ToString();

                if (string.IsNullOrEmpty(downloadUrl))
                    throw new Exception("No download URL returned from API");

                Log("Download URL: " + downloadUrl);
            }
            catch (Exception ex)
            {
                Log("API ERROR: " + ex.Message);
                Response.StatusCode = 500;
                Response.Write("Error getting video URL: " + ex.Message);
                Response.End();
                return;
            }

            // Handle HTTP Range for STB
            long startByte = 0;
            long endByte = -1;
            string rangeHeader = Request.Headers["Range"];

            if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
            {
                string[] parts = rangeHeader.Substring(6).Split('-');
                if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
                    long.TryParse(parts[0], out startByte);

                if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                    long.TryParse(parts[1], out endByte);

                Response.StatusCode = 206;
                Response.AddHeader("Accept-Ranges", "bytes");
            }
            else
            {
                Response.StatusCode = 200;
                Response.AddHeader("Accept-Ranges", "bytes");
            }

            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(downloadUrl);
                req.Method = "GET";
                req.Timeout = 30000;
                req.ReadWriteTimeout = 30000;
                req.AllowAutoRedirect = true;
                req.UserAgent = "SETTEMediaroomApp/1.0";

                // If the remote server supports ranges, request only what the client asked for
                if (startByte > 0 || endByte >= 0)
                {
                    if (endByte >= startByte && endByte >= 0)
                        req.AddRange(startByte, endByte);
                    else
                        req.AddRange(startByte);
                }

                string eth4IP = GetEthernet4IP();
                if (!string.IsNullOrEmpty(eth4IP))
                {
                    req.ServicePoint.BindIPEndPointDelegate = (sp, ep, retry) =>
                    {
                        try
                        {
                            return new IPEndPoint(IPAddress.Parse(eth4IP), 0);
                        }
                        catch
                        {
                            return null;
                        }
                    };
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream input = resp.GetResponseStream())
                using (Process ffmpeg = new Process())
                {
                    ffmpeg.StartInfo.FileName = ffmpegPath;
                    ffmpeg.StartInfo.Arguments =
                        "-i pipe:0 " +
                        "-c:v libx264 -profile:v main -level 3.1 " +
                        "-preset ultrafast -pix_fmt yuv420p " +
                        "-b:v " + bitrate + "k -maxrate " + bitrate + "k -bufsize " + (bitrate * 2) + "k " +
                        "-c:a aac -b:a 128k -ac 2 " +
                        "-movflags +frag_keyframe+empty_moov+faststart " +
                        "-f mp4 pipe:1";

                    ffmpeg.StartInfo.UseShellExecute = false;
                    ffmpeg.StartInfo.RedirectStandardInput = true;
                    ffmpeg.StartInfo.RedirectStandardOutput = true;
                    ffmpeg.StartInfo.RedirectStandardError = true;
                    ffmpeg.StartInfo.CreateNoWindow = true;

                    ffmpeg.ErrorDataReceived += (s, ev) =>
                    {
                        if (!string.IsNullOrEmpty(ev.Data))
                            Log("FFmpeg: " + ev.Data);
                    };

                    ffmpeg.Start();
                    ffmpeg.BeginErrorReadLine();

                    Task.Run(() =>
                    {
                        try
                        {
                            if (input != null)
                                input.CopyTo(ffmpeg.StandardInput.BaseStream);
                        }
                        catch (Exception ex)
                        {
                            Log("Input stream error: " + ex.Message);
                        }
                        finally
                        {
                            try { ffmpeg.StandardInput.Close(); } catch { }
                        }
                    });

                    ffmpeg.StandardOutput.BaseStream.CopyTo(Response.OutputStream);
                    ffmpeg.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Log("Streaming ERROR: " + ex.Message);
                Response.StatusCode = 500;
                Response.Write("Error streaming video: " + ex.Message);
                Response.End();
            }
        }

        private string GetEthernet4IP()
        {
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string n = nic.Name.ToLowerInvariant();
                    if (n.Contains("ethernet 4") || n.Contains("eth4"))
                    {
                        foreach (var a in nic.GetIPProperties().UnicastAddresses)
                        {
                            if (a.Address.AddressFamily == AddressFamily.InterNetwork)
                                return a.Address.ToString();
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private string HttpGet(string url)
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.Method = "GET";
            r.Timeout = 120000;
            r.ReadWriteTimeout = 120000;
            r.UserAgent = "SETTEMediaroomApp/1.0";

            string eth4IP = GetEthernet4IP();
            if (!string.IsNullOrEmpty(eth4IP))
            {
                r.ServicePoint.BindIPEndPointDelegate = (sp, ep, retry) =>
                {
                    try
                    {
                        return new IPEndPoint(IPAddress.Parse(eth4IP), 0);
                    }
                    catch
                    {
                        return null;
                    }
                };
            }

            using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }

        private void Log(string msg)
        {
            try
            {
                File.AppendAllText(
                    Server.MapPath("~/youtubeclone/log.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + msg + "\n"
                );
            }
            catch
            {
            }
        }
    }
}