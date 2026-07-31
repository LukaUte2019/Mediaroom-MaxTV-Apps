using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.Script.Serialization;

public partial class NewMessage : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        string userId = Request.QueryString["userid"];
        string threadId = Request.QueryString["thread_id"];
        string toUserId = Request.QueryString["to_userid"];
        string message = Request.QueryString["txtMessage"];

        // NEW: read thread_name, to_username and to_full_name from querystring (may be encoded)
        string threadName = Request.QueryString["thread_name"] ?? "";
        if (!string.IsNullOrEmpty(threadName))
            threadName = HttpUtility.UrlDecode(threadName);

        string toUsername = Request.QueryString["to_username"] ?? "";
        if (!string.IsNullOrEmpty(toUsername))
            toUsername = HttpUtility.UrlDecode(toUsername);

        string toFullName = Request.QueryString["to_full_name"] ?? "";
        if (!string.IsNullOrEmpty(toFullName))
            toFullName = HttpUtility.UrlDecode(toFullName);

        // Fallbacks: if one is missing, try to use the other or toUserId
        if (string.IsNullOrEmpty(toFullName) && !string.IsNullOrEmpty(toUsername))
            toFullName = toUsername;
        if (string.IsNullOrEmpty(toUsername) && !string.IsNullOrEmpty(toFullName))
            toUsername = toFullName;
        if (string.IsNullOrEmpty(toUsername) && string.IsNullOrEmpty(toFullName) && !string.IsNullOrEmpty(toUserId))
            toUsername = "user" + toUserId;

        if (string.IsNullOrEmpty(userId))
        {
            RenderError("Missing userid.");
            return;
        }

        // If message exists -> send
        if (!string.IsNullOrEmpty(message) && !string.IsNullOrEmpty(threadId))
        {
            // pass threadName and recipient info to SendMessage
            bool sent = SendMessage(userId, threadId, message, threadName, toUserId, toUsername, toFullName);
            RenderResult(sent, message, threadId, userId, threadName, toUserId, toUsername, toFullName);
            return;
        }

        // Otherwise show form (pass threadName and recipient info so it will be included in send URL and displayed)
        RenderForm(userId, toUserId, threadId, threadName, toUsername, toFullName);
    }

    // ================= SEND MESSAGE =================

    // UPDATED: SendMessage now accepts threadName and recipient info and includes them in the JSON body sent to the API
    private bool SendMessage(string userId, string threadId, string message, string threadName, string toUserId, string toUsername, string toFullName)
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
                message = message,
                // include thread_name so backend/logging can know the human-friendly thread title
                thread_name = threadName,
                // include recipient info so backend can use/display it if desired
                to_userid = toUserId,
                to_username = toUsername,
                to_full_name = toFullName
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

    // ================= MRML FORM =================

    // UPDATED: RenderForm displays title first ("Nova Poraka"), and under it the recipient full name / username.
    private void RenderForm(string userId, string toUserId, string threadId, string threadName, string toUsername, string toFullName)
    {
        string sendUrl =
            "http://172.16.40.101/SETTEMediaroomApp/NewMessage.aspx"
            + "?userid=" + HttpUtility.UrlEncode(userId)
            + "&to_userid=" + HttpUtility.UrlEncode(toUserId)
            + "&to_username=" + HttpUtility.UrlEncode(toUsername)
            + "&to_full_name=" + HttpUtility.UrlEncode(toFullName)
            + "&thread_id=" + HttpUtility.UrlEncode(threadId)
            + "&thread_name=" + HttpUtility.UrlEncode(threadName); // include thread_name and recipient info here

        // Prepare default message (pre-fill) if thread name is available
        string defaultMessage = "";
        if (!string.IsNullOrEmpty(threadName))
        {
            defaultMessage = "Re: " + threadName;
        }

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("<MrmlPage id=\"NewMessagePage\" width=\"1280\" height=\"720\">");
        sb.AppendLine("<Panel>");

        // ----- CHANGED ORDER: Title first -----
        sb.AppendLine("<Text top=\"40\" left=\"80\" fontstyle=\"Reg36\" foreground=\"argb(255,255,255,255)\">Nova Poraka</Text>");

        // Show recipient (To:) under the title
        if (!string.IsNullOrEmpty(toFullName) || !string.IsNullOrEmpty(toUsername))
        {
            string toDisplay = "";
            if (!string.IsNullOrEmpty(toFullName))
                toDisplay = "To: " + HttpUtility.HtmlEncode(toFullName);
            if (!string.IsNullOrEmpty(toUsername))
            {
                if (!string.IsNullOrEmpty(toDisplay))
                    toDisplay += " ";
                toDisplay += HttpUtility.HtmlEncode("(" + "@" + toUsername + ")");
            }

            // placed under the title
            sb.AppendLine("<Text top=\"100\" left=\"80\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">" 
                + toDisplay + "</Text>");
        }


        // EditText for message — if defaultMessage is set, place it as inner text so the field is prefilled
        if (!string.IsNullOrEmpty(defaultMessage))
        {
            sb.AppendLine("<EditText id=\"txtMessage\" top=\"180\" left=\"80\" width=\"1120\" height=\"200\" fontstyle=\"Reg24\" background=\"argb(255,40,40,40)\">"
                + HttpUtility.HtmlEncode(defaultMessage) + "</EditText>");
        }
        else
        {
            sb.AppendLine("<EditText id=\"txtMessage\" top=\"180\" left=\"80\" width=\"1120\" height=\"200\" fontstyle=\"Reg24\" background=\"argb(255,40,40,40)\" />");
        }

        // Global action: submit the EditText value as txtMessage via GET (matches existing pattern)
        sb.AppendLine("<Actions>");
        sb.AppendLine("  <Action name=\"SendMessageToUser\" type=\"submit\" data=\"txtMessage\" method=\"GET\" url=\"page:" 
            + HttpUtility.HtmlAttributeEncode(sendUrl) + "\" />");
        sb.AppendLine("</Actions>");

        // Button to trigger the action
        sb.AppendLine("<Button top=\"420\" left=\"80\" width=\"300\" height=\"80\">");
        sb.AppendLine("  <Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">Isprati</Text>");
        sb.AppendLine("  <Actions>");
        sb.AppendLine("    <Event type=\"onclick\" action=\"SendMessageToUser\" />");
        sb.AppendLine("  </Actions>");
        sb.AppendLine("</Button>");

        // Optional cancel/back button — returns to threads (keep thread context if available)
        string backUrl =
            "http://172.16.40.101/SETTEMediaroomApp/DMThreads.aspx"
            + "?userid=" + HttpUtility.UrlEncode(userId)
            + (string.IsNullOrEmpty(threadId) ? "" : "&thread_id=" + HttpUtility.UrlEncode(threadId))
            + (string.IsNullOrEmpty(threadName) ? "" : "&thread_name=" + HttpUtility.UrlEncode(threadName));

        sb.AppendLine(string.Format(
            "<Button top=\"420\" left=\"400\" width=\"300\" height=\"80\" href=\"page:{0}\">" +
            "<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">Nazad</Text>" +
            "</Button>",
            HttpUtility.HtmlAttributeEncode(backUrl)
        ));

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    // ================= RESULT =================

    // UPDATED: RenderResult also receives recipient info and keeps it in the back URL (so DMThreads can keep context)
    private void RenderResult(bool success, string message, string threadId, string userId, string threadName, string toUserId, string toUsername, string toFullName)
    {
        string backUrl =
            "http://172.16.40.101/SETTEMediaroomApp/DMThreads.aspx"
            + "?thread_id=" + HttpUtility.UrlEncode(threadId)
            + "&userid=" + HttpUtility.UrlEncode(userId)
            + (string.IsNullOrEmpty(threadName) ? "" : "&thread_name=" + HttpUtility.UrlEncode(threadName));

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("<MrmlPage id=\"NewMessageResult\" width=\"1280\" height=\"720\">");
        sb.AppendLine("<Panel>");

        sb.AppendLine(string.Format(
            "<Text top=\"150\" left=\"80\" fontstyle=\"Reg36\" foreground=\"argb(255,255,255,255)\">{0}</Text>",
            success ? "Porakata e ispratena!" : "Neuspesno prakjanje!"
        ));

        // Show the message text back to user (encoded)
        sb.AppendLine(string.Format(
            "<Text top=\"220\" left=\"80\" width=\"1120\" fontstyle=\"Reg24\" foreground=\"argb(255,200,200,200)\">{0}</Text>",
            HttpUtility.HtmlEncode(message)
        ));

        if (!string.IsNullOrEmpty(toFullName) || !string.IsNullOrEmpty(toUsername))
        {
            string toDisplay = "";
            if (!string.IsNullOrEmpty(toFullName))
                toDisplay = HttpUtility.HtmlEncode(toFullName);
            if (!string.IsNullOrEmpty(toUsername))
                toDisplay += " (" + HttpUtility.HtmlEncode("@" + toUsername) + ")";

            sb.AppendLine(string.Format(
                "<Text top=\"340\" left=\"80\" width=\"1120\" fontstyle=\"Reg20\" foreground=\"argb(255,200,200,200)\">{0}</Text>",
                toDisplay
            ));
        }

        sb.AppendLine(string.Format(
            "<Button top=\"400\" left=\"80\" width=\"300\" height=\"80\" href=\"page:{0}\">" +
            "<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">Nazad</Text>" +
            "</Button>",
            HttpUtility.HtmlAttributeEncode(backUrl)
        ));

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    private void RenderError(string text)
    {
        Response.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<uidescription version=\"3.0\">\n" +
            "<MrmlPage width=\"1280\" height=\"720\">\n" +
            "<Panel>\n" +
            "<Text top=\"200\" left=\"80\" fontstyle=\"Reg36\" foreground=\"argb(255,255,60,60)\">" +
            HttpUtility.HtmlEncode(text) +
            "</Text>\n" +
            "</Panel>\n" +
            "</MrmlPage>\n" +
            "</uidescription>");
        Response.End();
    }
}