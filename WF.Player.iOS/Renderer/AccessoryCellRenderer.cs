﻿// WF.Player - A Wherigo Player which use the Wherigo Foundation Core.
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
using Xamarin.Forms.Platform.iOS;
using Xamarin.Forms;
using WF.Player.Controls;
using WF.Player.Controls.iOS;
using MonoTouch.UIKit;

[assembly: ExportRendererAttribute (typeof (AccessoryCell), typeof (AccessoryCellRenderer))]

namespace WF.Player.Controls.iOS
{
	public class AccessoryCellRenderer : TextCellRenderer
	{

		public override MonoTouch.UIKit.UITableViewCell GetCell (Cell item, MonoTouch.UIKit.UITableView tv)
		{
			var cell = base.GetCell (item, tv);

			cell.Accessory = MonoTouch.UIKit.UITableViewCellAccessory.DisclosureIndicator;
			cell.BackgroundColor = UIColor.Clear;

			return cell;
		}
	}
}

