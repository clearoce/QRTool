using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace QRTool
{
    public static class PrinterService
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DOC_INFO_1
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPWStr)] public string pOutputFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string pDatatype;
        }

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int StartDocPrinter(IntPtr hPrinter, int level, ref DOC_INFO_1 pDocInfo);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool AbortPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);

        public static List<string> ListPrinters()
        {
            List<string> printers = new List<string>();
            foreach (string name in PrinterSettings.InstalledPrinters)
            {
                if (!string.IsNullOrEmpty(name)) printers.Add(name);
            }
            printers.Sort(StringComparer.CurrentCultureIgnoreCase);
            return printers;
        }

        public static string DefaultPrinter()
        {
            try
            {
                PrinterSettings settings = new PrinterSettings();
                return settings.PrinterName == null ? "" : settings.PrinterName;
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static int SendRaw(string printerName, byte[] data, string jobName)
        {
            if (printerName == null || printerName.Trim().Length == 0)
                throw new ArgumentException("未选择打印机");
            if (data == null || data.Length == 0)
                throw new ArgumentException("没有可发送的 TSPL 数据");

            IntPtr handle;
            if (!OpenPrinter(printerName.Trim(), out handle, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法打开打印机");

            bool docStarted = false;
            try
            {
                DOC_INFO_1 info = new DOC_INFO_1();
                info.pDocName = string.IsNullOrEmpty(jobName) ? "QRTool QR Label" : jobName;
                info.pOutputFile = null;
                info.pDatatype = "RAW";

                int jobId = StartDocPrinter(handle, 1, ref info);
                if (jobId == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "创建打印作业失败");
                docStarted = true;

                try
                {
                    if (!StartPagePrinter(handle))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "开始打印页失败");

                    int offset = 0;
                    while (offset < data.Length)
                    {
                        int remainingLength = data.Length - offset;
                        byte[] remaining = new byte[remainingLength];
                        Array.Copy(data, offset, remaining, 0, remainingLength);
                        int written;
                        if (!WritePrinter(handle, remaining, remainingLength, out written) || written <= 0)
                            throw new Win32Exception(Marshal.GetLastWin32Error(), "写入打印数据失败");
                        offset += written;
                    }

                    if (!EndPagePrinter(handle))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "结束打印页失败");
                    if (!EndDocPrinter(handle))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "提交打印作业失败");

                    docStarted = false;
                    return jobId;
                }
                catch (Exception)
                {
                    if (docStarted)
                    {
                        try { AbortPrinter(handle); } catch (Exception) { }
                    }
                    throw;
                }
            }
            finally
            {
                ClosePrinter(handle);
            }
        }
    }
}
