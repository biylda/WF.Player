﻿// <copyright file="ICartridgeProvider.cs" company="Wherigo Foundation">
//   WF.Player - A Wherigo Player which use the Wherigo Foundation Core.
//   Copyright (C) 2012-2014  Brice Clocher (contact@cybisoft.net)
//   Copyright (C) 2012-2014  Dirk Weltz (mail@wfplayer.com)
// </copyright>

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using Microsoft.Live;
using System.Collections.Generic;
using System.Linq;
using System.IO.IsolatedStorage;
using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Threading;
using Geowigo.Utils;
using Xamarin.Forms;

namespace WF.Player.Models.Providers
{
	/// <summary>
	/// A provider that can download cartridges from a user's SkyDrive cloud storage.
	/// </summary>
	public class SkyDriveCartridgeProvider : ICartridgeProvider
	{
		#region Constants

		private static readonly string LiveConnectClientID = "000000004C10D95D";

		private static readonly string[] _Scopes = new string[] { "wl.basic", "wl.skydrive_update", "wl.offline_access", "wl.signin" };

		private static readonly TimeSpan GetRequestTimeoutTimeSpan = TimeSpan.FromSeconds(20d);

		private static readonly string UploadFolderName = "Uploads";

		#endregion
		
		#region Nested Classes

		private class SkyDriveFile
		{
			public SkyDriveFile(string id, string name, string dlDirectory)
			{
				Id = id;
				Name = name;
				DownloadDirectory = dlDirectory;
			}
			public string Id { get; set; }

			public string Name { get; set; }

			public string DownloadDirectory { get; set; }
		}

		#endregion
		
		#region Members

		private bool _isLinked = false;
		private bool _autoLoginOnInitFail = false;
		private bool _isSyncing = false;

		private CartridgeProviderSyncEventArgs _syncEventArgs;

		private List<SkyDriveFile> _dlFiles = new List<SkyDriveFile>();

		private List<string> _ulFiles;

		private object _syncRoot = new object();

		private LiveAuthClient _authClient;
		private LiveConnectClient _liveClient;

		private Timer _requestTimeout;

		private string _geowigoFolderId;
		private string _uploadsFolderId;
		private IsolatedStorageFileStream _currentUlFileStream;
		
		#endregion

		#region Properties
		public string ServiceName
		{
			get { return "OneDrive"; }
		}

		public bool IsLinked
		{
			get
			{
				lock (_syncRoot)
				{
					return _isLinked;
				}
			}

			private set
			{
				lock (_syncRoot)
				{
					if (_isLinked != value)
					{
						_isLinked = value;
						RaisePropertyChanged("IsLinked");
					}
				}
			}
		}

		public string IsoStoreCartridgesPath
		{
			get;
			set;
		}

		public string IsoStoreCartridgeContentPath
		{
			get;
			set;
		}

