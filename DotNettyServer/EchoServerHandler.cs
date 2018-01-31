﻿using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNettyServer
{
    public class EchoServerHandler : ChannelHandlerAdapter //管道处理基类，较常用
    {
        //  重写基类的方法，当消息到达时触发，这里收到消息后，在控制台输出收到的内容，并原样返回了客户端
        public override void ChannelRead(IChannelHandlerContext context, object message)
        {

            var buffer = message as IByteBuffer;
            if (buffer != null)
            {
                Console.WriteLine("接收到客户端消息:" + buffer.ToString(Encoding.UTF8));
            }
            context.WriteAsync(message);//写入输出流
        }

        // 输出到客户端，也可以在上面的方法中直接调用WriteAndFlushAsync方法直接输出
        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        //捕获 异常，并输出到控制台后断开链接，提示：客户端意外断开链接，也会触发
        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine("服务器异常:" + exception);
            context.CloseAsync();
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            Console.WriteLine("激活" + context.Channel.LocalAddress.ToString());
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            Console.WriteLine("断开连接" + context.Channel.LocalAddress.ToString());
        }
    }
}