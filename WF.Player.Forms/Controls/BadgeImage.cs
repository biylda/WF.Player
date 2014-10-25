﻿// <copyright file="BadgeImage.cs" company="Wherigo Foundation">
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

namespace WF.Player.Controls
{
	using System;
	using Xamarin.Forms;

	/// <summary>
	/// Badge image.
	/// </summary>
	public class BadgeImage : Image
	{
		/// <summary>
		/// The name of the number property.
		/// </summary>
		public const string NumberPropertyName = "Number";

		#region Number

		/// <summary>
		/// Bindable number property.
		/// </summary>
		public static readonly BindableProperty NumberProperty = BindableProperty.Create<BadgeImage, int>(p => p.Number, 0, BindingMode.OneWay);

		/// <summary>
		/// Gets or sets the number.
		/// </summary>
		/// <value>The number.</value>
		public int Number
		{
			get
			{
				return (int)GetValue(NumberProperty);
			}

			set
			{
				SetValue(NumberProperty, value);
			}
		}

		#endregion
	}
}
