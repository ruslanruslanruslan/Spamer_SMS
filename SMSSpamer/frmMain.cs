﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMSSpamer
{
  public partial class frmMain : Form
  {
    public frmMain()
    {
      InitializeComponent();
    }

    ModemLogic modemLogic = new ModemLogic();
    bool bStop = false;
    bool bStopped = true;

    private void AddLog(string msg, Color msgColor)
    {
      int start = rtbLog.Text.Length - 1;
      if (start < 0)
        start = 0;
      rtbLog.AppendText(DateTime.Now.ToShortTimeString() + " | " + msg + Environment.NewLine);
      rtbLog.Select(start, rtbLog.Text.Length - start + 1);
      rtbLog.SelectionColor = msgColor;
      rtbLog.SelectionStart = rtbLog.Text.Length;
      rtbLog.ScrollToCaret();
    }

    private bool TryToConnectToModem(string strPortName)
    {
      try
      {
        modemLogic.ClosePort();
      }
      catch (Exception ex)
      {
        AddLog(ex.Message, LogMessageColor.Error());
      }
      try
      {
        modemLogic.OpenPort(strPortName);
        if (modemLogic.ConnectedPort != null)
        {
          AddLog("Device successfully connected", LogMessageColor.Success());
          return true;
        }
      }
      catch (Exception ex)
      {
        AddLog(ex.Message, LogMessageColor.Error());
      }
      return false;
    }

    private void LoadPortNames()
    {
      try
      {
        string[] ports = modemLogic.LoadPorts();
        foreach (string port in ports)
        {
          cbModem.Items.Add(port);
        }
      }
      catch (Exception ex)
      {
        AddLog(ex.Message, LogMessageColor.Error());
      }
      try
      {
        if (cbModem.Items.Count > 0)
        {
          cbModem.SelectedIndex = 0;
        }
        else
        {
          AddLog("No avalable devices", LogMessageColor.Error());
        }
      }
      catch (Exception ex)
      {
        AddLog(ex.Message, LogMessageColor.Error());
      }
    }

    private void frmMain_Load(object sender, EventArgs e)
    {
      string[] args = Environment.GetCommandLineArgs();
      btnSend.Enabled = false;
      cbModem.SelectedIndexChanged -= cbModem_SelectedIndexChanged;
      LoadPortNames();
      if (cbModem.Items.Count > 0)
      {
        if (cbModem.Items.Contains(Properties.Default.ModemName))
        {
          cbModem.SelectedIndex = cbModem.Items.IndexOf(Properties.Default.ModemName);
        }
      }
      edtPhoneNumber.Text = Properties.Default.PhoneNumber;
      edtMessage.Text = Properties.Default.Message;
      if (TryToConnectToModem(modemLogic.GetPortNameByIndex(cbModem.SelectedIndex)))
      {
        btnSend.Enabled = true;
      }
      cbModem.SelectedIndexChanged += cbModem_SelectedIndexChanged;
      if (args.Count() > 1)
      {
        cbModem.Enabled = false;
        edtPhoneNumber.Enabled = false;
        edtMessage.Enabled = false;
        btnSend.Enabled = false;
        btnSettings.Enabled = false;
        btnSendFromDB.Enabled = false;
        if (args[1] == "--send-sms")
        {
          Text += " 'Send single SMS mode'";
          AddLog("Send single SMS mode", LogMessageColor.Information());
          Task.Factory.StartNew(() =>
            {
              TryAutoSendMessage(args[2], args[3]);
            }
          );
        }
        else if (args[1] == "--send-sms-from-db")
        {
          Text += " 'Send SMS from DB mode'";
          AddLog("Send SMS from DB mode", LogMessageColor.Information());
          string server = "", login = "", password = "", database = "";
          int port = 3306;
          for (int i = 2; i < args.Count(); i += 2)
          {
            if (args[i] == "-server")
            {
              server = args[i + 1];
            }
            else if (args[i] == "-database")
            {
              database = args[i + 1];
            }
            else if (args[i] == "-login")
            {
              login = args[i + 1];
            }
            else if (args[i] == "-password")
            {
              password = args[i + 1];
            }
            else if (args[i] == "-port")
            {
              port = Convert.ToInt32(args[i + 1]);
            }
          }
          MySqlDB db = new MySqlDB(login, password, server, port, database);
          try
          {
            var testConnection = db.mySqlConnection;
          }
          catch (Exception ex)
          {
            MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Program.Exit(1);
          }
          Task.Factory.StartNew(() =>
            {
              SendMessageFromDatabase(db);
              db.Close();
            }
          );
        }
      }
      else
      {
        if (Properties.Default.MySqlServerAddress.Length == 0 ||
            Properties.Default.MySqlServerDatabase.Length == 0 ||
            Properties.Default.MySqlServerPassword.Length == 0 ||
            Properties.Default.MySqlServerPort == 0 ||
            Properties.Default.MySqlServerUsername.Length == 0)
        {
          frmSettings frm = new frmSettings();
          frm.ShowDialog();
        }
      }
    }
    private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
    {
      modemLogic.ClosePort();
      Properties.Default.ModemName = cbModem.Text;
      Properties.Default.PhoneNumber = edtPhoneNumber.Text;
      Properties.Default.Message = edtMessage.Text;
      Properties.Default.Save();
    }

    private void cbModem_SelectedIndexChanged(object sender, EventArgs e)
    {
      btnSend.Enabled = false;
      TryToConnectToModem(modemLogic.GetPortNameByIndex(cbModem.SelectedIndex));
      btnSend.Enabled = true;
    }

    private void btnSend_Click(object sender, EventArgs e)
    {
      if (SendMessage(edtPhoneNumber.Text, edtMessage.Text))
      {
        AddLog("Message '" + edtMessage.Text + "' successfully sent to '" + edtPhoneNumber.Text + "'", LogMessageColor.Success());
      }
      else
      {
        AddLog("Can't send message '" + edtMessage.Text + "' to '" + edtPhoneNumber.Text, LogMessageColor.Error());
      }
    }

    private bool SendMessage(string PhoneNo, string Message)
    {
      try
      {
        AddLog("Sending message '" + Message + "' to '" + PhoneNo, LogMessageColor.Information());
        return modemLogic.SendMessage(PhoneNo, Message);
      }
      catch (Exception ex)
      {
        AddLog("Can't send message '" + Message + "' to '" + PhoneNo + "': " + ex.Message, LogMessageColor.Error());
      }
      return false;
    }

    private void TryAutoSendMessage(string Phone, string Message)
    {
      if (!SendMessage(Phone, Message))
      {
        foreach (string port in modemLogic.Ports)
        {
          modemLogic.OpenPort(port);
          if (SendMessage(Phone, Message))
          {
            Program.Exit(0);
            return;
          }
        }
      }
      Program.Exit(1);
    }

    private void SendMessageFromDatabase(MySqlDB db)
    {
      while (true)
      {
        if (bStop)
          return;
        try
        {
          var messages = db.GetMessagePacket();
          int Sent = 0;
          foreach (var message in messages)
          {
            if (bStop)
              return;
            try
            {
              if (SendMessage(message.number, message.message))
              {
                AddLog("Success", LogMessageColor.Success());
                try
                {
                  db.SetMessageSent(message.id);
                  Sent++;
                  AddLog("Mark [" + message.id + "] as sent", LogMessageColor.Information());
                }
                catch (Exception ex)
                {
                  AddLog("Can't mark [" + message.id + "] as sent: '" + ex.Message, LogMessageColor.Error());
                }
              }
            }
            catch (Exception ex)
            {
              AddLog("Can't send message '" + message.message + "' to '" + message.number + "': " + ex.Message, LogMessageColor.Error());
            }            
          }
          if (Sent == 0)
          {
            AddLog("Nothing sent. Sleeping for 60 sec", LogMessageColor.Error());
            for (int i = 0; i < 60; i++)
            {
              if (bStop)
                return;
              System.Threading.Thread.Sleep(1000);
            }
          }
        }
        catch (Exception ex)
        {
          AddLog("Can't send messages '" + ex.Message, LogMessageColor.Error());
        }
      }
    }

    private void btnSettings_Click(object sender, EventArgs e)
    {
      frmSettings frm = new frmSettings();
      frm.ShowDialog();
    }

    private void btnSendFromDB_Click(object sender, EventArgs e)
    {
      if (bStopped)
      {
        MySqlDB db = new MySqlDB(Properties.Default.MySqlServerUsername, Properties.Default.MySqlServerPassword, Properties.Default.MySqlServerAddress, Properties.Default.MySqlServerPort, Properties.Default.MySqlServerDatabase);
        Task.Factory.StartNew(() =>
          {
            bStopped = false;
            btnSendFromDB.Text = "Stop";
            SendMessageFromDatabase(db);
            db.Close();
            bStopped = true;
            btnSendFromDB.Text = "Send from DB";
            AddLog("Stopped", LogMessageColor.Information());
          }
        );
      }
      else
      {
        bStop = true;
        AddLog("Stopping", LogMessageColor.Information());
      }
    }
  }

  class LogMessageColor
  {
    public static Color Information()
    {
      return Color.Black;
    }
    public static Color Warning()
    {
      return Color.Gold;
    }
    public static Color Error()
    {
      return Color.Red;
    }
    public static Color Success()
    {
      return Color.LimeGreen;
    }
  }
}