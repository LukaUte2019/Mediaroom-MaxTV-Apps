using System;
using System.Web;

public partial class DalIEVeligden : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml";
        Response.Cache.SetCacheability(HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        DateTime today = DateTime.Today;
        DateTime easter = GetOrthodoxEaster(today.Year);

        bool isEaster = today.Date == easter.Date;

        string text;
        string subText;

        if (isEaster)
        {
            text = "Da, deneska e Veligden!";
            subText = "Hristos voskrese!";
        }
        else
        {
            int days = (int)(easter - today).TotalDays;
            text = "Ne, deneska ne e Veligden!";
            subText = "Uste " + days + " dena do Veligden";
        }

        // Encode (za da ne pukne MRML)
        text = HttpUtility.HtmlEncode(text);
        subText = HttpUtility.HtmlEncode(subText);

        string mrml = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<uidescription version=""3.0"">

  <MrmlPage width=""1280"" height=""720"">

    <Panel width=""1280"" height=""720"">

      <Text
        left=""100""
        top=""200""
        width=""1080""
        fontstyle=""Reg48""
        align=""center"">
        {0}
      </Text>

      <Text
        left=""100""
        top=""300""
        width=""1080""
        fontstyle=""Reg28""
        align=""center"">
        {1}
      </Text>

    </Panel>

  </MrmlPage>

</uidescription>", text, subText);

        Response.Write(mrml);
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    // Orthodox Easter
    private DateTime GetOrthodoxEaster(int year)
    {
        int a = year % 4;
        int b = year % 7;
        int c = year % 19;

        int d = (19 * c + 15) % 30;
        int e = (2 * a + 4 * b - d + 34) % 7;

        int month = (d + e + 114) / 31;
        int day = ((d + e + 114) % 31) + 1;

        DateTime julian = new DateTime(year, month, day);

        return julian.AddDays(13);
    }
}