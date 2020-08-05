﻿using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Windows.Forms;

using HFM.Log;

namespace HFM.Forms.Controls
{
    [ExcludeFromCodeCoverage]
    public partial class LogFileViewer : RichTextBox
    {
        private ICollection<LogLine> _logLines;

        public string LogOwnedByInstanceName { get; private set; } = String.Empty;

        public LogFileViewer()
        {
            InitializeComponent();
        }

        private const int MaxDisplayableLogLines = 500;

        public void SetLogLines(ICollection<LogLine> lines, string logOwnedByInstance, bool highlightLines)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<ICollection<LogLine>, string, bool>(SetLogLines), lines, logOwnedByInstance, highlightLines);
                return;
            }

            LogOwnedByInstanceName = logOwnedByInstance;

            // limit the maximum number of log lines
            int lineOffset = lines.Count - MaxDisplayableLogLines;
            if (lineOffset > 0)
            {
                lines = lines.Where((x, i) => i > lineOffset).ToList();
            }

            _logLines = lines;
            HighlightLines(highlightLines);
        }

        public void HighlightLines(bool value)
        {
#if DEBUG
            var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
            if (value)
            {
                Rtf = BuildRtfString();
            }
            else
            {
                Rtf = null;
                Lines = _logLines.Select(line => line.Raw.Replace("\r", String.Empty)).ToArray();
            }
#if DEBUG
            System.Diagnostics.Debug.WriteLine("HighlightLines: {0:#,##0} ms", sw.ElapsedMilliseconds);
#endif
        }

        private string BuildRtfString()
        {
            // cf1 - Dark Green
            // cf2 - Dark Red
            // cf3 - Dark Orange
            // cf4 - Blue
            // cf5 - Slate Gray

            var sb = new StringBuilder();
            sb.Append(@"{\rtf1\ansi\deff0{\colortbl;\red0\green150\blue0;\red139\green0\blue0;\red255\green140\blue0;\red0\green0\blue255;\red120\green120\blue120;}");
            foreach (var line in _logLines)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, @"{0}{1}\line", GetLineColor(line), line);
            }
            return sb.ToString();
        }

        private static string GetLineColor(LogLine line)
        {
            if (line.LineType == LogLineType.None)
            {
                return @"\cf5 ";
            }

            if (line.Data is LogLineDataParserError)
            {
                return @"\cf3 ";
            }

            if (line.LineType == LogLineType.WorkUnitFrame)
            {
                return @"\cf1 ";
            }

            if (line.LineType == LogLineType.ClientShutdown ||
                line.LineType == LogLineType.ClientCoreCommunicationsError ||
                line.LineType == LogLineType.ClientCoreCommunicationsErrorShutdown ||
                line.LineType == LogLineType.ClientEuePauseState ||
                line.LineType == LogLineType.WorkUnitCoreShutdown ||
                line.LineType == LogLineType.WorkUnitCoreReturn)
            {
                return @"\cf2 ";
            }

            return @"\cf4 ";
        }

        public void SetNoLogLines()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(SetNoLogLines));
                return;
            }

            _logLines = null;

            Rtf = Core.Application.IsRunningOnMono ? String.Empty : null;
            Text = "No Log Available";
        }

        #region Native Scroll Messages (don't call under Mono)

        public void ScrollToBottom()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(ScrollToBottom));
                return;
            }

            SelectionStart = TextLength;

            if (Core.Application.IsRunningOnMono)
            {
                ScrollToCaret();
            }
            else
            {
                Internal.NativeMethods.SendMessage(Handle, Internal.NativeMethods.WM_VSCROLL, new IntPtr(Internal.NativeMethods.SB_BOTTOM), new IntPtr(0));
            }
        }

        public void ScrollToTop()
        {
            if (Core.Application.IsRunningOnMono)
            {
                throw new NotImplementedException("This function is not implemented when running under the Mono Runtime.");
            }

            Internal.NativeMethods.SendMessage(Handle, Internal.NativeMethods.WM_VSCROLL, new IntPtr(Internal.NativeMethods.SB_TOP), new IntPtr(0));
        }

        public void ScrollLineDown()
        {
            if (Core.Application.IsRunningOnMono)
            {
                throw new NotImplementedException("This function is not implemented when running under the Mono Runtime.");
            }

            Internal.NativeMethods.SendMessage(Handle, Internal.NativeMethods.WM_VSCROLL, new IntPtr(Internal.NativeMethods.SB_LINEDOWN), new IntPtr(0));
        }

        public void ScrollLineUp()
        {
            if (Core.Application.IsRunningOnMono)
            {
                throw new NotImplementedException("This function is not implemented when running under the Mono Runtime.");
            }

            Internal.NativeMethods.SendMessage(Handle, Internal.NativeMethods.WM_VSCROLL, new IntPtr(Internal.NativeMethods.SB_LINEUP), new IntPtr(0));
        }

        public void ScrollToLine(int lineNumber)
        {
            if (Core.Application.IsRunningOnMono)
            {
                throw new NotImplementedException("This function is not implemented when running under the Mono Runtime.");
            }

            Internal.NativeMethods.SendMessage(Handle, Internal.NativeMethods.EM_LINESCROLL, new IntPtr(0), new IntPtr(lineNumber));
        }

        #endregion
    }
}
