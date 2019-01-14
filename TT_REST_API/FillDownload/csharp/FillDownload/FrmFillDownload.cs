using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.IO;

namespace FillDownload
{
    public partial class FrmFillDownload : Form
    {
        FillDownloadThread m_fillThread = null;
        StreamWriter m_outputFile = null;

        List<FillColumn> m_TradePaneColumns = null;

        public FrmFillDownload()
        {
            InitializeComponent();

            InitializeColumnList();

            clbColumns.Items.Clear();
            foreach (FillColumn column in m_TradePaneColumns)
            {
                clbColumns.Items.Add(column, true);
            }
             
            this.FormClosing += FrmFillDownload_FormClosing;

            LoadSettings();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            // Validate our inputs

            int interval = 0;
            if (!int.TryParse(txtFrequency.Text, out interval))
            {
                MessageBox.Show("Error parsing frequency \"" + txtFrequency.Text + "\".");
                return;
            }

            if(interval <= 0)
            {
                MessageBox.Show("Frequency must be greater than zero.");
                return;
            }

            TimeSpan start_time = dtpStartTime.Value.TimeOfDay;
            TimeSpan end_time = dtpEndTime.Value.TimeOfDay;
            if(start_time > end_time)
            {
                MessageBox.Show("Error: Start time must come before end time");
                return;
            }

            // Try to log in to the REST API
            RestManager.Init(txtKey.Text, txtSecret.Text, txtEnvironment.Text);
            if (!RestManager.IsAuthorized())
            {
                MessageBox.Show("Rest API was not able to log in with provided App Key and Secret");
                return;
            }
            else
            {
                RestManager.OnTokenError += RestManager_OnTokenError;
            }

            DateTime start_date = default(DateTime);
            if(dtpStartDate.CustomFormat != " ")
            {
                // TimeStamp correction so it properly reflects midnight of 
                // this day in local time
                start_date = dtpStartDate.Value.Date - TimeZoneInfo.Local.BaseUtcOffset;
            }
            else
            {
                start_date = DateTime.Today - TimeZoneInfo.Local.BaseUtcOffset;
            }

            if (!chkSunday.Checked && !chkMonday.Checked && !chkTuesday.Checked && !chkWednesday.Checked && !chkThursday.Checked && !chkFriday.Checked && !chkSaturday.Checked)
            {
                MessageBox.Show("Must select at least one day to run downloader.");
                return;
            }

            bool[] days_to_run = new bool[7];
            days_to_run[0] = chkSunday.Checked;
            days_to_run[1] = chkMonday.Checked;
            days_to_run[2] = chkTuesday.Checked;
            days_to_run[3] = chkWednesday.Checked;
            days_to_run[4] = chkThursday.Checked;
            days_to_run[5] = chkFriday.Checked;
            days_to_run[6] = chkSaturday.Checked;

            try
            {
                FileStream fs = File.Create(txtOutput.Text);
                fs.Close();
                m_outputFile = new StreamWriter(txtOutput.Text, true, Encoding.ASCII);
                m_outputFile.AutoFlush = true;
                m_outputFile.Write(GetCSVHeader());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error creating output file: " + ex.Message);
                return;
            }

            clbColumns.Enabled = false;
            btnStart.Enabled = false;
            btnBrowse.Enabled = false;

            m_fillThread = new FillDownloadThread(start_time, end_time, new TimeSpan(0, interval, 0), days_to_run, start_date);
            m_fillThread.FillDownload += fillThread_OnFillDownload;
            m_fillThread.OnError += OnError;
            m_fillThread.Start();
        }

        private void RestManager_OnTokenError(object sender, string error_message)
        {
            this.OnError(this, error_message);
        }

        private void fillThread_OnFillDownload(object sender, List<TT_Fill> fills)
        {
            bool errors = false;
            foreach (TT_Fill fill in fills)
            {
                String row = "";
                foreach (FillColumn column in clbColumns.CheckedItems)
                {
                    try
                    {
                        row += column.DisplayField(fill) + ",";
                    }
                    catch (Exception ex)
                    {
                        row += ",";
                        ErrorLog.Write("Error: Error parsing fill column " + column.ColumnName + " for fill " + fill.RecordID + Environment.NewLine + ex.Message);
                        errors = true;
                    }
                }
                row += Environment.NewLine;
                m_outputFile.Write(row.ToString());
            }

            if (errors)
                this.OnError(this, "Errors parsing fills. Closing down.");
        }

