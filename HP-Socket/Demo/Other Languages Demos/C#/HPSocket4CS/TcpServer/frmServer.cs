﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using HPSocketCS;

namespace TcpServer_PFM
{
    public enum AppState
    {
        Starting, Started, Stoping, Stoped, Error
    }

    public partial class frmServer : Form
    {
        private AppState appState = AppState.Stoped;

        private delegate void ShowMsg(string msg);
        private ShowMsg AddMsgDelegate;

        HPSocketCS.TcpServer server = new HPSocketCS.TcpServer();

        private string title = "Echo TcpServer [ 'C' - clear list box ]";
        public frmServer()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {

                this.Text = title;
                // 本机测试没必要改地址,有需求请注释或删除
                this.txtIpAddress.ReadOnly = true;

                // 加个委托显示msg,因为on系列都是在工作线程中调用的,ui不允许直接操作
                AddMsgDelegate = new ShowMsg(AddMsg);


                // 设置回调函数
                server.SetCallback(OnPrepareListen, OnAccept, OnSend, OnReceive, OnClose, OnError, OnServerShutdown);

                SetAppState(AppState.Stoped);
            }
            catch (Exception ex)
            {
                SetAppState(AppState.Error);
                AddMsg(ex.Message);
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                String ip = this.txtIpAddress.Text.Trim();
                ushort port = ushort.Parse(this.txtPort.Text.Trim());

                // 写在这个位置是上面可能会异常
                SetAppState(AppState.Starting);

                // 启动服务
                if (server.Start(ip, port))
                {
                    this.Text = string.Format("{2} - ({0}:{1})", ip, port, title);
                    SetAppState(AppState.Started);
                    throw new Exception(string.Format("$Server Start OK -> ({0}:{1})", ip, port));
                }
                else
                {
                    SetAppState(AppState.Stoped);
                    throw new Exception(string.Format("$Server Start Error -> {0}({1})", server.GetLastErrorDesc(), server.GetlastError()));
                }
            }
            catch (Exception ex)
            {
                AddMsg(ex.Message);
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            SetAppState(AppState.Stoping);

            // 停止服务
            AddMsg("$Server Stop");
            if (server.Stop())
            {
                this.Text = title;
                SetAppState(AppState.Stoped);
            }
            else
            {
                AddMsg(string.Format("$Stop Error -> {0}({1})", server.GetLastErrorDesc(), server.GetlastError()));
            }
        }

        private void btnDisconn_Click(object sender, EventArgs e)
        {
            try
            {
                // 未做64位判断
                IntPtr connId = (IntPtr)Convert.ToInt32(this.txtDisConn.Text.Trim());
                // 断开指定客户
                if (server.Disconnect(connId, true))
                {
                    AddMsg(string.Format("$({0}) Disconnect OK", connId));
                }
                else
                {
                    throw new Exception(string.Format("Disconnect({0}) Error", connId));
                }
            }
            catch (Exception ex)
            {
                AddMsg(ex.Message);
            }
        }


        HandleResult OnPrepareListen(IntPtr soListen)
        {
            // 监听事件到达了,一般没什么用吧?

            return HandleResult.Ok;
        }

        HandleResult OnAccept(IntPtr dwConnId, IntPtr pClient)
        {
            // 客户进入了


            // 获取客户端ip和端口
            string ip = string.Empty;
            ushort port = 0;
            if (server.GetRemoteAddress(dwConnId, ref ip, ref port))
            {
                AddMsg(string.Format(" > [{0},OnAccept] -> PASS({1}:{2})", dwConnId, ip.ToString(), port));
            }
            else
            {
                AddMsg(string.Format(" > [{0},OnAccept] -> Server_GetClientAddress() Error", dwConnId));
            }


            // 设置附加数据
            ClientInfo ci = new ClientInfo();
            ci.ConnId = dwConnId;
            ci.IpAddress = ip;
            ci.Port = port;
            if (server.SetConnectionExtra(dwConnId, ci) == false)
            {
                AddMsg(string.Format(" > [{0},OnAccept] -> SetConnectionExtra fail", dwConnId));
            }

            return HandleResult.Ok;
        }

        HandleResult OnSend(IntPtr dwConnId, IntPtr pData, int iLength)
        {
            // 服务器发数据了


            AddMsg(string.Format(" > [{0},OnSend] -> ({1} bytes)", dwConnId, iLength));

            return HandleResult.Ok;
        }