		public bool IsSyncing
		{
			get
			{
				lock (_syncRoot)
				{
					return _isSyncing;
				}
			}

			private set
			{
				lock (_syncRoot)
				{
					if (_isSyncing != value)
					{
						_isSyncing = value;
						RaisePropertyChanged("IsSyncing");
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets if this provider is allowed to perform
		/// background downloads (if they are supported).
		/// </summary>
		public bool IsBackgroundDownloadAllowed
		{
			get;
			set;
		}
		#endregion

		#region Events

		public event EventHandler<CartridgeProviderSyncEventArgs> SyncCompleted;

		public event EventHandler<CartridgeProviderSyncEventArgs> SyncProgress;

		public event EventHandler<CartridgeProviderSyncAbortEventArgs> SyncAborted;

		public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

		#endregion

		public SkyDriveCartridgeProvider()
		{
			// Tries to link the provider but does not start the login
			// process if no active session has been found.
			BeginLink(false);
		}

		private void RaisePropertyChanged(string propName)
		{
			Device.BeginInvokeOnMainThread(() =>
			{
				if (PropertyChanged != null)
				{
					PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propName));
				}
			});
		}

		#region Timeout
		private void StartTimeoutTimer(TimeSpan timeSpan)
		{
			Timer timer;
			lock (_syncRoot)
			{
				// Creates the timer if it doesn't exist.
				if (_requestTimeout == null)
				{
					// Creates and starts the timer.
					_requestTimeout = new Timer(new TimerCallback(OnTimeoutTimerTick));
				}

				timer = _requestTimeout;
			}

			// The timer should fire once only.
			timer.Change((int)timeSpan.TotalMilliseconds, Timeout.Infinite);
		}

		private void CancelTimeoutTimer()
		{
			lock (_syncRoot)
			{
				// Bye bye timer.
				if (_requestTimeout != null)
				{
					_requestTimeout.Dispose();
					_requestTimeout = null;
				}
			}
		}

		private void OnTimeoutTimerTick(object target)
		{
			// Cancels the timer.
			CancelTimeoutTimer();

			// We are not syncing anymore.
			IsSyncing = false;

			// Raise the event.
			RaiseSyncAbort(true);
		} 
		#endregion

		#region LiveConnect Auth/Login

		public void BeginLink()
		{
			BeginLink(true);
		}

		private void BeginLink(bool autoLogin)
		{			
			// Returns if already linked.
			if (IsLinked)
			{
				return;
			}
			
			// Stores auto-login setting.
			lock (_syncRoot)
			{
				_autoLoginOnInitFail = autoLogin;
			}
			
			// Makes sure the auth client exists.
			if (_authClient == null)
			{
				_authClient = new LiveAuthClient(LiveConnectClientID);
				_authClient.InitializeCompleted += new EventHandler<LoginCompletedEventArgs>(OnAuthClientInitializeCompleted);
				_authClient.LoginCompleted += new EventHandler<LoginCompletedEventArgs>(OnAuthClientLoginCompleted);
			}

			// Starts initializing.
			try
			{
				_authClient.InitializeAsync(_Scopes);
			}
			catch (LiveAuthException ex)
			{
				// Ignores but dumps the exception.
				Geowigo.Utils.DebugUtils.DumpException(ex, dumpOnBugSenseToo: false);
			}
		}

		private void OnAuthClientInitializeCompleted(object sender, LoginCompletedEventArgs e)
		{
			if (e.Status == LiveConnectSessionStatus.Connected)
			{
				// We're online, get the client.
				MakeClientFromSession(e.Session);
			}
			else
			{
				// Checks if we need to auto login.
				bool shouldAutoLogin = false;
				lock (_syncRoot)
				{
					shouldAutoLogin = _autoLoginOnInitFail;
				}

				// If we need to auto-login, do it.
				if (shouldAutoLogin)
				{
					_authClient.LoginAsync(_Scopes);
				}
			}
		}

		private void MakeClientFromSession(LiveConnectSession session)
		{
			// Creates the client.
			_liveClient = new LiveConnectClient(session);

			// Adds event handlers.
			_liveClient.DownloadCompleted += new EventHandler<LiveDownloadCompletedEventArgs>(OnLiveClientDownloadCompleted);
			_liveClient.BackgroundDownloadCompleted += new EventHandler<LiveOperationCompletedEventArgs>(OnLiveClientBackgroundDownloadCompleted);
			_liveClient.GetCompleted += new EventHandler<LiveOperationCompletedEventArgs>(OnLiveClientGetCompleted);
			_liveClient.UploadCompleted += new EventHandler<LiveOperationCompletedEventArgs>(OnLiveClientUploadCompleted);

			// Makes the client download even when on battery, or cellular data scheme.
			_liveClient.BackgroundTransferPreferences = BackgroundTransferPreferences.AllowCellularAndBattery;

			// Attaches downloads that have been running in the background
			// while the app was not active.
			_liveClient.AttachPendingTransfers();

			// Notify we're linked.
			IsLinked = true;
		}

		private void OnAuthClientLoginCompleted(object sender, LoginCompletedEventArgs e)
		{
			if (e.Status == LiveConnectSessionStatus.Connected)
			{
				// We're online, get the client.
				MakeClientFromSession(e.Session);

				// Notify we're linked.
				IsLinked = true;
			}
		}

		#endregion

		#region LiveConnect Sync

		public void BeginSync()
		{
			// Sanity checks.
			if (!IsLinked)
			{
				throw new InvalidOperationException("The SkyDrive provider is not linked.");
			}

			// Makes sure a pending sync is not in progress.
			if (IsSyncing)
			{
				return;
			}
			IsSyncing = true;

			// Creates the folder if it isn't created.
			using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication())
			{
				isf.CreateDirectory(IsoStoreCartridgesPath);
				isf.CreateDirectory(IsoStoreCartridgeContentPath);
			}

			// Sync Step 1: Ask for the list of files from root folder.
			_liveClient.GetAsync("me/skydrive/files?filter=folders", "root");

			// Starts the timeout timer.
			StartTimeoutTimer(GetRequestTimeoutTimeSpan);
		}

		private void BeginDownloadFile(SkyDriveFile file)
		{
			// Adds the file id to the list of currently downloading files.
			lock (_syncRoot)
			{
				_dlFiles.Add(file);
			}

			// The emulator has no support for background download.
			// Peform a direct download instead.
			bool shouldDirectDownload = !IsBackgroundDownloadAllowed || Microsoft.Devices.Environment.DeviceType == Microsoft.Devices.DeviceType.Emulator;
			
			// Starts downloading the cartridge to the isostore.
			string fileAttribute = file.Id + "/content";
			if (shouldDirectDownload)
			{
				// Direct download.
				_liveClient.DownloadAsync(fileAttribute, GetDownloadUserState(file));
			}
			else
			{
				try
				{
					// Tries to perform a background download.
					_liveClient.BackgroundDownloadAsync(
						fileAttribute,
						new Uri(GetTempIsoStorePath(file.Name), UriKind.RelativeOrAbsolute),
						GetDownloadUserState(file)
						);
				}
				catch (Exception)
				{
					// Tries the direct download method.
					_liveClient.DownloadAsync(fileAttribute, GetDownloadUserState(file));
				}
			}
			
		}

		private void EndSyncDownloads()
		{
			// Sync Step 6. 
			// The downloading phase of the sync is over, let's continue with uploads.

			// Cancels everything if no Geowigo or upload folder was found.
			if (_geowigoFolderId == null || _uploadsFolderId == null)
			{
				EndSync();
				return;
			}

			// Makes a list of files to upload.
			List<string> toUpload;
			using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication())
			{
				toUpload = isf.GetAllFiles("/*.gws")
					.Union(isf.GetAllFiles("/*.gwl")
					.Where(s => ("/"+s).StartsWith(IsoStoreCartridgeContentPath)))
					.ToList();
			}

			// Returns if there is nothing to upload.
			if (toUpload.Count < 1)
			{
				EndSync();
				return;
			}

			// Stores the list of files to upload.
			lock (_syncRoot)
			{
				_ulFiles = toUpload;
			}

			// Starts uploading the first file. The next ones will be triggered
			// once it finished uploading.
			string firstFile = _ulFiles[0];
			using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication())
			{
				_currentUlFileStream = isf.OpenFile(firstFile, FileMode.Open, FileAccess.Read, FileShare.Read);
				_liveClient.UploadAsync(_uploadsFolderId, Path.GetFileName(firstFile), _currentUlFileStream, OverwriteOption.Overwrite, firstFile);
			}
		}

