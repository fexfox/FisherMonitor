﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

// Github: copyliu/bilibili_dm
namespace FisherMonitor.Commentlib
{
    public class CommentProvider
    {
        private readonly string _roomid;
        private bool _connected; //连接情况

        private TcpClient _client;
        private NetworkStream _netStream;
        private const short Protocolversion = 1;

        //事件
        public event ReceivedCommentEvt OnReceivedComment;
        public event DisconnectEvt OnDisconnected;
        public event ReceivedRoomCountEvt OnReceivedRoomCount;

        public CommentProvider(string roomid)
        {
            _roomid = roomid;
        }

        public async void Connect()
        {
            try
            {
                var cmtServer = await GetCmtServer();
                var cmtHost = cmtServer.Item1;
                var cmtPort = cmtServer.Item2;

                if (cmtHost == null)
                {
                    throw new Exception("无法获得弹幕服务器地址");
                }

                //连接弹幕服务器
                _client = new TcpClient();
                await _client.ConnectAsync(cmtHost, cmtPort);
                _netStream = _client.GetStream();

                int.TryParse(_roomid, out int roomIdNumber);
                if (SendJoinChannel(roomIdNumber))
                {
                    _connected = true;
                    HeartbeatLoop();
                    var thread = new Thread(ReceiveMessageLoop) { IsBackground = true };
                    thread.Start();
                }
                else
                {
                    InfoLogger.SendInfo(_roomid, "ERROR", "加入频道失败");
                    throw new Exception("Could't add the channel");
                }
            }catch(Exception e)
            {
                InfoLogger.SendInfo(_roomid, "ERROR", e.Message);
                _disconnect(e);
            }
        }

        private void ReceiveMessageLoop()
        {
            try
            {
                var stableBuffer = new byte[_client.ReceiveBufferSize];
                while (_connected)
                {

                    _netStream.ReadB(stableBuffer, 0, 4);
                    var packetlength = BitConverter.ToInt32(stableBuffer, 0);
                    packetlength = IPAddress.NetworkToHostOrder(packetlength);

                    if (packetlength < 16)
                    {
                        throw new NotSupportedException("协议失败: (L:" + packetlength + ")");
                    }

                    _netStream.ReadB(stableBuffer, 0, 2);//magic
                    _netStream.ReadB(stableBuffer, 0, 2);//protocol_version 

                    _netStream.ReadB(stableBuffer, 0, 4);
                    var typeId = BitConverter.ToInt32(stableBuffer, 0);
                    typeId = IPAddress.NetworkToHostOrder(typeId);

                    _netStream.ReadB(stableBuffer, 0, 4);//magic, params?
                    var playloadlength = packetlength - 16;
                    if (playloadlength == 0)
                    {
                        continue;//没有内容了
                    }

                    typeId = typeId - 1;//magic, again (为啥要减一啊) 
                    var buffer = new byte[playloadlength];
                    _netStream.ReadB(buffer, 0, playloadlength);
                    switch (typeId)
                    {
                        case 0:
                        case 1:
                        case 2:
                            {
                                var viewer = BitConverter.ToUInt32(buffer.Take(4).Reverse().ToArray(), 0); //观众人数
                                OnReceivedRoomCount?.Invoke(this, new ReceivedRoomCountArgs { UserCount = viewer });
                                break;
                            }
                        case 3:
                        case 4://playerCommand
                            {

                                var json = Encoding.UTF8.GetString(buffer, 0, playloadlength);
                                try
                                {
                                    var nowTime = Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalMilliseconds);
                                    var cmt = new CommentModel(json, nowTime, 2);
                                    OnReceivedComment?.Invoke(this, new ReceivedCommentArgs { Comment = cmt });
                                }
                                catch (Exception)
                                {
                                    // ignored
                                }
                                break;
                            }
                        case 5://newScrollMessage
                            {
                                break;
                            }
                        case 7:
                            {
                                break;
                            }
                        case 16:
                            {
                                break;
                            }
                        default:
                            {
                                break;
                            }                  
                    }
                }
            }
            catch (NotSupportedException e)
            {
                _disconnect(e);
            }
            catch (Exception e)
            {
                _disconnect(e);

            }
        }

