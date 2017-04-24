using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace GuestBook2.Controllers
{
    public class HttpTestController:System.Web.Mvc.Controller
    {
        private readonly String IP = "127.0.0.1";
        private readonly int port = 16000;
        public static Socket mSocket=new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        public static Socket mClientSocket,s;
        public static Thread tServer;
        public static Thread[] tReceiver = new Thread[2];
        public static Socket[] mClientSockets = new Socket[2];
        public static bool[] isUsing = new bool[2];
        public static List<byte[]> bytes = new List<byte[]>();
        public static byte[] buffer = new byte[6];          //公共资源区
        public static bool mExit=false;
        public static bool locked=false;
        public static bool first_time = true;
        public static int count = 0;
        public const byte NODEROTATE_LEFT = 0x01;           //正转
        public const byte NODEROTATE_RIGHT = 0x02;          //反转
        public const byte NODEFOWARD = 0x03;                //加速
        public const byte NODEBACKWARD = 0x04;              //减速
        public const byte NODESTOP = 0x00;                  //停止



        public ActionResult Index()
        {
            List<byte[]> b = new List<byte[]>(bytes);
            return View(b);
        }


        public void startserver(/*Object o*/)
        {
            init();
            IPAddress ip = IPAddress.Parse(IP);
            IPEndPoint endpoint = new IPEndPoint(ip, port);
            mExit = false;
            try
            {
                if (first_time)
                {
                    System.Diagnostics.Debug.Write("3");
                    //mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    mSocket.Bind(endpoint);
                    System.Diagnostics.Debug.Write("4");
                    mSocket.Listen(1);
                    first_time = false;
                }

                while (!mExit)
                {
                    mSocket.BeginAccept(new AsyncCallback(AcceptCallBack),mSocket);                //异步监听
                    System.Diagnostics.Debug.Write("5");
                   //mClientSocket.Send(new byte[] {0x01,0x02});
                   // s = new Socket(mClientSocket);
                    tReceiver[count%2] = new Thread(new ParameterizedThreadStart(receive));
                    tReceiver[count%2].Start(count%2);
                    count++;
                    Thread.Sleep(30);
                }
                shutSocket();

            }
            catch (Exception e)
            {
                Console.Write(e.StackTrace);
            }

          
        }

        private void receive(Object o)
        {
            int index=int.Parse(o.ToString());
            while(!mExit){
                byte[] buf = new byte[bytes[index].Length];
                bytes[index].CopyTo(buf,0);
                try
                {
                    while (locked) ;
                    locked = true;
                    //mClientSocket.BeginReceive(bytes, 0, 6, SocketFlags.None,new AsyncCallback(ReceiveCallBack),mClientSocket);
                    if (isUsing[index])
                    {
                        mClientSockets[index].BeginReceive(buffer, 0, 6, SocketFlags.None, new AsyncCallback(ReceiveCallBack), mClientSockets[index]);
                        buffer.CopyTo(bytes[index],0);
                    }
                    locked = false;
                    Thread.Sleep(30);
                }
                catch (SocketException e)
                {
                    shutSocket();
                }
                catch (ObjectDisposedException e)
                {
                    shutSocket();
                }
                if (bytes[index][0] != 0xff)
                    buf.CopyTo(bytes[index], 0);
                Thread.Sleep(30);
                System.Diagnostics.Debug.Write(bytes[5]);
                System.Diagnostics.Debug.Write(buf);
                //Refresh();
            }

        }

        public void sendData(byte data,int node)
        {
            Byte[] buf = new Byte[3];
            buf[0] = 0xff;
            buf[1] = Byte.Parse(getHex(node));
            buf[2] = data;
            //mClientSocket.Send(buf);
            mClientSockets[node % 2].Send(buf);
            //mSocket.Send(buf);
        }

        static void ReceiveCallBack(IAsyncResult result)
        {
            if (mExit)
                return;
            Socket temp = (Socket)result.AsyncState;
            temp.EndReceive(result);
            result.AsyncWaitHandle.Close();
            temp.BeginReceive(buffer, 0, 6, SocketFlags.None, new AsyncCallback(ReceiveCallBack),temp);
        }

        static void AcceptCallBack(IAsyncResult result)
        {
            if (mExit)
                return;
            Socket server_temp = (Socket)result;
            mClientSockets[count % 2] = server_temp.EndAccept(result);
            server_temp.BeginAccept(new AsyncCallback(AcceptCallBack), server_temp);
        }

        private void shutSocket()
        {
            mExit = true;
            if(isUsing[0])
                mClientSockets[0].Close();
            if(isUsing[1])
                mClientSockets[1].Close();
            isUsing[0] = isUsing[1] = false;
        }

        private String getHex(int x)
        {
            String result = "";
            int low = x & (0x0f);
            int high = x & (0xf0);
            if (high >= 10)
            {
                result += ('A' + high - 10);
            }
            if (low >= 10)
            {
                result += ('A' + low - 10);
            }
            return result;
        }

        private void init()
        {
            byte[] b = new byte[6] { 0x61, 0x61, 0x61, 0x61, 0x61, 0x61};
            bytes.Add(b);
            bytes.Add(b);
        }


        public JsonResult getD()
        {
            var condition = new
            {
                node = bytes[0][1],
                temperature = bytes[0][2],
                wet = bytes[0][3],
                light = bytes[0][4],
                rainy = bytes[0][5],
                node1=bytes[1][1],
                temperature1 = bytes[0][2],
                wet1 = bytes[0][3],
                light1 = bytes[0][4],
                rainy1 = bytes[0][5]
            };
            return Json(condition);
        }

        public ActionResult Forward(int node)
        {
            sendData(NODEFOWARD,node);
            return RedirectToAction("Index");
        }

        public ActionResult BackWard(int node)
        {
            sendData(NODEBACKWARD,node);
            return RedirectToAction("Index");
        }

        public ActionResult Rotate_Right(int node)
        {
            sendData( NODEROTATE_LEFT,node);
            return RedirectToAction("Index");
        }

        public ActionResult Rotate_Left(int node)
        {
            sendData(NODEROTATE_RIGHT,node);
            return RedirectToAction("Index");
        }

        public ActionResult STOP(int node)
        {
            sendData(NODESTOP,node);
            return RedirectToAction("Index");
        }

        public ActionResult StartUp()
        {
//            if (tServer == null)
//            {
//                tServer = new Thread(new ParameterizedThreadStart(startserver));
//                tServer.Start();
//           }
            startserver();
            return RedirectToAction("Index");
        }

        public ActionResult ShutDown()
        {
            while (locked) ;
            if (!mExit)
                shutSocket();
            return RedirectToAction("Index");
        }


    }
}
