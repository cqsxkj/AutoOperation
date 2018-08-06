/* 功能：阿里木客自动运维服务
 * 时间：2018年5月7日14:50:20
 * 
 * 
 * 
 * */
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace AutoOperationService
{
    public partial class Service1 : ServiceBase
    {
        private bool isRun = false;
        //创建连接的Socket
        Socket socketSend;
        //XmlFiles updaterXmlFiles = null;
        //创建接收客户端发送消息的线程
        Thread threadReceive;
        int BufferSize = 2048;
        string SERVERIP = null;
        public Service1()
        {
            SERVERIP = System.Configuration.ConfigurationSettings.AppSettings["UpServer"];
            InitializeComponent();
            Log.log.WriteTxt("阿里木客自动运维服务：【服务线程启动】");
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Elapsed += new System.Timers.ElapsedEventHandler(TimedEvent);
            timer.Interval = 5000;//每5秒检测重连
            timer.Enabled = true;
            Thread thread = new Thread(WatchDog2MC);
            thread.IsBackground = true;
            thread.Start();
        }
        //定时执行事件
        private void TimedEvent(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (!isRun)
                {
                    CreateConnect(SERVERIP, 1991);
                }
                else
                {
                    SendMesg(string.Format("<SX>{0}<SX>", "heartbeat"));
                }
            }
            catch (Exception ex)
            {
                isRun = false;
                DisposeConnect();
                SendMesg("定时异常:" + ex);
            }
        }

        protected override void OnStart(string[] args)
        {
            //Log.log.WriteTxt("阿里木客自动运维服务：【服务线程启动】");
            //System.Timers.Timer timer = new System.Timers.Timer();
            //timer.Elapsed += new System.Timers.ElapsedEventHandler(TimedEvent);
            //timer.Interval = 5000;//每5秒检测重连
            //timer.Enabled = true;
            //Thread thread = new Thread(WatchDog2MC);
            //thread.IsBackground = true;
            //thread.Start();
        }

        private void WatchDog2MC()
        {
            try
            {
                if (!CheckExeExists("SXMS.SCADA.Manager.exe"))
                {
                    Log.log.WriteTxt("监视发现M端已退出");
                    RestartM();
                }
                if (!CheckExeExists("SXMS.SCADA.Collector.exe"))
                {
                    Log.log.WriteTxt("监视发现C端已退出");
                    RestartC();
                }
                Thread.Sleep(30000);
                WatchDog2MC();
            }
            catch (Exception e)
            {
                Log.log.WriteTxt("监视线程");
            }
        }
        /// <summary>
        /// 应用程序是否存在
        /// </summary>
        /// <param name="appName">程序名称</param>
        /// <returns>应用程序是否在运行</returns>
        private static bool CheckExeExists(string appName)
        {
            Process[] allProcess = Process.GetProcesses();
            foreach (Process p in allProcess)
            {
                if (p.ProcessName.ToLower() + ".exe" == appName.ToLower())
                {
                    return true;
                }
            }
            return false;
        }

        protected override void OnStop()
        {
            Log.log.WriteTxt("阿里木客自动运维服务：【服务停止】");
        }

        protected override void OnShutdown()
        {
            Log.log.WriteTxt("阿里木客自动运维服务提示：【计算机关闭】");
            base.OnShutdown();
        }

        /// <summary>
        /// 连接
        /// </summary>
        private void CreateConnect(string ip, int port)
        {
            try
            {
                socketSend = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress ipadd = IPAddress.Parse(ip);
                IAsyncResult connResult = socketSend.BeginConnect(ipadd, port, null, null);
                connResult.AsyncWaitHandle.WaitOne(3000, true);
                //socketSend.Connect(ipadd, port);
                if (connResult.IsCompleted)
                {
                    isRun = true;
                    //Log.log.WriteTxt("连接服务端");
                    //开启一个新的线程不停的接收服务器发送消息的线程
                    threadReceive = new Thread(new ThreadStart(Receive));
                    //设置为后台线程
                    threadReceive.IsBackground = true;
                    threadReceive.Start();
                }
                else
                {
                    //Log.log.WriteTxt("连接超时");
                }
            }
            catch (Exception)
            {
                return;
            }
        }

        /// <summary>
        /// 接收服务器发送的消息
        /// </summary>
        private void Receive()
        {
            try
            {
                while (true)
                {
                    byte[] buffer = new byte[BufferSize];
                    //实际接收到的字节数
                    //WriteLog("receive mesg");
                    int r = socketSend.Receive(buffer);
                    if (r == 0)
                    {
                        DisposeConnect();
                        break;
                    }
                    else
                    {
                        string command = Encoding.Default.GetString(buffer, 0, r);
                        Log.log.WriteTxt(command);
                        SendMesg("client:" + command);
                        AnalysiCommand(command);
                    }
                }
            }
            catch (Exception)
            {
                DisposeConnect();
            }
        }
        /// <summary>
        /// 解析上级命令并执行
        /// </summary>
        /// <param name="json"></param>
        private void AnalysiCommand(string json)
        {
            try
            {
                switch (json.Trim().ToUpper())
                {
                    case "STARTAUTOUPDATE"://启动自动更新
                        //StartUpdateEXE();
                        break;
                    case "STARTCONSOLEUPDATER":
                        StartConsoleExe();
                        break;
                    case "RESTART C"://重启C
                        RestartC();
                        break;
                    case "RESTART M"://重启M
                        RestartM();
                        break;
                    case "GETVERSION"://获取版本信息
                        GetVersion();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                Log.log.WriteTxt("命令执行错误！" + e.Message);
            }
        }
        private void GetVersion()
        {
            string filePath = AppDomain.CurrentDomain.BaseDirectory + "\\UpdateList.xml";
            // 用文件流打开用户要发送的文件；    
            using (FileStream fs = new FileStream(filePath, FileMode.Open))
            {
                //string fileName = System.IO.Path.GetFileName(filePath);
                //string fileExtension = System.IO.Path.GetExtension(filePath);
                //string strMsg = "我给你发送的文件为： " + fileName + fileExtension + "\r\n";
                //byte[] arrMsg = System.Text.Encoding.UTF8.GetBytes(strMsg); // 将要发送的字符串转换成Utf-8字节数组；    
                //byte[] arrSendMsg = new byte[arrMsg.Length + 1];
                //arrSendMsg[0] = 0; // 表示发送的是消息数据    
                //Buffer.BlockCopy(arrMsg, 0, arrSendMsg, 1, arrMsg.Length);
                byte[] arrFile = new byte[1024 * 1024 * 2];
                int length = fs.Read(arrFile, 0, arrFile.Length);  // 将文件中的数据读到arrFile数组中；    
                byte[] arrFileSend = new byte[length];
                //arrFileSend[0] = 1; // 用来表示发送的是文件数据；    
                Buffer.BlockCopy(arrFile, 0, arrFileSend, 0, length);
                // 还有一个 CopyTo的方法，但是在这里不适合； 当然还可以用for循环自己转化；    
                //  sockClient.Send(arrFileSend);// 发送数据到服务端；    
                socketSend.Send(arrFileSend);// 解决了 sokConnection是局部变量，不能再本函数中引用的问题；   

            }
        }
        /// <summary>
        /// 重启C端
        /// </summary>
        private void RestartC()
        {
            try
            {
                CloseCurrentProgram("SXMS.SCADA.Collector.exe");
                Process.Start(AppDomain.CurrentDomain.BaseDirectory + "C\\" + "SXMS.SCADA.Collector.exe");
                Log.log.WriteTxt("重启：" + AppDomain.CurrentDomain.BaseDirectory + "C\\" + "SXMS.SCADA.Collector.exe");
                SendMesg("已重启： C");
            }
            catch (Exception e)
            {
                SendMesg("重启C端异常" + e.Message);
            }
        }
        /// <summary>
        /// 重启M端
        /// </summary>
        private void RestartM()
        {
            try
            {
                CloseCurrentProgram("SXMS.SCADA.Manager.exe");
                Process.Start(AppDomain.CurrentDomain.BaseDirectory + "M\\" + "SXMS.SCADA.Manager.exe");
                SendMesg("已重启：M");
            }
            catch (Exception e)
            {
                SendMesg("重启M端异常" + e.Message);
            }
        }
        /// <summary>
        /// 关闭正在运行的程序
        /// </summary>
        /// <param name="appName">程序名称以 .exe结尾</param>
        private void CloseCurrentProgram(string appName)
        {
            Process[] allProcess = Process.GetProcesses();
            foreach (Process p in allProcess)
            {
                if (p.ProcessName.ToLower() + ".exe" == appName.ToLower())
                {
                    for (int i = 0; i < p.Threads.Count; i++)
                        p.Threads[i].Dispose();
                    p.Kill();
                }
            }
        }
        /// <summary>
        /// 启动控制台更新程序
        /// </summary>
        private void StartConsoleExe()
        {
            try
            {
                CloseCurrentProgram("ConsoleUpdater.exe");
                Process.Start(AppDomain.CurrentDomain.BaseDirectory + "ConsoleUpdater.exe");
                SendMesg("已启动：ConsoleUpdater.exe");
                Thread thread = new Thread(ConsoleUpdaterCheck);
                thread.IsBackground = true;
                thread.Start();
            }
            catch (Exception e)
            {
                Log.log.WriteTxt("启动更新程序失败！" + e);
                return;
            }
        }
        /// <summary>
        /// 检测升级是否完成
        /// </summary>
        private void ConsoleUpdaterCheck()
        {
            while (true)
            {
                if (!CheckExeExists("ConsoleUpdater.exe"))
                {
                    SendMesg("更新程序运行结束");
                    break;
                }
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// 启动更新程序，程序名称AutoUpdate/AutoUpdate.exe
        /// </summary>
        private static void StartUpdateEXE()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = (AppDomain.CurrentDomain.BaseDirectory + "AutoUpdate/AutoUpdate.exe");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = false;
                Process.Start(psi);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 客户端给服务器发送消息
        /// </summary>
        /// <param name="mesg"></param>
        private void SendMesg(string mesg)
        {
            try
            {
                int receive = socketSend.Send(Encoding.Default.GetBytes(mesg));
            }
            catch (Exception)
            {
                DisposeConnect();
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        private void DisposeConnect()
        {
            try
            {
                //关闭socket
                socketSend.Close();
                isRun = false;
                //终止线程
                //threadReceive.Abort();
            }
            catch (Exception e)
            {
                //Log.log.WriteTxt(e.Message);
            }
        }
    }
}
