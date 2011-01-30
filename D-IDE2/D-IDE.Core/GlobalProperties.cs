﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.IO;
using Parser.Core;
using System.Xml;
using D_IDE.Core;

namespace D_IDE
{
	public class GlobalProperties
	{
		/// <summary>
		/// A list of all globally loaded projects.
		/// Useful when
		/// </summary>
		public static List<Project> ProjectCache = new List<Project>();

        public const string MainSettingsFile = "D-IDE2.settings.xml";
        public const string LayoutFile = "D-IDE2.layout.xml";

        /// <summary>
        /// Globally initializes all settings and essential properties
        /// </summary>
        public static void Init()
        {
			try
			{
				Instance = Load();
				if (Instance == null)
					Instance = new GlobalProperties();
			}
			catch (Exception ex) { ErrorLogger.Log(ex); }
        }

		#region Loading & Saving
		public static GlobalProperties Load()
		{
			return Load(Path.Combine(IDEInterface.ConfigDirectory, MainSettingsFile));
		}
		public static GlobalProperties Load(string fn)
		{
			if (!File.Exists(fn)) return null;

            try
            {
                Stream stream = File.Open(fn, FileMode.Open);

                XmlTextReader xr = new XmlTextReader(stream);
                var p = new GlobalProperties();

                while (xr.Read())// now 'settings' should be the current node
                {
                    if (xr.NodeType == XmlNodeType.Element)
                    {
                        switch (xr.LocalName)
                        {
                            default: break;
                                
                            case "codetemplates":
                                //CodeTemplate.Load(xr);
                                break;

                            case "recentprojects":
                                if (xr.IsEmptyElement) break;
                                while (xr.Read())
                                {
                                    if (xr.LocalName == "f")
                                    {
                                        try
                                        {
                                            p.LastProjects.Add(xr.ReadString());
                                        }
                                        catch { }
                                    }
                                    else break;
                                }
                                break;

                            case "recentfiles":
                                if (xr.IsEmptyElement) break;
                                while (xr.Read())
                                {
                                    if (xr.LocalName == "f")
                                    {
                                        try
                                        {
                                            p.LastFiles.Add(xr.ReadString());
                                        }
                                        catch { }
                                    }
                                    else break;
                                }
                                break;

                            case "lastopenedfiles":
                                if (xr.IsEmptyElement) break;
                                while (xr.Read())
                                {
                                    if (xr.LocalName == "f")
                                    {
                                        try
                                        {
                                            p.LastOpenFiles.Add(xr.ReadString());
                                        }
                                        catch { }
                                    }
                                    else break;
                                }
                                break;


                            case "openlastprj":
                                if (xr.MoveToAttribute("value"))
                                {
                                    p.OpenLastPrj = xr.Value == "1";
                                }
                                break;

                            case "openlastfiles":
                                if (xr.MoveToAttribute("value"))
                                {
                                    p.OpenLastFiles = xr.Value == "1";
                                }
                                break;

                            case "windowstate":
                                if (xr.MoveToAttribute("value"))
                                {
                                    try { p.lastFormState = (WindowState)Convert.ToInt32(xr.Value); }
                                    catch { }
                                }
                                break;

                            case "windowsize":
								p.lastFormSize = new Size();
                                if (xr.MoveToAttribute("x"))
                                {
                                    try { p.lastFormSize.Width = Convert.ToInt32(xr.Value); }
                                    catch { }
                                }
                                if (xr.MoveToAttribute("y"))
                                {
                                    try { p.lastFormSize.Height = Convert.ToInt32(xr.Value); }
                                    catch { }
                                }
                                break;

                            case "retrievenews":
                                if (xr.MoveToAttribute("value"))
                                {
                                    p.RetrieveNews = xr.Value == "1";
                                }
                                break;

                            case "externaldbg":
                                if (xr.MoveToAttribute("value"))
                                {
                                    p.UseExternalDebugger = xr.Value == "1";
                                }
                                break;

							case "lastselectedribbontab":
								if (xr.MoveToAttribute("value"))
								{
									p.LastSelectedRibbonTab=Convert.ToInt32(xr.Value);
								}
								break;

                            case "watchforupdates":
                                if (xr.MoveToAttribute("value"))
                                {
                                    p.WatchForUpdates = xr.Value == "1";
                                }
                                break;

                            case "defprjdir":
                                p.DefaultProjectDirectory = xr.ReadString();
                                break;

                            case "debugger":
                                if (xr.IsEmptyElement) break;
                                while (xr.Read())
                                {
                                    if (xr.LocalName == "bin")
                                    {
                                        p.ExternalDebugger_Bin = xr.ReadString();
                                    }
                                    else if (xr.LocalName == "args")
                                    {
                                        p.ExternalDebugger_Arguments = xr.ReadString();
                                    }
                                    else break;
                                }
                                break;

                            case "lastsearchdir":
                                p.lastSearchDir = xr.ReadString();
                                break;

                            case "verbosedbgoutput":
                                if (xr.MoveToAttribute("value"))
                                {
                                    p.VerboseDebugOutput = xr.Value == "1";
                                }
                                break;

                            case "skipunknowncode":
                                if (xr.MoveToAttribute("value"))
                                {
                                    p.SkipUnknownCode = xr.Value == "1";
                                }
                                break;

							case "verbosebuild":
								if (xr.MoveToAttribute("value"))
								{
									p.VerboseBuildOutput = xr.Value == "1";
								}
								break;

                            case "autosave":
                                if (xr.MoveToAttribute("value"))
                                {
                                    p.DoAutoSaveOnBuilding = xr.Value == "1";
                                }
                                break;


                            case "highlightings":
                                if (xr.IsEmptyElement) break;
                                while (xr.Read())
                                {
                                    if (xr.LocalName == "f")
                                    {
                                        try
                                        {
                                            string ext = xr.GetAttribute("ext");
                                            p.SyntaxHighlightingEntries.Add(ext, xr.ReadString());
                                        }
                                        catch { }
                                    }
                                    else break;
                                }
                                break;

                            case "shownewconsolewhenexecuting":
                                if (xr.MoveToAttribute("value"))
                                {
                                    p.ShowDebugConsole = xr.Value == "1";
                                }
                                break;
                        }
                    }
                }

                xr.Close();
				return p;

            }
            catch {  }
            return null;
		}

