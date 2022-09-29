using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FisherMonitor.Bililivelib
{
    public class RoomInfo
    {
        public string realRoomid;
        public string title;
        public bool liveStatus;
        public string username;
        public bool net_error;
    }

    static public class PathFinder
    {
        static public Task<RoomInfo> GetRoomInfo(string originalRoomId)
        {
            return Task.Run(() => {
                //InfoLogger.SendInfo(originalRoomId, "DEBUG", "正在刷新信息");
                var roominfo_NETERR = new RoomInfo { net_error = true };
                var roomWebPageUrl = "https://api.live.bilibili.com/room/v1/Room/get_info?id=" + originalRoomId;
                var wc = new WebClient();
                wc.Headers.Add("Accept: */*");
                wc.Headers.Add("User-Agent: " + Ver.UA);
                wc.Headers.Add("Accept-Language: zh-CN,zh;q=0.8,en;q=0.6,ja;q=0.4");

                //发送HTTP请求
                byte[] roomHtml;

                try
                {
                    roomHtml = wc.DownloadData(roomWebPageUrl);
                }
                catch(WebException e0)
                {
                    InfoLogger.SendInfo(originalRoomId, "ERROR", "获取房间信息失败<net>：" + e0.Message);
                    return roominfo_NETERR;
                }
                catch (Exception e)
                {
                    InfoLogger.SendInfo(originalRoomId, "ERROR", "获取房间信息失败：" + e.Message);
                    return null;
                }

                //解析返回结果
                try
                {
                    var roomJson = Encoding.UTF8.GetString(roomHtml);
                    var result = JObject.Parse(roomJson);
                    var uid = result["data"]["uid"].ToString();

                    var userInfoUrl = "https://api.bilibili.com/x/web-interface/card?mid=" + uid;
                    var uwc = new WebClient();
                    uwc.Headers.Add("Accept: */*");
                    uwc.Headers.Add("User-Agent: " + Ver.UA);
                    uwc.Headers.Add("Accept-Language: zh-CN,zh;q=0.8,en;q=0.6,ja;q=0.4");

                    byte[] userHtml;
                    try
                    {
                        userHtml = uwc.DownloadData(userInfoUrl);
                    }
                    catch (WebException e0)
                    {
                        InfoLogger.SendInfo(originalRoomId, "ERROR", "获取用户信息失败<net>：" + e0.Message);
                        return roominfo_NETERR;
                    }
                    catch (Exception e)
                    {
                        InfoLogger.SendInfo(originalRoomId, "ERROR", "获取用户信息失败：" + e.Message);
                        return null;
                    }

                    var userJson = Encoding.UTF8.GetString(userHtml);
                    var userResult = JObject.Parse(userJson);
                    var userName = userResult["data"]["card"]["name"].ToString();

                    var roominfo = new RoomInfo
                    {
                        realRoomid = result["data"]["room_id"].ToString(),
                        title = result["data"]["title"].ToString(),
                        liveStatus = result["data"]["live_status"].ToString() == "1" ? true : false,
                        username = userName
                    };
                    return roominfo;
                }
                catch (WebException e0)
                {
                    InfoLogger.SendInfo(originalRoomId, "ERROR", "房间信息解析失败<net>：" + e0.Message);
                    return roominfo_NETERR;
                }
                catch (Exception e)
                {
                    InfoLogger.SendInfo(originalRoomId, "ERROR", "房间信息解析失败：" + e.Message);
                    return null;
                }

                
            });
        }
    }
}
