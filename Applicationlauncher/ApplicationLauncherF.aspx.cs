using System;
using System.Web;

public partial class ApplicationLauncherF : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        string mrml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<uidescription version=""3.0"">

  <MrmlPage
    id=""TVPage11""
    width=""1280""
    height=""720""
    background=""image(AppImages/pozadina.jpg)"">

    <!-- Global Actions -->
    <Actions>
      <Action
        name=""OpenLukaTube""
        type=""submit""
        data=""DeviceGuid""
        method=""GET""
        url=""page:http://172.16.40.101/SETTEMediaroomApp/LukaTube.aspx"" />
    </Actions>

    <Panel
      id=""TVPanel1""
      left=""0""
      top=""0""
      width=""1280""
      height=""720"">

      <!-- System Info -->
      <DataSource
        id=""SystemInfo""
        uri=""local://system-info"" />

      <!-- Hidden Device GUID -->
      <EditText
        id=""DeviceGuid""
        visible=""false""
        datasource=""{Binding Source=SystemInfo,Path=DeviceId}"" />

      <!-- LukaTube Button -->
      <Button
        id=""TVButtonTVMix""
        left=""250""
        top=""250""
        width=""150""
        height=""150"">
        <!-- Small App Logo Overlay -->
        <Image
            id=""LogoLukaTube""
            width=""146""
            height=""146""
            background=""image(AppImages/youtube.png)"" />

        <Actions>
          <Event type=""onclick"" action=""OpenLukaTube"" />
        </Actions>
      </Button>

      <!-- App Store Button -->
      <Button
        id=""TVButtonAppStore""
        left=""430""
        top=""250""
        width=""150""
        height=""150""
        href=""page:http://172.16.40.100/stbappstore/appstore.php"" >

        <!-- Small App Logo Overlay -->
        <Image
            id=""LogoAppStore""
            width=""146""
            height=""146""
            background=""image(AppImages/appstore.png)"" />
      </Button>

      <!-- Labels -->
      <Text
        id=""TVLabel1""
        left=""600""
        top=""115""
        fontstyle=""Reg32""
        foreground=""argb(255,226,0,116)"">
        LukaTube@MaxTV
      </Text>

      <Text
        id=""TVLabel2""
        left=""600""
        top=""160""
        width=""420""
        fontstyle=""Reg26""
        foreground=""argb(255,255,255,255)"">
        Enjoy your favorite videos on LukaTube for MaxTV
      </Text>

    </Panel>

  </MrmlPage>
</uidescription>";

        Response.Write(mrml);
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }
}