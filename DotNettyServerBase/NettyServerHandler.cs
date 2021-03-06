﻿using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using DotNettyCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DotNettyServerBase
{
    public class NettyServerHandler : ChannelHandlerAdapter //管道处理基类，较常用
    {
        private static ConcurrentDictionary<string, Link> _channels = new ConcurrentDictionary<string, Link>();

        /// <summary>
        /// 处理接收到消息
        /// </summary>
        /// <param name="context"></param>
        /// <param name="message"></param>
        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            Message response = new Message();
            try
            {
                var buffer = message as IByteBuffer;
                if (buffer != null)
                {
                    LogHelper.Info("接收到客户端" + context.Channel.RemoteAddress.ToString() + "消息:" + buffer.ToString(Encoding.UTF8));
                    Message request = JsonHelper.JsonDeserialize<Message>(buffer.ToString(Encoding.UTF8));
                    if (request != null)
                    {
                        response.type = request.type;
                        response.from = request.to;
                        response.to = request.from;
                        Link oldLink = null;

                        if (request.type == MessageType.CONNECT.ToString())
                        {
                            response.msg = "注册成功！";
                            response.state = true;
                            Link newLink = new Link();
                            newLink.channel = context;
                            newLink.username = request.from;
                            if (_channels.TryGetValue(context.Channel.RemoteAddress.ToString(), out oldLink))
                            {
                                _channels.TryUpdate(context.Channel.RemoteAddress.ToString(), newLink, oldLink);
                            }
                            else
                            {
                                _channels.TryAdd(context.Channel.RemoteAddress.ToString(), newLink);
                            }
                            LogHelper.Info("建立连接：" + context.Channel.RemoteAddress.ToString() + "   " + newLink.username);
                        }
                        else if (request.type == MessageType.LIST.ToString())
                        {
                            List<string> users = new List<string>();
                            foreach (string key in _channels.Keys)
                            {
                                if (_channels.TryGetValue(key, out oldLink) && !string.IsNullOrWhiteSpace(oldLink.username))
                                {
                                    users.Add(oldLink.username);
                                }
                            }
                            response.msg = JsonHelper.JsonSerialize(users);
                            response.state = true;
                        }
                        else if (request.type == MessageType.EMIT.ToString())
                        {
                            response.from = request.from;
                            response.to = request.to;
                            if (!string.IsNullOrWhiteSpace(request.to))
                            {
                                if (request.to == CommonHelper.SYS)
                                {

                                }
                                else
                                {
                                    bool hasUser = false;
                                    foreach (string key in _channels.Keys)
                                    {
                                        if (_channels.TryGetValue(key, out oldLink) && oldLink.username == request.to)
                                        {
                                            context = oldLink.channel;
                                            response.msg = request.msg;
                                            response.state = true;
                                            hasUser = true;
                                            break;
                                        }
                                    }
                                    if (!hasUser)
                                    {
                                        response.msg = "当前用户不存在或不在线！";
                                        response.state = false;
                                    }
                                }
                            }
                        }
                        else
                        {
                            response = new Message
                            {
                                type = request.type,
                                from = CommonHelper.SYS,
                                to = request.from,
                                state = false,
                                msg = "不支持的请求类型!"
                            };
                        }
                    }
                    else
                    {
                        response = new Message
                        {
                            from = CommonHelper.SYS,
                            state = false,
                            msg = "非法的请求格式!"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                response = new Message
                {
                    from = CommonHelper.SYS,
                    state = false,
                    msg = ex.Message
                };
                LogHelper.Error("指令处理出错：" + ex);
            }
            byte[] messageBytes = Encoding.UTF8.GetBytes(JsonHelper.JsonSerialize(response));
            //缓存区
            IByteBuffer initialMessage = Unpooled.Buffer(1024);
            initialMessage.WriteBytes(messageBytes);
            context.WriteAndFlushAsync(initialMessage);    // 回写输出流
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            if (exception is SocketException && ((SocketException)exception).ErrorCode == 10054)
            {
                LogHelper.Info(context.Channel.RemoteAddress.ToString() + "套接字异常:" + exception.Message);
            }
            else
            {
                LogHelper.Error(context.Channel.RemoteAddress.ToString() + "服务器异常:" + exception);
                NettyServerHelper.init().Wait();
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            Link link = new Link();
            _channels.TryRemove(context.Channel.RemoteAddress.ToString(), out link);
            LogHelper.Info("断开连接：" + context.Channel.RemoteAddress.ToString() + "   " + (link == null ? "" : link.username));
        }
    }
}
