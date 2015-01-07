﻿// <copyright file="MapViewModel.cs" company="Wherigo Foundation">
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
using Vernacular;
using Acr.XamForms.UserDialogs;

namespace WF.Player
{
	using System;
	using System.Windows.Input;
	using Xamarin.Forms;
	using Xamarin.Forms.Maps;
	using WF.Player.Controls;

	public class MapViewModel : BaseViewModel
	{
		public static string PositionPropertyName = "Position";
		public static string MapOrientationPropertyName = "MapOrientation";
		public static string MapOrientationSourcePropertyName = "MapOrientationSource";
		public static string CommandButtonCenterPressedPropertyName = "CommandButtonCenterPressed";
		public static string CommandButtonTypePressedPropertyName = "CommandButtonTypePressed";

		private ExtendedMap map;

		private MapSpan visibleRegion;

		/// <summary>
		/// The position.
		/// </summary>
		private WF.Player.Services.Geolocation.Position position;

		public MapViewModel()
		{
//			Position = App.GPS.LastKnownPosition;
			visibleRegion = MapSpan.FromCenterAndRadius(new Position(0, 0), Distance.FromMeters(1000));

			MapOrientation = MapOrientation.NorthUp;
		}

		#region Properties

		public ExtendedMap Map
		{
			get
			{
				return map;
			}

			set
			{
				if (map != value)
				{
					if (map != null)
					{
						map.PropertyChanged -= HandlePropertyChanged;
					}
					map = value;
					map.PropertyChanged += HandlePropertyChanged;
//					Position = App.GPS.LastKnownPosition;
//					map.MoveToRegion(visibleRegion);
				}
			}
		}

		public MapSpan VisibleRegion
		{
			get
			{
				if (map != null)
				{
					visibleRegion = map.VisibleRegion;
				}

				return visibleRegion;
			}

			set
			{
				if (visibleRegion != value)
				{
					visibleRegion = value;
					map.MoveToRegion(value);
				}
			}
		}

		public MapOrientation MapOrientation 
		{ 
			get
			{
				return map.MapOrientation;
			}

			private set
			{
				if (map != null)
				{
					map.MapOrientation = value;
				}
			}
		}

		public string MapOrientationSource
		{
			get
			{
				if (MapOrientation == MapOrientation.NorthUp)
				{
					return "IconMapNorth.png";
				}
				else
				{
					return "IconMapOrientation.png";
				}
			}
		}

		#region Position

		/// <summary>
		/// Gets Position from the actual location.
		/// </summary>
		/// <value>The Position.</value>
		public WF.Player.Services.Geolocation.Position Position
		{
			get
			{
				return this.position;
			}

			internal set
			{
				SetProperty<WF.Player.Services.Geolocation.Position>(ref this.position, value, PositionPropertyName);
			}
		}

		#endregion

		#endregion

		#region Commands

		public ICommand CommandButtonCenterPressed
		{
			get
			{
				return new Command(ButtonCenterPressed);
			}
		}

		public ICommand CommandButtonTypePressed
		{
			get
			{
				return new Command(ButtonTypePressed);
			}
		}

		public ICommand CommandButtonOrientationPressed
		{
			get
			{
				return new Command(ButtonOrientationPressed);
			}
		}

		#endregion