		public static void Save()
		{
			try
			{
				Save(Path.Combine(IDEInterface.ConfigDirectory, MainSettingsFile));
			}
			catch (Exception ex) { ErrorLogger.Log(ex); }
		}
		public static void Save(string fn)
		{
			if (String.IsNullOrEmpty(fn)) return;

			var xw = new XmlTextWriter(fn, Encoding.UTF8);
			xw.WriteStartDocument();
			xw.WriteStartElement("settings");

			xw.WriteStartElement("recentprojects");
			foreach (string f in Instance.LastProjects)
			{
				xw.WriteStartElement("f"); xw.WriteCData(f); xw.WriteEndElement();
			}
			xw.WriteEndElement();

			xw.WriteStartElement("recentfiles");
			foreach (string f in Instance.LastFiles)
			{
				xw.WriteStartElement("f"); xw.WriteCData(f); xw.WriteEndElement();
			}
			xw.WriteEndElement();

			xw.WriteStartElement("lastopenedfiles");
			foreach (string f in Instance.LastOpenFiles)
			{
				xw.WriteStartElement("f"); xw.WriteCData(f); xw.WriteEndElement();
			}
			xw.WriteEndElement();

			xw.WriteStartElement("openlastprj");
			xw.WriteAttributeString("value", Instance.OpenLastPrj ? "1" : "0");
			xw.WriteEndElement();

			xw.WriteStartElement("openlastfiles");
			xw.WriteAttributeString("value", Instance.OpenLastFiles ? "1" : "0");
			xw.WriteEndElement();

			xw.WriteStartElement("windowstate");
			xw.WriteAttributeString("value", ((int)Instance.lastFormState).ToString());
			xw.WriteEndElement();

			xw.WriteStartElement("windowsize");
			xw.WriteAttributeString("x", Instance.lastFormSize.Width.ToString());
			xw.WriteAttributeString("y", Instance.lastFormSize.Height.ToString());
			xw.WriteEndElement();

			xw.WriteStartElement("retrievenews");
			xw.WriteAttributeString("value", Instance.RetrieveNews ? "1" : "0");
			xw.WriteEndElement();

			xw.WriteStartElement("externaldbg");
			xw.WriteAttributeString("value", Instance.UseExternalDebugger ? "1" : "0");
			xw.WriteEndElement();

			xw.WriteStartElement("lastselectedribbontab");
			xw.WriteAttributeString("value", Instance.LastSelectedRibbonTab.ToString());
			xw.WriteEndElement();

			xw.WriteStartElement("watchforupdates");
			xw.WriteAttributeString("value", Instance.WatchForUpdates ? "1" : "0");
			xw.WriteEndElement();

			xw.WriteStartElement("defprjdir");
			xw.WriteCData(Instance.DefaultProjectDirectory);
			xw.WriteEndElement();

			xw.WriteStartElement("debugger");
			xw.WriteStartElement("bin");
			xw.WriteCData(Instance.ExternalDebugger_Bin);
			xw.WriteEndElement();
			xw.WriteStartElement("args");
			xw.WriteCData(Instance.ExternalDebugger_Arguments);
			xw.WriteEndElement();
			xw.WriteEndElement();

			xw.WriteStartElement("lastsearchdir");
			xw.WriteCData(Instance.lastSearchDir);
			xw.WriteEndElement();

			xw.WriteStartElement("verbosedbgoutput");
			xw.WriteAttributeString("value", Instance.VerboseDebugOutput ? "1" : "0");
			xw.WriteEndElement();

			xw.WriteStartElement("verbosebuild");
			xw.WriteAttributeString("value", Instance.VerboseBuildOutput ? "1" : "0");
			xw.WriteEndElement();

			xw.WriteStartElement("autosave");
			xw.WriteAttributeString("value", Instance.DoAutoSaveOnBuilding ? "1" : "0");
			xw.WriteEndElement();

			xw.WriteStartElement("autosave");
			xw.WriteAttributeString("value", Instance.DoAutoSaveOnBuilding ? "1" : "0");
			xw.WriteEndElement();

			xw.WriteStartElement("highlightings");
			foreach (string ext in Instance.SyntaxHighlightingEntries.Keys)
			{
				if (String.IsNullOrEmpty(Instance.SyntaxHighlightingEntries[ext])) continue;
				xw.WriteStartElement("f");
				xw.WriteAttributeString("ext", ext);
				xw.WriteCData(Instance.SyntaxHighlightingEntries[ext]);
				xw.WriteEndElement();
			}
			xw.WriteEndElement();

            xw.WriteStartElement("shownewconsolewhenexecuting");
            xw.WriteAttributeString("value", Instance.ShowDebugConsole ? "1" : "0");
            xw.WriteEndElement();

            //Code templates
            //CodeTemplate.Save(xw);

			xw.WriteEndDocument();
			xw.Close();
		}
		#endregion
		public static GlobalProperties Instance=null;