        HandleResult OnReceive(IntPtr dwConnId, IntPtr pData, int iLength)
        {
            // 数据到达了
            try
            {
                // 从pData中获取字符串
                // string str = Marshal.PtrToStringAnsi(pData, iLength);

                // intptr转byte[]
                // byte[] bytes = new byte[iLength];
                // Marshal.Copy(pData, bytes, 0, iLength);


                // 获取附加数据
                IntPtr clientPtr = IntPtr.Zero;
                if (server.GetConnectionExtra(dwConnId, ref clientPtr))
                {
                    // ci 就是accept里传入的附加数据了
                    ClientInfo ci = (ClientInfo)Marshal.PtrToStructure(clientPtr, typeof(ClientInfo));
                    AddMsg(string.Format(" > [{0},OnReceive] -> {1}:{2} ({3} bytes)", ci.ConnId, ci.IpAddress, ci.Port, iLength));
                }
                else
                {
                    AddMsg(string.Format(" > [{0},OnReceive] -> ({1} bytes)", dwConnId, iLength));
                }

                if (server.Send(dwConnId, pData, iLength))
                {
                    return HandleResult.Ok;
                }

                return HandleResult.Error;
            }
            catch (Exception)
            {

                return HandleResult.Ignore;
            }
        }

        HandleResult OnClose(IntPtr dwConnId)
        {
            // 客户离开了


            // 释放附加数据
            if (server.SetConnectionExtra(dwConnId, null) == false)
            {
                AddMsg(string.Format(" > [{0},OnClose] -> SetConnectionExtra({0}, null) fail", dwConnId));
            }


            AddMsg(string.Format(" > [{0},OnClose]", dwConnId));
            return HandleResult.Ok;
        }

        HandleResult OnError(IntPtr dwConnId, SocketOperation enOperation, int iErrorCode)
        {
            // 客户出错了

            AddMsg(string.Format(" > [{0},OnError] -> OP:{1},CODE:{2}", dwConnId, enOperation, iErrorCode));
            // return HPSocketSdk.HandleResult.Ok;

            // 因为要释放附加数据,所以直接返回OnClose()了
            return OnClose(dwConnId);
        }

        HandleResult OnServerShutdown()
        {
            // 服务关闭了


            AddMsg(" > [OnServerShutdown]");
            return HandleResult.Ok;
        }


        /// <summary>
        /// 设置程序状态
        /// </summary>
        /// <param name="state"></param>
        void SetAppState(AppState state)
        {
            appState = state;
            this.btnStart.Enabled = (appState == AppState.Stoped);
            this.btnStop.Enabled = (appState == AppState.Started);
            this.txtIpAddress.Enabled = (appState == AppState.Stoped);
            this.txtPort.Enabled = (appState == AppState.Stoped);
            this.txtDisConn.Enabled = (appState == AppState.Started);
            this.btnDisconn.Enabled = (appState == AppState.Started && this.txtDisConn.Text.Length > 0);
        }

        /// <summary>
        /// 往listbox加一条项目
        /// </summary>
        /// <param name="msg"></param>
        void AddMsg(string msg)
        {
            if (this.lbxMsg.InvokeRequired)
            {
                // 很帅的调自己
                this.lbxMsg.Invoke(AddMsgDelegate, msg);
            }
            else
            {
                if (this.lbxMsg.Items.Count > 100)
                {
                    this.lbxMsg.Items.RemoveAt(0);
                }
                this.lbxMsg.Items.Add(msg);
                this.lbxMsg.TopIndex = this.lbxMsg.Items.Count - (int)(this.lbxMsg.Height / this.lbxMsg.ItemHeight);
            }
        }

        private void txtDisConn_TextChanged(object sender, EventArgs e)
        {
            // CONNID框被改变事件
            this.btnDisconn.Enabled = (appState == AppState.Started && this.txtDisConn.Text.Length > 0);
        }

        private void lbxMsg_KeyPress(object sender, KeyPressEventArgs e)
        {
            // 清理listbox
            if (e.KeyChar == 'c' || e.KeyChar == 'C')
            {
                this.lbxMsg.Items.Clear();
            }
        }

        private void frmServer_FormClosed(object sender, FormClosedEventArgs e)
        {
            server.Destroy();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class ClientInfo
    {
        public IntPtr ConnId { get; set; }
        public string IpAddress { get; set; }
        public ushort Port { get; set; }
    }
}