		private void HandlePropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "VisibleRegion" && visibleRegion != null)
			{
				map.MoveToRegion(visibleRegion);
				// TODO: Remove?
				CalculateBoundingCoordinates(map.VisibleRegion);
			}
		}

		private void ButtonCenterPressed(object o)
		{
			App.Click();

			var cfg = new Acr.XamForms.UserDialogs.ActionSheetConfig().SetTitle(Catalog.GetString("Focus on"));

			cfg.Add(Catalog.GetString("Current Location"), () => HandleCenterLocation());
			cfg.Add(Catalog.GetString("Gamefield"), () => HandleCenterGamefield());
			cfg.Add(Catalog.GetString("Both"), () => HandleCenterBoth());

			cfg.Cancel = new Acr.XamForms.UserDialogs.ActionSheetOption(Catalog.GetString("Cancel"), App.Click);

			DependencyService.Get<Acr.XamForms.UserDialogs.IUserDialogService>().ActionSheet(cfg);
		}

		private void HandleCenterLocation()
		{
			if (App.GPS.LastKnownPosition != null)
			{
				visibleRegion = MapSpan.FromCenterAndRadius(new Position(App.GPS.LastKnownPosition.Latitude, App.GPS.LastKnownPosition.Longitude), Distance.FromMeters(1000));
			}
			else
			{
				visibleRegion = MapSpan.FromCenterAndRadius(new Position(0, 0), Distance.FromMeters(1000));
			}

			map.MoveToRegion(visibleRegion);
		}

		private void HandleCenterGamefield()
		{
			var bounds = App.Game.Bounds;

			if (bounds == null)
			{
				return;
			}

			visibleRegion = new MapSpan(new Xamarin.Forms.Maps.Position(bounds.Center.Latitude, bounds.Center.Longitude), 
				Math.Abs(bounds.Top - bounds.Bottom) * 1.1, 
				Math.Abs(bounds.Right - bounds.Left) * 1.1);

			map.MoveToRegion(visibleRegion);
		}

		private void HandleCenterBoth()
		{
			var bounds = App.Game.Bounds;

			if (bounds == null)
			{
				return;
			}

			if (App.GPS.LastKnownPosition != null)
			{
				bounds.Inflate(new WF.Player.Core.ZonePoint(App.GPS.LastKnownPosition.Latitude, App.GPS.LastKnownPosition.Longitude, 0));
			}

			visibleRegion = new MapSpan(new Xamarin.Forms.Maps.Position(bounds.Center.Latitude, bounds.Center.Longitude), Math.Abs(bounds.Top - bounds.Bottom) * 1.1, Math.Abs(bounds.Right - bounds.Left) * 1.1);

			map.MoveToRegion(visibleRegion);
		}

		private void ButtonTypePressed(object o)
		{
			App.Click();

			var cfg = new ActionSheetConfig();
			cfg.SetTitle(Vernacular.Catalog.GetString("Maptype"));
			cfg.Add(Vernacular.Catalog.GetString("Street"), () => HandleMapTypeSelection(MapType.Street));
			cfg.Add(Vernacular.Catalog.GetString("Satellite"), () => HandleMapTypeSelection(MapType.Satellite));
			cfg.Add(Vernacular.Catalog.GetString("Hybrid"), () => HandleMapTypeSelection(MapType.Hybrid));
			cfg.Cancel = new ActionSheetOption(Vernacular.Catalog.GetString("Cancel"), App.Click);

			DependencyService.Get<Acr.XamForms.UserDialogs.IUserDialogService>().ActionSheet(cfg);
		}

		private void ButtonOrientationPressed(object o)
		{
			App.Click();

			MapOrientation = MapOrientation == MapOrientation.NorthUp ? MapOrientation.HeadingUp : MapOrientation.NorthUp;

			NotifyPropertyChanged(MapOrientationSourcePropertyName);
		}

		private void HandleMapTypeSelection(MapType type)
		{
			App.Click();

			map.MapType = type;
		}

		/// <summary>
		/// In response to this forum question http://forums.xamarin.com/discussion/22493/maps-visibleregion-bounds
		/// Useful if you need to send the bounds to a web service or otherwise calculate what
		/// pins might need to be drawn inside the currently visible viewport.
		/// </summary>
		private static void CalculateBoundingCoordinates (MapSpan region)
		{
			// WARNING: I haven't tested the correctness of this exhaustively!
			var center = region.Center;
			var halfheightDegrees = region.LatitudeDegrees / 2;
			var halfwidthDegrees = region.LongitudeDegrees / 2;

			var left = center.Longitude - halfwidthDegrees;
			var right = center.Longitude + halfwidthDegrees;
			var top = center.Latitude + halfheightDegrees;
			var bottom = center.Latitude - halfheightDegrees;

			// Adjust for Internation Date Line (+/- 180 degrees longitude)
			if (left < -180) left = 180 + (180 + left);
			if (right > 180) right = (right - 180) - 180;

			// I don't wrap around north or south; I don't think the map control allows this anyway
			Console.WriteLine ("Bounding box:");
			Console.WriteLine ("            " + top);
			Console.WriteLine (" " + left + " " + right);
			Console.WriteLine ("            " + bottom);
		}
	}
}