        private async void HeartbeatLoop()
        {
            try
            {
                while (_connected)
                {
                    SendHeartbeatAsync();
                    await Task.Delay(30000);
                }
            }
            catch (Exception e)
            {
                InfoLogger.SendInfo(_roomid, "ERROR", e.Message);
                _disconnect(e);
            }
        }

        public void Disconnect()
        {

            _connected = false;
            try
            {
                _client.Close();
            }
            catch
            {
                //ignore
            }
            _netStream = null;
        }

        private void _disconnect(Exception e)
        {
            if (!_connected) return;
            InfoLogger.SendInfo(_roomid, "INFO", "连接断开");
            _connected = false;
            _client.Close();
            _netStream = null;
            OnDisconnected?.Invoke(this, new DisconnectEvtArgs { Error = e });
        }

        private void SendHeartbeatAsync()
        {
            SendSocketData(2);
        }


        //发送加入频道数据
        private bool SendJoinChannel(int channelId)
        {
            var r = new Random();
            var tmpuid = (long)(1e14 + 2e14 * r.NextDouble());
            var packetModel = new { roomid = channelId, uid = tmpuid };
            var playload = JsonConvert.SerializeObject(packetModel);
            SendSocketData(7, playload);
            return true;
        }

        private void SendSocketData(int action, string body = "")
        {
            SendSocketData(0, 16, Protocolversion, action, 1, body);
        }

        private void SendSocketData(int packetlength, short magic, short ver, int action, int param = 1, string body = "")
        {
            var playload = Encoding.UTF8.GetBytes(body);
            if (packetlength == 0)
            {
                packetlength = playload.Length + 16;
            }
            var buffer = new byte[packetlength];
            using (var ms = new MemoryStream(buffer))
            {
                var b = BitConverter.GetBytes(buffer.Length).ToBigEndian();

                ms.Write(b, 0, 4);
                b = BitConverter.GetBytes(magic).ToBigEndian();
                ms.Write(b, 0, 2);
                b = BitConverter.GetBytes(ver).ToBigEndian();
                ms.Write(b, 0, 2);
                b = BitConverter.GetBytes(action).ToBigEndian();
                ms.Write(b, 0, 4);
                b = BitConverter.GetBytes(param).ToBigEndian();
                ms.Write(b, 0, 4);
                if (playload.Length > 0)
                {
                    ms.Write(playload, 0, playload.Length);
                }
                _netStream.WriteAsync(buffer, 0, buffer.Length);
                _netStream.FlushAsync();
            }
        }

        /// <summary>
        /// 获取弹幕服务器地址和端口。
        /// </summary>
        /// <returns>Tuple&lt;string, int>(弹幕服务器地址, 端口)</returns>
        private Task<Tuple<string, int>> GetCmtServer()
        {
            return Task.Run(() => {
                //获取真实弹幕服务器地址。
                InfoLogger.SendInfo(_roomid, "INFO", "开始解析弹幕服务器");

                var chatWc = new WebClient();
                chatWc.Headers.Add("Accept: */*");
                chatWc.Headers.Add("User-Agent: " + Ver.UA);
                chatWc.Headers.Add("Accept-Language: zh-CN,zh;q=0.8,en;q=0.6,ja;q=0.4");

                var chatApi = "https://api.live.bilibili.com/room/v1/Danmu/getConf?room_id=" + _roomid;
                string chatConfString;
                try
                {
                    chatConfString = chatWc.DownloadString(chatApi);
                }
                catch (Exception e)
                {
                    InfoLogger.SendInfo(_roomid, "ERROR", "无法解析弹幕服务器：" + e.Message);
                    throw;
                }

                //解析弹幕信息
                try
                {
                    var chatConf = JObject.Parse(chatConfString);
                    var cmtServerHost = chatConf["data"]["host"].ToString();
                    int.TryParse(chatConf["data"]["port"].ToString(), out int cmtServerPort);
                    InfoLogger.SendInfo(_roomid, "INFO", $"解析弹幕服务器地址成功：{cmtServerHost}:{cmtServerPort}");
                    return new Tuple<string, int>(cmtServerHost, cmtServerPort);
                }
                catch (Exception e)
                {
                    InfoLogger.SendInfo(_roomid, "ERROR", "解析弹幕服务器失败：" + e.Message);
                    throw;
                }
            });
        }

    }
}
