﻿// <copyright file="MainActivity.cs" company="Wherigo Foundation">
//   WF.Player - A Wherigo Player which use the Wherigo Foundation Core.
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
using Android.Content.Res;
using Android.Util;
using Xamarin.Forms;
using WF.Player.Services.Device;

namespace WF.Player.Droid
{
	using Android.App;
	using Android.Content;
	using Android.OS;
	using Vernacular;
	using WF.Player.Droid.Services.Core;
	using WF.Player.Services.Settings;
	using WF.Player.Services.UserDialogs;
	using Xamarin.Forms.Platform.Android;

	[Activity (Label = "WF.Player", 
	ScreenOrientation = global::Android.Content.PM.ScreenOrientation.Portrait,
	ConfigurationChanges=global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.ScreenSize
	)]
	[IntentFilter(new string[] { Intent.ActionView }, Categories = new string[] { Intent.CategoryDefault })]
	public class MainActivity : FormsApplicationActivity
	{
		protected override void OnCreate (Bundle bundle)
		{
			#if __HOCKEYAPP__

			//Register the Update Manager
			HockeyApp.UpdateManager.Register (this, "acc974c814cad87cf7e01b8e8c4d5ece");

			// Register the Feedback Manager
			HockeyApp.FeedbackManager.Register (this, "acc974c814cad87cf7e01b8e8c4d5ece");

			#endif

			#if __INSIGHTS__

			Xamarin.Insights.Initialize("7cc331e1fae1f21a7646fb5df552ff8213bd8bc9", this);

			#endif

			// Init Xamarin.Forms
			Xamarin.Forms.Forms.Init (this, bundle);
			Xamarin.FormsMaps.Init(this, bundle);

			UserDialogs.Init(this);

			if (Settings.IsDarkTheme) 
			{
				this.SetTheme (Resource.Style.AppTheme_Dark);
			}
			else
			{
				this.SetTheme (Resource.Style.AppTheme_Light);
			}

			base.OnCreate (bundle);


			// Update language
			DependencyService.Get<ILanguageSetter>().Update();

//			if (!string.IsNullOrEmpty(Settings.Current.GetValueOrDefault<string>(Settings.LanguageKey, string.Empty)))
//			{
//				Resources standardResources = this.Resources;
//				AssetManager assets = standardResources.Assets;
//				DisplayMetrics metrics = standardResources.DisplayMetrics;
//				Configuration config = new Configuration(standardResources.Configuration);
//				config.Locale = new Java.Util.Locale(Settings.Current.GetValueOrDefault<string>(Settings.LanguageKey, string.Empty));
//				Resources defaultResources = new Resources(assets, metrics, config);
//
//				Catalog.Implementation = new AndroidCatalog(defaultResources, typeof(Resource.String));
//			}
//			else
//			{
//				Catalog.Implementation = new AndroidCatalog(Resources, typeof(Resource.String));
//			}

			App.PathCartridges = App.PathForCartridges;
			App.PathDatabase = App.PathForCartridges;

			// Create Xamarin.Forms App and load the first page
			LoadApplication(new App(new AndroidPlatformHelper()));

			// Has intent informations for a file to open
			string action = this.Intent.Action;

			if (action == Intent.ActionView)
			{
				var uriData = this.Intent.Data;
				var uriPath = uriData.EncodedPath;
				// now you call whatever function your app uses 
				// to consume the txt file whose location you now know 
			}
		}

		/// <summary>
		/// Raises the activity result event.
		/// </summary>
		/// <Docs>The integer request code originally supplied to
		///  startActivityForResult(), allowing you to identify who this
		///  result came from.</Docs>
		/// <param name="requestCode">Request code.</param>
		/// <param name="resultCode">Result code.</param>
		/// <param name="data">An Intent, which can return result data to the caller
		///  (various data can be attached to Intent "extras").</param>
		protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
		{
			if (resultCode == Result.Ok) {
				if (App.Colors.IsDarkTheme) {
					this.SetTheme (Resource.Style.AppTheme_Dark);
				} else {
					this.SetTheme (Resource.Style.AppTheme_Light);
				}

				this.Recreate ();
			}

			base.OnActivityResult (requestCode, resultCode, data);
		}

		protected override void OnResume ()
		{
			base.OnResume ();

			#if __HOCKEYAPP__

			// Register for Crash detection / handling
			// You should do this in your main activity
			HockeyApp.CrashManager.Register (this, "acc974c814cad87cf7e01b8e8c4d5ece");

			//Start Tracking usage in this activity
			HockeyApp.Tracking.StartUsage (this);

			#endif
		}

		protected override void OnPause ()
		{
			#if __HOCKEYAPP__

			//Stop Tracking usage in this activity
			HockeyApp.Tracking.StopUsage (this);

			#endif

			base.OnPause ();
		}

		/// <Docs>The menu item that was selected.</Docs>
		/// <returns>To be added.</returns>
		/// <para tool="javadoc-to-mdoc">This hook is called whenever an item in your options menu is selected.
		///  The default implementation simply returns false to have the normal
		///  processing happen (calling the item's Runnable or sending a message to
		///  its Handler as appropriate). You can use this method for any items
		///  for which you would like to do processing without those other
		///  facilities.</para>
		/// <summary>
		/// Raises the options item selected event.
		/// </summary>
		/// <param name="item">Item.</param>
		public override bool OnOptionsItemSelected(Android.Views.IMenuItem item)
		{
			// Handle back button for list pages, because of own back buttons
			if (App.GameNavigation != null)
			{
				// We ar in the game
				if (App.GameNavigation.CurrentPage is GameMainView && item.ItemId == global::Android.Resource.Id.Home)
				{
					App.Game.ShowScreen(ScreenType.Last, null);

					return true;
				}

				if (App.GameNavigation.CurrentPage is GameCheckLocationView && item.ItemId == global::Android.Resource.Id.Home)
				{
					// We are on the check location screen
					// Remove active screen
					App.GameNavigation.CurrentPage.Navigation.PopModalAsync();
					App.GameNavigation = null;

					return true;
				}
			}

			return base.OnOptionsItemSelected(item);
		}
	}
}
