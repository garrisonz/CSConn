﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace socketClient_win {
    public partial class Form1 : Form {

        private Socket_Cli cliS = new Socket_Cli();               //客户端的Socket
        private Socket_Listen lisS = new Socket_Listen();         //监听的Socket

        public Socket_aliveL alive_list = new Socket_aliveL();       //Socket列表

        public Form1() {
            InitializeComponent();
        }

        private void textBox1_TextChanged(object sender, EventArgs e) {

        }

        /***************************************************************  发送信息 ***************/
        private void btn_send_Click(object sender, EventArgs e) {
            String msg = this.getMsgContent();
            if (msg.Length == 0) {
                appendToHistory("请输入内容\n");
                return;
            }

            List<String> target = this.getSendTarget();
            List<string> failed = alive_list.sendMsg2SockeList(msg, target);
            foreach (string error in failed) {
                appendToHistory("sendCli异常：" + error);
            }

            //发消息给服务端
            try {
                MsgData md = new MsgData();
                md.msg = msg;
                cliS.send2Server(md);
            }
            catch (Exception ex) {
                appendToHistory("sendServer异常:" + ex.Message);
                if (cliS.so == null) {
                    return;
                }
                try {
                    cliS.closeSocket();
                }
                catch (Exception excep) {
                    appendToHistory("close异常" + excep.Message);
                }
            }

            appendToHistory("本机:\n" + msg + "\n");
            tb_msg.Text = "";
        }


        /*************************************************************** END  发送信息 ***********/

        /**
         * 启动监听 -- 线程
         */
        public void startListen_thread() {

            //正常 - 应用Dns.GetHostAddresses 获得本地 ip
            IPAddress localIp = IPAddress.Parse("127.0.0.1");
            int localPort = cliS.getLocalPort();
            lisS.startListen(localIp, localPort);

            Thread newThread = new Thread(lisS.listenConn);
            newThread.IsBackground = true;
            newThread.Start();
        }

        /********************************************************** 界面的操作 ********/

        /**
        * 获得发送目标
        */
        public List<String> getSendTarget() {
            List<String> sendTarget = new List<String>();
            for (int i = 0; i < checked_lb_online.Items.Count; i++) {
                if (checked_lb_online.GetItemChecked(i) == false) {
                    continue;
                }
                String clientInfo = checked_lb_online.GetItemText(checked_lb_online.Items[i]);
                String[] info = clientInfo.Split('.');

                //此处 +2 是魔术数字，要根据现实界面而更改
                String ipAndPort = clientInfo.Substring(info[0].Length + 2);

                sendTarget.Add(ipAndPort);
            }
            return sendTarget;
        }

        private void btn_conn_Click(object sender, EventArgs e) {
            IPEndPoint ipPoint = this.getServerAdd();
             
            if (cliS.isSocketConnected()) {
                tb_history.AppendText("已连接 \n");
                return;
            }

            try {
                cliS.conn(ipPoint);
                appendToHistory("服务器连接成功. 这里是"+cliS.so.LocalEndPoint.ToString()+"\n");

                //开新线程 - 接受信息
                Thread newThread = new Thread(ReceiveServerMsg_thread);
                newThread.IsBackground = true;
                newThread.Start();

                lisS.f1 = this;

                Thread listenThread = new Thread(startListen_thread);
                listenThread.IsBackground = true;
                listenThread.Start();
            }
            catch (Exception ex) {
                appendToHistory("连接失败\n失败原因：" + ex.Message.ToString());
            }
        }

        /**
         * 接受服务端消息的线程
         */
        public void ReceiveServerMsg_thread() {
            while (true) {
                try {
                    MsgData md = cliS.receiveServerMsgData();
                    if (md.cliList.Length > 0) {
                        showOnline(md.cliList);
                    }
                    if (md.msg.Length > 0) {
                        appendToHistory("服务端：\n" + md.msg + "\n");
                    }
                }
                catch (Exception ex) {
                    if (this.IsDisposed)
                        return;
                    appendToHistory("服务端 - Receive异常：\n" + ex.Message + "\n");

                    cliS.closeSocket();
                    break;
                }
            }
        }

        /**
        * 获得服务器地址
        */
        public IPEndPoint getServerAdd() {
            String server_ip = tb_server_id.Text;
            String portStr = tb_port.Text;
            int port = int.Parse(portStr);

            IPAddress ip = IPAddress.Parse(server_ip);
            IPEndPoint ipPort = new IPEndPoint(ip, port);

            return ipPort;
        }

        /**
        * 获得发送内容
        */
        public String getMsgContent() {
            String msg = tb_msg.Text;

            return msg;
        }

        /********************************************************** 和界面的操作 ********/


        /*********************************************************** UI修改函数 *******/
        /*********************************************************** 下面一般无关联 *******/

        /**
         * 显示在线客户端
         */
        public void showOnline(String cliListStr) {
            clearOnlieList_lb();
            String[] cliArr = cliListStr.Split('&');
            for (int i = 0; i < cliArr.Length; i++) {
                String cliInfo = i + ". " + cliArr[i];
                if (cliArr[i] == cliS.so.LocalEndPoint.ToString()) {
                    continue;
                }

                addToOnlieList_lb(cliInfo);
            }
        }

        private delegate void addListInvokeCallback(String msg);
        /**
         * 增加客户列表 ListBox
         */
        public void addToOnlieList_lb(String msg) {
            if (checked_lb_online.InvokeRequired) {
                addListInvokeCallback callback = new addListInvokeCallback(addToOnlieList_lb);
                checked_lb_online.Invoke(callback, msg);
            }
            else {
                checked_lb_online.Items.Add(msg);
            }
        }

        private delegate void listInvokeCallback();
        /**
         * 清空客户端列表 ListBox
         */
        public void clearOnlieList_lb() {
            if (checked_lb_online.InvokeRequired) {
                listInvokeCallback callback = new listInvokeCallback(clearOnlieList_lb);
                checked_lb_online.Invoke(callback);
            }
            else {
                checked_lb_online.Items.Clear();
            }
        }

        private delegate void InvokeCallback(String msg);
        /**
         * 给历史记录增加文字
         */
        public void appendToHistory(String msg) {
            if (tb_history.InvokeRequired) {
                InvokeCallback callback = new InvokeCallback(appendToHistory);
                tb_history.Invoke(callback, msg);
            }
            else {
                if (tb_history == null ) {
                    return;
                }

                tb_history.AppendText(msg);
            }
        }

        /*********************************************************** UI修改函数 *******/

        private void button1_Click(object sender, EventArgs e){
        }


        private void Form1_FormClosed(object sender, FormClosedEventArgs e) {
            this.closeAllRes();
        }

        /**
         * 关闭所有资源
         */
        public void closeAllRes() {
            cliS.closeResourse();
        }

        private void btn_file_Click(object sender, EventArgs e) {
            String ipAndPort = this.getOneTarget();
            if (ipAndPort.Length == 0)
                return;

            String fileFullPath = FileTranser.getFileName();
            if (fileFullPath.Length == 0)
                return;

            translateFile_simple(ipAndPort, fileFullPath);
        }

        /**
         * 获得一个发送对象
         */
        public String getOneTarget() {
            List<string> TargetList = this.getSendTarget();
            if (TargetList.Count == 0) {
                appendToHistory("发送文件 先选择发送目标");
                return "";
            }
            if (TargetList.Count > 1) {
                appendToHistory("发送文件 一次只能发送一个目标");
                return "";
            }
            String ipAndPort = TargetList[0];

            return ipAndPort;
        }


        /**
         * 传文件
         */
        public void translateFile_simple(String ipAndPort, String fileFullPath) {
            string filename = System.IO.Path.GetFileName(fileFullPath);
            System.IO.FileInfo fileInfo = new System.IO.FileInfo(fileFullPath);
            long fileSize = fileInfo.Length;

            MsgData md = new MsgData();
            md.type = "FILE";
            md.fileName = filename;
            md.fileSize = fileSize;
            md.msg = FileTranser.getFileContent(fileFullPath);
            String mdString = MsgData.SerializeMsg(md);

            String error = alive_list.sendMsg(mdString, ipAndPort);
            if (error.Length > 0) {
                appendToHistory("文件传输异常："+error + "\n");
                return;
            }

        }

        private void button1_Click_1(object sender, EventArgs e) {
            string folderPath = FileTranser.getFolderPath();
            tb_msg.Text = folderPath;
        }


    }
}