        private void OnError(object sender, string error_message)
        {
            ErrorLog.Write(error_message + Environment.NewLine + "--------------------------------------------------" + Environment.NewLine);
            MessageBox.Show(error_message);

            if (this.InvokeRequired)
                this.BeginInvoke(new Action(this.Close));
            else
                this.Close();
        }

        private void FrmFillDownload_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (m_fillThread != null)
                m_fillThread.StopThread();

            if (m_outputFile != null)
                m_outputFile.Close();
        }

        private void Form_Load(object sender, EventArgs e)
        {

        }

        private void InitializeColumnList()
        {
            m_TradePaneColumns = new List<FillColumn>();

            m_TradePaneColumns.Add(new FillColumn("UtcDate", delegate (TT_Fill fill) { return fill.UtcDateString; }));
            m_TradePaneColumns.Add(new FillColumn("UtcTime", delegate (TT_Fill fill) { return fill.UtcTimeString; }));
            m_TradePaneColumns.Add(new FillColumn("ExchangeName", delegate (TT_Fill fill) { return fill.ExchangeName; }));
            m_TradePaneColumns.Add(new FillColumn("ContractName", delegate (TT_Fill fill) { return fill.ContractName; }));
            m_TradePaneColumns.Add(new FillColumn("TradeSide", delegate (TT_Fill fill) { return fill.TradeSide; }));
            m_TradePaneColumns.Add(new FillColumn("FillQty", delegate (TT_Fill fill) { return fill.FillQty; }));
            m_TradePaneColumns.Add(new FillColumn("Price", delegate (TT_Fill fill) { return fill.Price; }));
            m_TradePaneColumns.Add(new FillColumn("FullPartial", delegate (TT_Fill fill) { return fill.FullPartial; }));
            m_TradePaneColumns.Add(new FillColumn("OrdType", delegate (TT_Fill fill) { return fill.OrdType; }));
            m_TradePaneColumns.Add(new FillColumn("Modifier", delegate (TT_Fill fill) { return fill.Modifier; }));
            m_TradePaneColumns.Add(new FillColumn("Route", delegate (TT_Fill fill) { return fill.Route; }));
            m_TradePaneColumns.Add(new FillColumn("PositionEffect", delegate (TT_Fill fill) { return fill.PositionEffect; }));
            m_TradePaneColumns.Add(new FillColumn("Broker", delegate (TT_Fill fill) { return fill.Broker; }));
            m_TradePaneColumns.Add(new FillColumn("Account", delegate (TT_Fill fill) { return fill.Account; }));
            m_TradePaneColumns.Add(new FillColumn("AccountType", delegate (TT_Fill fill) { return fill.AccountType; }));
            m_TradePaneColumns.Add(new FillColumn("GiveUp", delegate (TT_Fill fill) { return fill.GiveUp; }));
            m_TradePaneColumns.Add(new FillColumn("TextA", delegate (TT_Fill fill) { return fill.TextA; }));
            m_TradePaneColumns.Add(new FillColumn("TextB", delegate (TT_Fill fill) { return fill.TextB; }));
            m_TradePaneColumns.Add(new FillColumn("TextC", delegate (TT_Fill fill) { return fill.TextC; }));
            m_TradePaneColumns.Add(new FillColumn("TextTT", delegate (TT_Fill fill) { return fill.TextTT; }));
            m_TradePaneColumns.Add(new FillColumn("Originator", delegate (TT_Fill fill) { return fill.Originator; }));
            m_TradePaneColumns.Add(new FillColumn("CurrentUser", delegate (TT_Fill fill) { return fill.CurrentUser; }));
            m_TradePaneColumns.Add(new FillColumn("ClientOrderID", delegate (TT_Fill fill) { return fill.ClientOrderID; }));
            m_TradePaneColumns.Add(new FillColumn("ParentOrderID", delegate (TT_Fill fill) { return fill.ParentOrderID; }));
            m_TradePaneColumns.Add(new FillColumn("OmaOrderID", delegate (TT_Fill fill) { return fill.OmaOrderID; }));
            m_TradePaneColumns.Add(new FillColumn("ExchangeOrderID", delegate (TT_Fill fill) { return fill.ExchangeOrderID; }));
            m_TradePaneColumns.Add(new FillColumn("ExchangeTransactionID", delegate (TT_Fill fill) { return fill.ExchangeTransactionID; }));
            m_TradePaneColumns.Add(new FillColumn("ExchangeAccount", delegate (TT_Fill fill) { return fill.ExchangeAccount; }));
            m_TradePaneColumns.Add(new FillColumn("ExchangeDate", delegate (TT_Fill fill) { return fill.ExchangeDate; }));
            m_TradePaneColumns.Add(new FillColumn("ExchangeTime", delegate (TT_Fill fill) { return fill.ExchangeTime; }));
            m_TradePaneColumns.Add(new FillColumn("ManualFill", delegate (TT_Fill fill) { return fill.ManualFill; }));
            m_TradePaneColumns.Add(new FillColumn("Symbol", delegate (TT_Fill fill) { return fill.Symbol; }));
            m_TradePaneColumns.Add(new FillColumn("ProductType", delegate (TT_Fill fill) { return fill.ProductType; }));
            m_TradePaneColumns.Add(new FillColumn("FillType", delegate (TT_Fill fill) { return fill.FillType; }));
            m_TradePaneColumns.Add(new FillColumn("ExecQty", delegate (TT_Fill fill) { return fill.ExecQty; }));
            m_TradePaneColumns.Add(new FillColumn("WorkQty", delegate (TT_Fill fill) { return fill.WorkQty; }));
            m_TradePaneColumns.Add(new FillColumn("AggressorFlag", delegate (TT_Fill fill) { return fill.AggressorFlag; }));
            m_TradePaneColumns.Add(new FillColumn("ConnectionId", delegate (TT_Fill fill) { return fill.ConnectionId; }));
            m_TradePaneColumns.Add(new FillColumn("PutCall", delegate (TT_Fill fill) { return fill.PutCall; }));
            m_TradePaneColumns.Add(new FillColumn("Strike", delegate (TT_Fill fill) { return fill.Strike; }));
            m_TradePaneColumns.Add(new FillColumn("OrderOrigination", delegate (TT_Fill fill) { return fill.OrderOrigination; }));
            m_TradePaneColumns.Add(new FillColumn("TradingCapacity", delegate (TT_Fill fill) { return fill.TradingCapacity; }));
            m_TradePaneColumns.Add(new FillColumn("LiquidityProvision", delegate (TT_Fill fill) { return fill.LiquidityProvision; }));
            m_TradePaneColumns.Add(new FillColumn("CommodityDerivativeIndicator", delegate (TT_Fill fill) { return fill.CommodityDerivativeIndicator; }));
            m_TradePaneColumns.Add(new FillColumn("InvestDec", delegate (TT_Fill fill) { return fill.InvestDec; }));
            m_TradePaneColumns.Add(new FillColumn("ExecDec", delegate (TT_Fill fill) { return fill.ExecDec; }));
            m_TradePaneColumns.Add(new FillColumn("ClientID", delegate (TT_Fill fill) { return fill.ClientID; }));
        }

