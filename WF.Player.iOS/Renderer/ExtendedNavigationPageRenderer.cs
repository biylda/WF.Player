// WF.Player - A Wherigo Player which use the Wherigo Foundation Core.
// Copyright (C) 2012-2014  Dirk Weltz <mail@wfplayer.com>
//
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
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;
using WF.Player.iOS;
using UIKit;
using CoreGraphics;
using System.Linq;
using WF.Player.Services.Settings;

[assembly: ExportRendererAttribute(typeof(WF.Player.Controls.ExtendedNavigationPage), typeof(WF.Player.Controls.iOS.ExtendedNavigationPageRenderer))]

namespace WF.Player.Controls.iOS
{
	public class ExtendedNavigationPageRenderer : NavigationRenderer
	{
		private bool animation;
		private UIBarButtonItem leftIconBarButtonItem;

		public ExtendedNavigationPageRenderer()
		{
			animation = true;
		}

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();

			if (!animation)
			{
				CreateBackButton();
			}

			UpdateColors();
		}

		public override void ViewWillAppear(bool animated)
		{
			base.ViewWillAppear(animated);

			// Use this for the right side navigation bar buttons
//			UIBarButtonItem.Appearance.TintColor = App.Colors.Tint.ToUIColor();
//			// Use this for the left side navigation bar buttons
//			NavigationBar.TintColor = App.Colors.Tint.ToUIColor();

//			NavigationBar.BarStyle = Settings.IsDarkTheme ? UIBarStyle.Black : UIBarStyle.Default;

			UpdateColors();
		}

		protected override void OnElementChanged(VisualElementChangedEventArgs e)
		{
			base.OnElementChanged(e);

			((ExtendedNavigationPage)Element).ViewController = this.ViewController;

			animation = ((ExtendedNavigationPage)Element).Animation;

			((NavigationPage)Element).PropertyChanged += OnElementPropertyChanged;

			if (!animation)
			{
				((NavigationPage)Element).Pushed += HandlePushed;
				((NavigationPage)Element).Popped += HandlePopped;
			}
		}

		private void OnElementPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "ShowBackButton" && !animation)
			{
				var item = NavigationBar.Items.Last();

				if (((ExtendedNavigationPage)Element).ShowBackButton)
				{
					if (leftIconBarButtonItem == null)
					{
						CreateBackButton();
					}

					item.LeftItemsSupplementBackButton = false;
					item.LeftBarButtonItem = leftIconBarButtonItem;
				}
				else
				{
					item.LeftBarButtonItem = null;
				}
			}

			if (e.PropertyName == "CurrentPage")
			{
				// Is it the first page
				if (((ExtendedNavigationPage)Element).CurrentPage is CartridgeListPage)
				{
					// Set values for the NavigationBar
					UpdateColors();
				}
			}
		}

		private void HandlePushed(object sender, NavigationEventArgs e)
		{
			var item = NavigationBar.Items.Last();

			if (((ExtendedNavigationPage)Element).ShowBackButton)
			{
				if (leftIconBarButtonItem == null)
				{
					CreateBackButton();
				}

				item.LeftItemsSupplementBackButton = false;
				item.LeftBarButtonItem = leftIconBarButtonItem;
			}
		}

		private void HandlePopped(object sender, NavigationEventArgs e)
		{
			var item = NavigationBar.Items.Last();

			if (((ExtendedNavigationPage)Element).ShowBackButton)
			{
				if (leftIconBarButtonItem == null)
				{
					CreateBackButton();
				}

				item.LeftItemsSupplementBackButton = false;
				item.LeftBarButtonItem = leftIconBarButtonItem;
			}
		}

		private void HandleBackButton(object sender, EventArgs e)
		{
			var navigationPage = (NavigationPage)Element;

			if (App.GameNavigation != null)
			{
				if (App.Game != null)
				{
					// We are in the game
					BeginInvokeOnMainThread(() => App.Game.ShowScreen(ScreenType.Last, null));
				}
				else
				{
					// We are on the check location screen
					// Remove active screen
					App.GameNavigation.CurrentPage.Navigation.PopModalAsync();
					App.GameNavigation = null;
				}
			}
			else
			{
				navigationPage.PopAsync(animation);
			}
		}

		private void CreateBackButton()
		{
			if (NavigationBar.Items.Count() == 0)
			{
				return;
			}

			// Handle new back button without animation
			UINavigationItem item = NavigationBar.Items[0];

			UIImage icon = UIImage.FromBundle("IconBack.png");

			var button = UIButton.FromType(UIButtonType.System);

			button.Bounds = new CGRect(0, 0, icon.Size.Width, icon.Size.Height);
			button.SetImage(icon, UIControlState.Normal);
			button.ImageEdgeInsets = new UIEdgeInsets(0.5f, -8, -0.5f, 8);

			button.AddTarget(HandleBackButton, UIControlEvent.TouchUpInside);
			leftIconBarButtonItem = new UIBarButtonItem(button);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				var navigationPage = (NavigationPage)Element;

				navigationPage.PropertyChanged -= OnElementPropertyChanged;
				navigationPage.Pushed -= HandlePushed;
				navigationPage.Popped -= HandlePopped;

				if (leftIconBarButtonItem != null)
				{
					leftIconBarButtonItem.Dispose();
					leftIconBarButtonItem = null;
				}
			}

			base.Dispose(disposing);
		}

		private void UpdateColors()
		{
			// Set background
			((ExtendedNavigationPage)Element).BackgroundColor = App.Colors.Background;
			((ExtendedNavigationPage)Element).BarTextColor = App.Colors.BarText;
			NavigationBar.BarTintColor = App.Colors.Bar.ToUIColor();
			// Use this for the left side navigation bar buttons
			NavigationBar.TintColor = App.Colors.Tint.ToUIColor();
		}
	}
}