		private void EndSync()
		{
			CartridgeProviderSyncEventArgs e;

			// Marks the sync as completed.
			lock (_syncRoot)
			{
				e = _syncEventArgs;
				_syncEventArgs = null;
			}
			IsSyncing = false;

			// Notifies that the sync is finished.
			if (e != null)
			{
				if (SyncCompleted != null)
				{
					SyncCompleted(this, e);
				}
			}
		}

		private string GetDownloadUserState(SkyDriveFile file)
		{
			return file.Id + "|" + file.Name + "|" + file.DownloadDirectory;
		}

		private string GetIsoStorePath(string filename, string directory)
		{
			return String.Format("{0}/{1}", directory, filename);
		}

		private string GetTempIsoStorePath(string filename)
		{
			return String.Format("/shared/transfers/_{0}", filename);
		}

		private void OnLiveClientBackgroundDownloadCompleted(object sender, LiveOperationCompletedEventArgs e)
		{
			// Sync Step 5. The file has already been downloaded to isostore.
			// Just move it to its right location.
			// (This only runs on the device.)

			int filesLeft;
			string dlFilename;
			string originalFilename;
			string dlTargetPath;
			PreProcessDownload(e, out filesLeft, out dlFilename, out originalFilename, out dlTargetPath);

			string filepath = GetIsoStorePath(originalFilename, dlTargetPath);
			if (e.Result != null)
			{
				// Moves the file.
				using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication())
				{
					// Makes sure the directory exists.
					isf.CreateDirectory(dlTargetPath);

					// Removes the destination file if it exists.
					if (isf.FileExists(filepath))
					{
						isf.DeleteFile(filepath);
					}

					// Moves the downloaded file to the right place.
					try
					{
						isf.MoveFile(GetTempIsoStorePath(dlFilename), filepath);
					}
					catch (Exception ex)
					{
						// In case of exception here, do nothing.
						// An attempt to load the file will be done anyway.
						Geowigo.Utils.DebugUtils.DumpException(ex, string.Format("dlFilename={0};targetFilename={1}", dlFilename, filepath), true);
					}
				}
			}

