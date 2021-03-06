//
// MonoDevelopStatusBar.cs
//
// Author:
//   Mike Krüger <mkrueger@novell.com>
//
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using Gtk;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Components.Docking;
using MonoDevelop.Components;
using MonoDevelop.Components.AtkCocoaHelper;

namespace MonoDevelop.Ide
{
	class MonoDevelopStatusBar : Gtk.HBox
	{
		Gtk.EventBox resizeGrip = new Gtk.EventBox ();

		const int ResizeGripWidth = 14;

		internal MonoDevelopStatusBar ()
		{
			BorderWidth = 0;
			Spacing = 0;
			HasResizeGrip = true;

			Accessible.Role = Atk.Role.Filler;

			HeaderBox hb = new HeaderBox (1, 0, 0, 0);
			hb.Accessible.Role = Atk.Role.Filler;
			hb.StyleSet += (o, args) => {
				hb.BorderColor = Styles.DockSeparatorColor.ToGdkColor ();
				hb.BackgroundColor = Styles.DockBarBackground.ToGdkColor ();
			};
			var mainBox = new HBox ();
			mainBox.Accessible.Role = Atk.Role.Filler;
			var alignment = new Alignment (0f, 0f, 0f, 0f);
			alignment.Accessible.Role = Atk.Role.Filler;
			mainBox.PackStart (alignment, true, true, 0);
			hb.Add (mainBox);
			hb.ShowAll ();
			PackStart (hb, true, true, 0);
			
			// Dock area
			
			CustomFrame dfr = new CustomFrame (0, 0, 1, 0);
			dfr.Accessible.Role = Atk.Role.Filler;
			dfr.StyleSet += (o, args) => {
				dfr.BorderColor = Styles.DockSeparatorColor.ToGdkColor ();
			};
			dfr.ShowAll ();
			DefaultWorkbench wb = (DefaultWorkbench)IdeApp.Workbench.RootWindow;
			var dockBar = wb.DockFrame.ExtractDockBar (PositionType.Bottom);
			dockBar.AlignToEnd = true;
			dockBar.ShowBorder = false;
			dockBar.NoShowAll = true;
			dfr.Add (dockBar);
			mainBox.PackStart (dfr, false, false, 0);

			// Resize grip

			resizeGrip.Accessible.SetRole (AtkCocoa.Roles.AXGrowArea);
			resizeGrip.WidthRequest = ResizeGripWidth;
			resizeGrip.HeightRequest = 0;
			resizeGrip.VisibleWindow = false;
			mainBox.PackStart (resizeGrip, false, false, 0);

			resizeGrip.ButtonPressEvent += delegate (object o, ButtonPressEventArgs args) {
				if (args.Event.Button == 1) {
					GdkWindow.BeginResizeDrag (Gdk.WindowEdge.SouthEast, (int)args.Event.Button, (int)args.Event.XRoot, (int)args.Event.YRoot, args.Event.Time);
				}
			};

			this.ShowAll ();

//			// todo: Move this to the CompletionWindowManager when it's possible.
//			StatusBarContext completionStatus = null;
//			CompletionWindowManager.WindowShown += delegate {
//				CompletionListWindow wnd = CompletionWindowManager.Wnd;
//				if (wnd != null && wnd.List != null && wnd.List.CategoryCount > 1) {
//					if (completionStatus == null)
//						completionStatus = CreateContext ();
//					completionStatus.ShowMessage (string.Format (GettextCatalog.GetString ("To toggle categorized completion mode press {0}."), IdeApp.CommandService.GetCommandInfo (Commands.TextEditorCommands.ShowCompletionWindow).AccelKey));
//				}
//			};
		}

		[Obsolete]
		public void ShowCaretState (int line, int column, int selectedChars, bool isInInsertMode)
		{
		}

		[Obsolete]
		public void ClearCaretState ()
		{
		}

		bool hasResizeGrip;
		public bool HasResizeGrip {
			get { return hasResizeGrip; }
			set { hasResizeGrip = value; resizeGrip.Visible = hasResizeGrip; }
		}

		Gdk.Rectangle GetGripRect ()
		{
			Gdk.Rectangle rect = new Gdk.Rectangle (0, 0, ResizeGripWidth, Allocation.Height);
			if (rect.Width > Allocation.Width)
				rect.Width = Allocation.Width;
			rect.Y = Allocation.Y + Allocation.Height - rect.Height;
			if (Direction == TextDirection.Ltr)
				rect.X = Allocation.X + Allocation.Width - rect.Width;
			else
				rect.X = Allocation.X + Style.XThickness;
			return rect;
		}
		
		protected override bool OnExposeEvent (Gdk.EventExpose evnt)
		{
			bool ret = base.OnExposeEvent (evnt);
			if (HasResizeGrip) {
				Gdk.Rectangle rect = GetGripRect ();
				int w = rect.Width - Style.Xthickness;
				int h = Allocation.Height - Style.YThickness;
				if (h < 18 - Style.YThickness) h = 18 - Style.YThickness;
				Gdk.WindowEdge edge = Direction == TextDirection.Ltr ? Gdk.WindowEdge.SouthEast : Gdk.WindowEdge.SouthWest;
				Gtk.Style.PaintResizeGrip (Style, GdkWindow, State, evnt.Area, this, "statusbar", edge, rect.X, rect.Y, w, h);
			}
 			return ret;
		}
	}
}
