## LukaTube for MaxTV

A Node.js backend server that serves **TV Mediaroom-style applications** with Netflix-style video grids, MRML pages, user profiles, playlists, and video playback. Designed for IPTV/MaxTV-style environments.

## How to host your Mediaroom application server

1. Set your PC's IP to: 172.16.40.101

2. Go to sharing tab in the wifi network interface propertes

4. Select allow other network users to connect through this computer internet connection.

5. in the drop down menu, Select the Ethernet LAN adapter that the IPTV STB is connected to your PC

6. Press OK and close the window

7. Open IIS and Click Add Website and set the folder of files to serve in content directory physical path to the extracted files from the repository

8. IIS server will be started on http://172.16.40.101:80

9. On the IPTV STB, to open LukaTube press the Menu > Applications and press LukaTube in the list of apps

10. Find a video you like to watch and press it. You can also search the library of LukaTube Videos

11. On the IPTV STB, to open The user profile press the Menu > TV Packages > My Account. You can then search a phone number thats linked to a Lukify Music Account.

12. If you want videos to show on the STB App, you need a seperate web server with mp4 video files at http://172.16.40.100/youtubeclone/videos_mediaroom/ connected to the router and on the same wifi netwok as PC