        private String GetCSVHeader()
        {
            String header = "";
            var selected_columns = clbColumns.SelectedItems;

            var selectEnumerator = selected_columns.GetEnumerator();
            for(int i = 0; i < clbColumns.CheckedItems.Count; i++)
            {
                header += clbColumns.CheckedItems[i].ToString() + ",";
            }

            header += Environment.NewLine;
            return header;
        }

        private void dtpStartDate_ValueChanged(object sender, EventArgs e)
        {
            // DateTimePickers do not allow a null value so we signal whether 
            // the date time picker is being used or not by displaying the date
            // or making it blank.
            if (dtpStartDate.Checked)
                dtpStartDate.CustomFormat = "MM/dd/yyyy";
            else
                dtpStartDate.CustomFormat = " ";
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            DialogResult result = fdOutFile.ShowDialog();
            if (result == DialogResult.OK)
            {
                string file = fdOutFile.FileName;
                try
                {
                    txtOutput.Text = file;
                }
                catch (IOException)
                {
                }
            }
        }

        private void LoadSettings()
        {
            txtURL.Text = Properties.filldownload.Default.ApiURL;
            txtKey.Text = Properties.filldownload.Default.Key;
            txtSecret.Text = Properties.filldownload.Default.Secret;
            txtEnvironment.Text = Properties.filldownload.Default.Env;

            txtFrequency.Text = Properties.filldownload.Default.Frequency;

            txtOutput.Text = Properties.filldownload.Default.OutputLocation;

            dtpEndTime.Value = dtpEndTime.Value.Date + Properties.filldownload.Default.EndTime;
            dtpStartTime.Value = dtpEndTime.Value.Date + Properties.filldownload.Default.StartTime;

            if (Properties.filldownload.Default.StartDate != default(DateTime))
            {
                dtpStartDate.Checked = true;
                dtpStartDate.Value = Properties.filldownload.Default.StartDate;
            }

            chkSunday.Checked = Properties.filldownload.Default.RunSunday;
            chkMonday.Checked = Properties.filldownload.Default.RunMonday;
            chkTuesday.Checked = Properties.filldownload.Default.RunTuesday;
            chkWednesday.Checked = Properties.filldownload.Default.RunWednesday;
            chkThursday.Checked = Properties.filldownload.Default.RunThurday;
            chkFriday.Checked = Properties.filldownload.Default.RunFriday;
            chkSaturday.Checked = Properties.filldownload.Default.RunSaturday;

            var columns = Properties.filldownload.Default.Columns;
            if (columns != null)
            {
                for (int i = 0; i < clbColumns.Items.Count; ++i)
                {
                    clbColumns.SetItemChecked(i, columns.Contains(clbColumns.Items[i].ToString()));
                }
            }
        }

        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            Properties.filldownload.Default.ApiURL = txtURL.Text;
            Properties.filldownload.Default.Key = txtKey.Text;
            Properties.filldownload.Default.Secret = txtSecret.Text;
            Properties.filldownload.Default.Env = txtEnvironment.Text;