			PostProcessDownload(filepath, filesLeft);
		}

		private void OnLiveClientDownloadCompleted(object sender, LiveDownloadCompletedEventArgs e)
		{
			// Sync Step 5. The file has been downloaded as a memory stream.
			// Write it to its direct location.
			// (This runs on the emulator and on devices where the background
			// download method failed.)

			int filesLeft;
			string dlFilename;
			string originalFilename;
			string dlTargetPath;
			PreProcessDownload(e, out filesLeft, out dlFilename, out originalFilename, out dlTargetPath);

			string filepath = GetIsoStorePath(dlFilename, dlTargetPath);
			if (e.Result != null)
			{
				using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication())
				{
					// Makes sure the directory exists.
					isf.CreateDirectory(dlTargetPath);

					// Creates a file at the right place.
					using (IsolatedStorageFileStream fs = isf.OpenFile(filepath, FileMode.Create))
					{
						e.Result.CopyTo(fs);
					}
				}
			}

			PostProcessDownload(filepath, filesLeft);
		}

		private void OnLiveClientGetCompleted(object sender, LiveOperationCompletedEventArgs e)
		{
			// Cancels the timeout timer.
			CancelTimeoutTimer();
			
			// No result? Nothing to do.
			if (e.Result == null)
			{
				EndSyncDownloads(); 
				return;
			}

			if ("root".Equals(e.UserState))
			{
				// Sync Step 2: We are getting results for the root folder.
				// We need to enumerate through all file entries and find the first
				// folder whose name is "Geowigo".
				// Then, we will ask for file enumerations of this folder.
				// If no folder is found, the sync is over.

				// Enumerates through all the file entries.
				List<object> data = (List<object>)e.Result["data"];
				foreach (IDictionary<string, object> content in data)
				{
					// Is it a folder?
					if ("folder".Equals(content["type"]))
					{
						// Is its name "Geowigo"?
						if ("Geowigo".Equals((string)content["name"], StringComparison.InvariantCultureIgnoreCase))
						{
							// Sync Step 3. Asks for the list of files in this folder.

							// Stores the folder id.
							_geowigoFolderId = (string)content["id"];

							// Asks for the list of files.
							_liveClient.GetAsync((string)content["id"] + "/files", "geowigo");

							// Starts the timeout timer.
							StartTimeoutTimer(GetRequestTimeoutTimeSpan);

							// Nothing more to do.
							return;
						}
					}
				}
				
				// If we are here, it means that the Geowigo folder was not found.
				// The sync ends.
				EndSyncDownloads();
				return;
			}
			else if ("geowigo".Equals(e.UserState))
			{
				// Sync Step 4: We are getting results for the Geowigo folder.
				// We need to enumerate through all files and download each GWC
				// or GWS file in the background.

				List<object> data = (List<object>)e.Result["data"];

				// Enumerates through all the file entries.
				List<SkyDriveFile> cartFiles = new List<SkyDriveFile>();
				List<SkyDriveFile> extraFiles = new List<SkyDriveFile>();
				foreach (IDictionary<string, object> content in data)
				{
					// Is it a cartridge file?
					string name = (string)content["name"];
					string lname = name.ToLower();
					object type = content["type"];
					if ("file".Equals(type))
					{
						if (lname.EndsWith(".gwc"))
						{
							// Adds the file to the list of cartridges.
							cartFiles.Add(new SkyDriveFile((string)content["id"], name, IsoStoreCartridgesPath));
						}
						else if (lname.EndsWith(".gws"))
						{
							// Adds the file to the list of extra files.
							extraFiles.Add(new SkyDriveFile((string)content["id"], name, IsoStoreCartridgeContentPath));
						}
					}
					else if ("folder".Equals(type) && UploadFolderName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
					{
						// We found the uploads folder.
						// Stores its id.
						_uploadsFolderId = (string)content["id"];
					}
				}

				// Creates the list of cartridges in the isostore that do not exist 
				// on SkyDrive anymore.
				// These will be removed. Extra files are not removed even if they are not
				// on SkyDrive anymore.
				List<string> isoStoreFiles;
				List<string> toRemoveFiles;
				List<string> isoStoreExtraFiles;
				using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication())
				{
					isoStoreExtraFiles = isf.GetAllFiles(IsoStoreCartridgeContentPath + "/*.gws");
					
					isoStoreFiles =
						isf
						.GetFileNames(GetIsoStorePath("*.gwc", IsoStoreCartridgesPath))
						.Select(s => IsoStoreCartridgesPath + "/" + s)
						.ToList();

					toRemoveFiles =
						isoStoreFiles
						.Where(s => !cartFiles.Any(sd => sd.Name == System.IO.Path.GetFileName(s)))
						.ToList();
				}

				// Clears from the list of extra files to download those which
				// are already somewhere in the isolated storage.
				foreach (SkyDriveFile ef in extraFiles.ToList())
				{
					if (isoStoreExtraFiles.Any(s => s.EndsWith("/" + ef.Name, StringComparison.InvariantCultureIgnoreCase)))
					{
						// The file needn't be downloaded.
						extraFiles.Remove(ef);
					}
				} 

				// Creates the list of cartridges and extra files that are on SkyDrive but
				// not in the isolated storage.
				List<SkyDriveFile> toDlFiles = cartFiles
					.Where(sd => !isoStoreFiles.Contains(GetIsoStorePath(sd.Name, sd.DownloadDirectory)))
					.Union(extraFiles)
					.ToList();

				// Bakes an event for when the sync will be over.
				lock (_syncRoot)
				{
					_syncEventArgs = new CartridgeProviderSyncEventArgs()
					{
						AddedFiles = toDlFiles
							.Select(sd => GetIsoStorePath(sd.Name, sd.DownloadDirectory))
							.ToList(),
						ToRemoveFiles = toRemoveFiles
					};
				}

				// Sends a progress event for removing the files marked
				// to be removed.
				if (toRemoveFiles.Count > 0)
				{
					RaiseSyncProgress(toRemoveFiles: toRemoveFiles);
				}

				// Starts downloading all new files, or terminate
				// the sync if no new file is to be downloaded.
				if (toDlFiles.Count > 0)
				{
					toDlFiles.ForEach(sd => BeginDownloadFile(sd));
				}
				else
				{
					EndSyncDownloads();
				}
			}
		}

		private void OnLiveClientUploadCompleted(object sender, LiveOperationCompletedEventArgs e)
		{
			// Sync Step 7. An upload has finished or didn't work.
			// Try to upload the next pending file or finish the whole process.
			
			// Removes the file from the current list of pending uploads.
			string nextFile = null;
			lock (_syncRoot)
			{
				// Removes the completed upload.
				_ulFiles.Remove((string)e.UserState);

				// Gets the next one.
				nextFile = _ulFiles.FirstOrDefault();
			}

			// Disposes the current file stream if any.
			if (_currentUlFileStream != null)
			{
				_currentUlFileStream.Dispose();
				_currentUlFileStream = null;
			}

			// If there is nothing more to do, the sync is over.
			if (nextFile == null)
			{
				EndSync();
				return;
			}

			// Uploads the next file.
			using (IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication())
			{
				_currentUlFileStream = isf.OpenFile(nextFile, FileMode.Open, FileAccess.Read, FileShare.Read);
				_liveClient.UploadAsync(_uploadsFolderId, Path.GetFileName(nextFile), _currentUlFileStream, OverwriteOption.Overwrite, nextFile);
			}
		}

		private void ParseDownloadEventArgs(AsyncCompletedEventArgs e, out string fileId, out string dlFilename, out string originalFilename, out string dlTargetPath)
		{
			string[] ustate = (e.UserState ?? "|").ToString().Split(new char[] { '|' });
			fileId = ustate[0];
			originalFilename = ustate[1];
			dlTargetPath = ustate[2];
			dlFilename = originalFilename;

			// Gets the downloaded filename from the event args if they support it.
			if (e is LiveOperationCompletedEventArgs)
			{
				object rawDlLoc;
				var result = ((LiveOperationCompletedEventArgs)e).Result;
				if (result != null && result.TryGetValue("downloadLocation", out rawDlLoc))
				{
					// The download location given by these event args is corrupted by
					// inappropriately inserted escape characters.
					// So let's just hack our way through it and figure out the actual
					// filename of the file that was saved in the isostore.
					dlFilename = ((string)rawDlLoc)
						.Split(new string[] { "shared\transfers_" }, StringSplitOptions.RemoveEmptyEntries)
						.LastOrDefault();
				}
			}
		}

		private void PostProcessDownload(string filepath, int filesLeft)
		{
			// Raise information about this progress.
			RaiseSyncProgress(addedFiles: new string[] { filepath });

			// If no more files are pending download, the sync is over!
			if (filesLeft == 0)
			{
				EndSyncDownloads();
			}
		}

		private void PreProcessDownload(AsyncCompletedEventArgs e, out int filesLeft, out string dlFilename, out string originalFilename, out string dlTargetPath)
		{
			// Removes the file from the queue of files to download.
			lock (_syncRoot)
			{
				// Parses the user state.
				string fileId;
				ParseDownloadEventArgs(e, out fileId, out dlFilename, out originalFilename, out dlTargetPath);

				// Removes the file if it is registered.
				SkyDriveFile file = _dlFiles.FirstOrDefault(sd => sd.Id.Equals(fileId));
				if (file != null)
				{
					_dlFiles.Remove(file);
				}

				// Gets how many files are pending completion.
				filesLeft = _dlFiles.Count;
			}
		}

		private void RaiseSyncProgress(IEnumerable<string> addedFiles = null, IEnumerable<string> toRemoveFiles = null)
		{
			// Raises an event for this file path.
			if (SyncProgress != null)
			{
				SyncProgress(this, new CartridgeProviderSyncEventArgs()
				{
					AddedFiles = addedFiles ?? new string[] {},
					ToRemoveFiles = toRemoveFiles ?? new string[] { }
				});
			}
		}

		private void RaiseSyncAbort(bool hasTimeout)
		{
			Deployment.Current.Dispatcher.BeginInvoke(() =>
			{
				if (SyncAborted != null)
				{
					SyncAborted(this, new CartridgeProviderSyncAbortEventArgs()
					{
						HasTimedOut = hasTimeout
					});
				}
			});
		}

		#endregion

	}
}
