using MahApps.Metro.Controls;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
namespace Updater
{
	public class MainWindow : MetroWindow, IComponentConnector
	{
		private const string URL = "URL";
		private const string Folder = "files/";
		private const string Executable = "Update.exe";
		private long patchSize = 0L;
		private long patchDownloaded = 0L;
		private Dictionary<int, PatchList> _PatchList = new Dictionary<int, PatchList>();
		internal ListBox lbLog;
		internal ProgressBar pbTotal;
		internal ProgressBar pbFile;
		private bool _contentLoaded;
		public static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			string currentDirectory = Environment.CurrentDirectory;
			string contents = string.Concat(new string[]
			{
				(e.ExceptionObject as Exception).TargetSite.Name,
				" : ",
				(e.ExceptionObject as Exception).Message,
				"\n",
				(e.ExceptionObject as Exception).StackTrace,
				"\n\n"
			});
			File.AppendAllText(currentDirectory + "\\LoaderCrashReport.txt", contents);
		}
		public MainWindow()
		{
			AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(MainWindow.CurrentDomain_UnhandledException);
			(
				from t in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName)
				where t.Id != Process.GetCurrentProcess().Id
				select t).ToList<Process>().ForEach(delegate(Process t)
			{
				t.Kill();
			});
			Process.GetProcessesByName("Update").ToList<Process>().ForEach(delegate(Process t)
			{
				t.Kill();
			});
			this.InitializeComponent();
			Thread.Sleep(1000);
			this.LoadPatchList();
		}
		private void RefreshInterface(int FileNumber)
		{
			this.patchDownloaded += this._PatchList[FileNumber - 1].Size;
			int num = Convert.ToInt32(this.patchDownloaded * 100L / this.patchSize);
			this.pbTotal.Value = (double)num;
			this.CheckFiles(FileNumber);
		}
		private string GetMD5FromFile(string Name)
		{
			string result;
			try
			{
				FileStream fileStream = new FileStream(Name, FileMode.Open);
				MD5 mD = new MD5CryptoServiceProvider();
				byte[] array = mD.ComputeHash(fileStream);
				fileStream.Close();
				StringBuilder stringBuilder = new StringBuilder();
				for (int i = 0; i < array.Length; i++)
				{
					stringBuilder.Append(array[i].ToString("x2"));
				}
				result = stringBuilder.ToString();
			}
			catch
			{
				result = "0";
			}
			return result;
		}
		private void Log(string message)
		{
			DateTime now = DateTime.Now;
			string str = string.Concat(new object[]
			{
				(now.Hour < 10) ? (48 + now.Hour) : now.Hour,
				":",
				(now.Minute < 10) ? (48 + now.Minute) : now.Minute,
				":",
				(now.Second < 10) ? (48 + now.Second) : now.Second
			});
			ListBoxItem listBoxItem = new ListBoxItem();
			listBoxItem.Content = str + " | " + message;
			listBoxItem.IsEnabled = false;
			this.lbLog.Items.Add(listBoxItem);
			this.lbLog.ScrollIntoView(this.lbLog.Items[this.lbLog.Items.Count - 1]);
		}
		private void LoadPatchList()
		{
			this.Log("Downloading PatchList");
			WebClient webClient = new WebClient();
			this.Log("Intizialized WebClient");
			webClient.DownloadFileCompleted += delegate(object param0, AsyncCompletedEventArgs param1)
			{
				this.PatchListToArray();
			};
			webClient.DownloadFileAsync(new Uri(string.Format("{0}index.php", "URL")), "updater.tmp");
			this.Log("Downloaded PatchList and rearray now.");
		}
		private void PatchListToArray()
		{
			bool flag = false;
			StreamReader streamReader = new StreamReader("updater.tmp");
			int num = 0;
			string text;
			while ((text = streamReader.ReadLine()) != null)
			{
				if (text == "[DIRECTORY]")
				{
					flag = true;
				}
				else
				{
					if (text == "[/DIRECTORY]")
					{
						flag = false;
					}
					else
					{
						if (flag && !Directory.Exists(text))
						{
							this.Log(string.Format("Creating Directory '{0}'", text));
							Directory.CreateDirectory(text);
						}
						else
						{
							if (!flag)
							{
								PatchList patchList = new PatchList();
								try
								{
									string[] array = text.Split(new char[]
									{
										'|'
									});
									patchList.Name = array[0];
									patchList.Hash = array[1];
									patchList.Size = (long)Convert.ToInt32(array[2]);
									this._PatchList.Add(num, patchList);
								}
								catch
								{
								}
								num++;
							}
						}
					}
				}
			}
			streamReader.Close();
			foreach (KeyValuePair<int, PatchList> current in this._PatchList)
			{
				this.patchSize += current.Value.Size;
			}
			File.Delete("updater.tmp");
			this.CheckFiles(0);
		}
		private void CheckFiles(int FileNumber)
		{
			if (this._PatchList.Count > FileNumber)
			{
				if (this._PatchList[FileNumber].Hash != this.GetMD5FromFile(this._PatchList[FileNumber].Name))
				{
					this.Log(string.Format("Downloading '{0}'", this._PatchList[FileNumber].Name));
					WebClient webClient = new WebClient();
					webClient.DownloadFileCompleted += delegate(object param0, AsyncCompletedEventArgs param1)
					{
						this.RefreshInterface(FileNumber + 1);
					};
					webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(this.wc_DownloadProgressChanged);
					webClient.DownloadFileAsync(new Uri(string.Format("{0}{1}{2}", "URL", "files/", this._PatchList[FileNumber].Name)), this._PatchList[FileNumber].Name);
				}
				else
				{
					this.Log(string.Format("'{0}' is up to date", this._PatchList[FileNumber].Name));
					this.RefreshInterface(FileNumber + 1);
				}
			}
			else
			{
				this.Log("Update Finished");
				if (File.Exists("Crypt.exe"))
				{
					Process process = new Process();
					process.StartInfo = new ProcessStartInfo("pCrypt.exe", "pCrypt");
					File.WriteAllText("pCrypt", "<?xml version=\"1.0\" encoding=\"utf-8\"?><project outputDir=\"tmp\" snKey=\"\" preset=\"normal\"><packer id=\"compressor\" /><assembly path=\"Update.exe\" isMain=\"true\" /></project>");
					process.Start();
					process.WaitForExit();
					File.Delete("pCrypt");
					File.Delete("Update.exe");
					File.Move("tmp\\Update.exe", "Update.exe");
					File.Delete("tmp\\report.crdb");
					Directory.Delete("tmp");
				}
				if (File.Exists("Update.exe"))
				{
					Process.Start("Update.exe", "--noupdate");
				}
				Environment.Exit(0);
			}
		}
		private void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			this.pbFile.Value = (double)e.ProgressPercentage;
			int num = Convert.ToInt32((this.patchDownloaded + e.BytesReceived) * 100L / this.patchSize);
			this.pbTotal.Value = (double)num;
		}
		[GeneratedCode("PresentationBuildTasks", "4.0.0.0"), DebuggerNonUserCode]
		public void InitializeComponent()
		{
			if (!this._contentLoaded)
			{
				this._contentLoaded = true;
				Uri resourceLocator = new Uri("/Updater;component/mainwindow.xaml", UriKind.Relative);
				Application.LoadComponent(this, resourceLocator);
			}
		}
		[GeneratedCode("PresentationBuildTasks", "4.0.0.0"), EditorBrowsable(EditorBrowsableState.Never), DebuggerNonUserCode]
		void IComponentConnector.Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
			case 1:
				this.lbLog = (ListBox)target;
				break;
			case 2:
				this.pbTotal = (ProgressBar)target;
				break;
			case 3:
				this.pbFile = (ProgressBar)target;
				break;
			default:
				this._contentLoaded = true;
				break;
			}
		}
	}
}