            Properties.filldownload.Default.Frequency = txtFrequency.Text;

            Properties.filldownload.Default.OutputLocation = txtOutput.Text;

            Properties.filldownload.Default.StartTime = dtpStartTime.Value.TimeOfDay;
            Properties.filldownload.Default.EndTime = dtpEndTime.Value.TimeOfDay;

            if (dtpStartDate.Checked)
                Properties.filldownload.Default.StartDate = dtpStartDate.Value;
            else
                Properties.filldownload.Default.StartDate = default(DateTime);

            Properties.filldownload.Default.RunSunday = chkSunday.Checked;
            Properties.filldownload.Default.RunMonday = chkMonday.Checked;
            Properties.filldownload.Default.RunTuesday = chkTuesday.Checked;
            Properties.filldownload.Default.RunWednesday = chkWednesday.Checked;
            Properties.filldownload.Default.RunThurday = chkThursday.Checked;
            Properties.filldownload.Default.RunFriday = chkFriday.Checked;
            Properties.filldownload.Default.RunSaturday = chkSaturday.Checked;

            var columns = new System.Collections.Specialized.StringCollection();
            foreach(var col in clbColumns.CheckedItems)
            {
                columns.Add(col.ToString());
            }
            Properties.filldownload.Default.Columns = columns;

            Properties.filldownload.Default.Save();
        }
    }


    delegate String ColumnDisplay(TT_Fill display);

    class FillColumn
    {
        public String ColumnName;
        public String DisplayField(TT_Fill fill)
        {
            return fieldDisplay(fill);
        }
        ColumnDisplay fieldDisplay;

        public FillColumn(String name, ColumnDisplay display)
        {
            ColumnName = name;
            fieldDisplay = display;
        }

        public override string ToString()
        {
            return ColumnName;
        }
    }
}