		public List<string>
			LastProjects = new List<string>(),
			LastFiles = new List<string>(),
			LastOpenFiles = new List<string>();
				
		public WindowState lastFormState = WindowState.Maximized;
		public Size lastFormSize=Size.Empty;
		public Dictionary<string, string> SyntaxHighlightingEntries = new Dictionary<string, string>();

		#region Build
		public bool VerboseBuildOutput = true;
		public bool DoAutoSaveOnBuilding = true;
		public string DefaultBinariesPath = "bin";
		#endregion

		#region Debugging
		public bool VerboseDebugOutput = false;
		public bool SkipUnknownCode = true;
        public bool ShowDebugConsole = true;

		public bool UseExternalDebugger = false;
		public string ExternalDebugger_Bin = "windbg.exe";
		public string ExternalDebugger_Arguments = "\"$exe\"";
		#endregion

		#region General

		readonly static string MultipleInstanceFlagFile = IDEInterface.CommonlyUsedDirectory + "\\MultipleInstancesAllowed.flag";
		public static bool AllowMultipleProgramInstances
		{
			get { return !File.Exists(MultipleInstanceFlagFile); }
			set
			{
				if (value && !AllowMultipleProgramInstances)
					File.WriteAllText(MultipleInstanceFlagFile, "Indicates whether D-IDE is allowed to be launched multiple times or not.");
				else if (!value && AllowMultipleProgramInstances)
					File.Delete(MultipleInstanceFlagFile);
			}
		}

		public int LastSelectedRibbonTab = 0;

		public bool WatchForUpdates = true;
		public bool ShowStartPage = true;
		public bool RetrieveNews = true;

		public string DefaultProjectDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\D Projects";
		public bool OpenLastPrj = true;
		public bool OpenLastFiles = true;
		#endregion

		public string lastSearchDir =Util.ApplicationStartUpPath;
	}
}
