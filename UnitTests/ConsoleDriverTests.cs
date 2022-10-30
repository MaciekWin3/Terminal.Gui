﻿using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;
using Terminal.Gui.Views;
using Xunit;
using Xunit.Abstractions;

// Alias Console to MockConsole so we don't accidentally use Console
using Console = Terminal.Gui.FakeConsole;

namespace Terminal.Gui.ConsoleDrivers {
	public class ConsoleDriverTests {
		readonly ITestOutputHelper output;

		public ConsoleDriverTests (ITestOutputHelper output)
		{
			this.output = output;
		}

		[Fact]
		public void Init_Inits ()
		{
			var driver = new FakeDriver ();
			Application.Init (driver, new FakeMainLoop (() => FakeConsole.ReadKey (true)));
			driver.Init (() => { });

			Assert.Equal (80, Console.BufferWidth);
			Assert.Equal (25, Console.BufferHeight);

			// MockDriver is always 80x25
			Assert.Equal (Console.BufferWidth, driver.Cols);
			Assert.Equal (Console.BufferHeight, driver.Rows);
			driver.End ();

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		[Fact]
		public void End_Cleans_Up ()
		{
			var driver = new FakeDriver ();
			Application.Init (driver, new FakeMainLoop (() => FakeConsole.ReadKey (true)));
			driver.Init (() => { });

			FakeConsole.ForegroundColor = ConsoleColor.Red;
			Assert.Equal (ConsoleColor.Red, Console.ForegroundColor);

			FakeConsole.BackgroundColor = ConsoleColor.Green;
			Assert.Equal (ConsoleColor.Green, Console.BackgroundColor);
			driver.Move (2, 3);
			Assert.Equal (2, Console.CursorLeft);
			Assert.Equal (3, Console.CursorTop);

			driver.End ();
			Assert.Equal (0, Console.CursorLeft);
			Assert.Equal (0, Console.CursorTop);
			Assert.Equal (ConsoleColor.Gray, Console.ForegroundColor);
			Assert.Equal (ConsoleColor.Black, Console.BackgroundColor);

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		[Fact]
		public void SetColors_Changes_Colors ()
		{
			var driver = new FakeDriver ();
			Application.Init (driver, new FakeMainLoop (() => FakeConsole.ReadKey (true)));
			driver.Init (() => { });
			Assert.Equal (ConsoleColor.Gray, Console.ForegroundColor);
			Assert.Equal (ConsoleColor.Black, Console.BackgroundColor);

			Console.ForegroundColor = ConsoleColor.Red;
			Assert.Equal (ConsoleColor.Red, Console.ForegroundColor);

			Console.BackgroundColor = ConsoleColor.Green;
			Assert.Equal (ConsoleColor.Green, Console.BackgroundColor);

			Console.ResetColor ();
			Assert.Equal (ConsoleColor.Gray, Console.ForegroundColor);
			Assert.Equal (ConsoleColor.Black, Console.BackgroundColor);
			driver.End ();

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		[Fact]
		public void FakeDriver_Only_Sends_Keystrokes_Through_MockKeyPresses ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var top = Application.Top;
			var view = new View ();
			var count = 0;
			var wasKeyPressed = false;

			view.KeyPress += (e) => {
				wasKeyPressed = true;
			};
			top.Add (view);

			Application.Iteration += () => {
				count++;
				if (count == 10) {
					Application.RequestStop ();
				}
			};

			Application.Run ();

			Assert.False (wasKeyPressed);

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		[Fact]
		public void FakeDriver_MockKeyPresses ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var text = "MockKeyPresses";
			var mKeys = new Stack<ConsoleKeyInfo> ();
			foreach (var r in text.Reverse ()) {
				var ck = char.IsLetter (r) ? (ConsoleKey)char.ToUpper (r) : (ConsoleKey)r;
				var cki = new ConsoleKeyInfo (r, ck, false, false, false);
				mKeys.Push (cki);
			}
			FakeConsole.MockKeyPresses = mKeys;

			var top = Application.Top;
			var view = new View ();
			var rText = "";
			var idx = 0;

			view.KeyPress += (e) => {
				Assert.Equal (text [idx], (char)e.KeyEvent.Key);
				rText += (char)e.KeyEvent.Key;
				Assert.Equal (rText, text.Substring (0, idx + 1));
				e.Handled = true;
				idx++;
			};
			top.Add (view);

			Application.Iteration += () => {
				if (mKeys.Count == 0) {
					Application.RequestStop ();
				}
			};

			Application.Run ();

			Assert.Equal ("MockKeyPresses", rText);

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		[Fact]
		public void SendKeys_Test ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var top = Application.Top;
			var view = new View ();
			var shift = false; var alt = false; var control = false;
			Key key = default;
			Key lastKey = default;
			List<Key> keyEnums = GetKeys ();
			int i = 0;
			int idxKey = 0;
			var PushIterations = 0;
			var PopIterations = 0;

			List<Key> GetKeys ()
			{
				List<Key> keys = new List<Key> ();

				foreach (Key k in Enum.GetValues (typeof (Key))) {
					if ((uint)k <= 0xff) {
						keys.Add (k);
					} else if ((uint)k > 0xff) {
						break;
					}
				}

				return keys;
			}

			view.KeyPress += (e) => {
				e.Handled = true;
				PopIterations++;
				var rMk = new KeyModifiers () {
					Shift = e.KeyEvent.IsShift,
					Alt = e.KeyEvent.IsAlt,
					Ctrl = e.KeyEvent.IsCtrl
				};
				lastKey = ShortcutHelper.GetModifiersKey (new KeyEvent (e.KeyEvent.Key, rMk));
				Assert.Equal (key, lastKey);
			};
			top.Add (view);

			Application.Iteration += () => {
				switch (i) {
				case 0:
					SendKeys ();
					break;
				case 1:
					shift = true;
					SendKeys ();
					break;
				case 2:
					alt = true;
					SendKeys ();
					break;
				case 3:
					control = true;
					SendKeys ();
					break;
				}
				if (PushIterations == keyEnums.Count * 4) {
					Application.RequestStop ();
				}
			};

			void SendKeys ()
			{
				var k = keyEnums [idxKey];
				var c = (char)k;
				var ck = char.IsLetter (c) ? (ConsoleKey)char.ToUpper (c) : (ConsoleKey)c;
				var mk = new KeyModifiers () {
					Shift = shift,
					Alt = alt,
					Ctrl = control
				};
				key = ShortcutHelper.GetModifiersKey (new KeyEvent (k, mk));
				Application.Driver.SendKeys (c, ck, shift, alt, control);
				PushIterations++;
				if (idxKey + 1 < keyEnums.Count) {
					idxKey++;
				} else {
					idxKey = 0;
					i++;
				}
			}

			Application.Run ();

			Assert.Equal (key, lastKey);

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		[Fact]
		public void TerminalResized_Simulation ()
		{
			var driver = new FakeDriver ();
			Application.Init (driver, new FakeMainLoop (() => FakeConsole.ReadKey (true)));
			var wasTerminalResized = false;
			Application.Resized = (e) => {
				wasTerminalResized = true;
				Assert.Equal (120, e.Cols);
				Assert.Equal (40, e.Rows);
			};

			Assert.Equal (80, Console.BufferWidth);
			Assert.Equal (25, Console.BufferHeight);

			// MockDriver is by default 80x25
			Assert.Equal (Console.BufferWidth, driver.Cols);
			Assert.Equal (Console.BufferHeight, driver.Rows);
			Assert.False (wasTerminalResized);

			// MockDriver will now be sets to 120x40
			driver.SetBufferSize (120, 40);
			Assert.Equal (120, Application.Driver.Cols);
			Assert.Equal (40, Application.Driver.Rows);
			Assert.True (wasTerminalResized);

			// MockDriver will still be 120x40
			wasTerminalResized = false;
			Application.HeightAsBuffer = true;
			driver.SetWindowSize (40, 20);
			Assert.Equal (120, Application.Driver.Cols);
			Assert.Equal (40, Application.Driver.Rows);
			Assert.Equal (120, Console.BufferWidth);
			Assert.Equal (40, Console.BufferHeight);
			Assert.Equal (40, Console.WindowWidth);
			Assert.Equal (20, Console.WindowHeight);
			Assert.True (wasTerminalResized);

			Application.Shutdown ();
		}

		[Fact]
		public void HeightAsBuffer_Is_False_Left_And_Top_Is_Always_Zero ()
		{
			var driver = new FakeDriver ();
			Application.Init (driver, new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			Assert.False (Application.HeightAsBuffer);
			Assert.Equal (0, Console.WindowLeft);
			Assert.Equal (0, Console.WindowTop);

			driver.SetWindowPosition (5, 5);
			Assert.Equal (0, Console.WindowLeft);
			Assert.Equal (0, Console.WindowTop);

			Application.Shutdown ();
		}

		[Fact]
		public void HeightAsBuffer_Is_True_Left_Cannot_Be_Greater_Than_WindowWidth ()
		{
			var driver = new FakeDriver ();
			Application.Init (driver, new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			Application.HeightAsBuffer = true;
			Assert.True (Application.HeightAsBuffer);

			driver.SetWindowPosition (81, 25);
			Assert.Equal (0, Console.WindowLeft);
			Assert.Equal (0, Console.WindowTop);

			Application.Shutdown ();
		}

		[Fact]
		public void HeightAsBuffer_Is_True_Left_Cannot_Be_Greater_Than_BufferWidth_Minus_WindowWidth ()
		{
			var driver = new FakeDriver ();
			Application.Init (driver, new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			Application.HeightAsBuffer = true;
			Assert.True (Application.HeightAsBuffer);

			driver.SetWindowPosition (81, 25);
			Assert.Equal (0, Console.WindowLeft);
			Assert.Equal (0, Console.WindowTop);

			// MockDriver will now be sets to 120x25
			driver.SetBufferSize (120, 25);
			Assert.Equal (120, Application.Driver.Cols);
			Assert.Equal (25, Application.Driver.Rows);
			Assert.Equal (120, Console.BufferWidth);
			Assert.Equal (25, Console.BufferHeight);
			Assert.Equal (80, Console.WindowWidth);
			Assert.Equal (25, Console.WindowHeight);
			driver.SetWindowPosition (121, 25);
			Assert.Equal (40, Console.WindowLeft);
			Assert.Equal (0, Console.WindowTop);

			driver.SetWindowSize (90, 25);
			Assert.Equal (120, Application.Driver.Cols);
			Assert.Equal (25, Application.Driver.Rows);
			Assert.Equal (120, Console.BufferWidth);
			Assert.Equal (25, Console.BufferHeight);
			Assert.Equal (90, Console.WindowWidth);
			Assert.Equal (25, Console.WindowHeight);
			driver.SetWindowPosition (121, 25);
			Assert.Equal (30, Console.WindowLeft);
			Assert.Equal (0, Console.WindowTop);

			Application.Shutdown ();
		}

		[Fact]
		public void HeightAsBuffer_Is_True_Top_Cannot_Be_Greater_Than_WindowHeight ()
		{
			var driver = new FakeDriver ();
			Application.Init (driver, new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			Application.HeightAsBuffer = true;
			Assert.True (Application.HeightAsBuffer);

			driver.SetWindowPosition (80, 26);
			Assert.Equal (0, Console.WindowLeft);
			Assert.Equal (0, Console.WindowTop);

			Application.Shutdown ();
		}

		[Fact]
		public void HeightAsBuffer_Is_True_Top_Cannot_Be_Greater_Than_BufferHeight_Minus_WindowHeight ()
		{
			var driver = new FakeDriver ();
			Application.Init (driver, new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			Application.HeightAsBuffer = true;
			Assert.True (Application.HeightAsBuffer);

			driver.SetWindowPosition (80, 26);
			Assert.Equal (0, Console.WindowLeft);
			Assert.Equal (0, Console.WindowTop);

			// MockDriver will now be sets to 80x40
			driver.SetBufferSize (80, 40);
			Assert.Equal (80, Application.Driver.Cols);
			Assert.Equal (40, Application.Driver.Rows);
			Assert.Equal (80, Console.BufferWidth);
			Assert.Equal (40, Console.BufferHeight);
			Assert.Equal (80, Console.WindowWidth);
			Assert.Equal (25, Console.WindowHeight);
			Assert.Equal (0, Console.WindowLeft);
			Assert.Equal (0, Console.WindowTop);
			driver.SetWindowPosition (80, 40);
			Assert.Equal (0, Console.WindowLeft);
			Assert.Equal (15, Console.WindowTop);

			driver.SetWindowSize (80, 20);
			Assert.Equal (80, Application.Driver.Cols);
			Assert.Equal (40, Application.Driver.Rows);
			Assert.Equal (80, Console.BufferWidth);
			Assert.Equal (40, Console.BufferHeight);
			Assert.Equal (80, Console.WindowWidth);
			Assert.Equal (20, Console.WindowHeight);
			Assert.Equal (0, Console.WindowLeft);
			Assert.Equal (15, Console.WindowTop);
			driver.SetWindowPosition (80, 41);
			Assert.Equal (0, Console.WindowLeft);
			Assert.Equal (20, Console.WindowTop);

			Application.Shutdown ();
		}

		[Fact]
		public void Internal_Tests ()
		{
			var cs = new ColorScheme ();
			Assert.Equal ("", cs.caller);
		}

		[Fact]
		[AutoInitShutdown]
		public void KeyModifiers_Resetting_At_New_Keystrokes ()
		{
			bool? okInitialFocused = null;
			bool? cancelInitialFocused = null;
			var okClicked = false;
			var closing = false;
			var cursorRight = false;
			var endingKeyPress = false;
			var closed = false;

			var top = Application.Top;

			var ok = new Button ("Ok");
			ok.Clicked += () => {
				if (!okClicked) {
					okClicked = true;
					Application.RequestStop ();
				}
			};

			var cancel = new Button ("Cancel");

			var d = new Dialog ("Quit", cancel, ok);
			d.KeyPress += (e) => {
				if (e.KeyEvent.Key == (Key.Q | Key.CtrlMask)) {
					if (!okClicked && !closing) {
						okInitialFocused = ok.HasFocus;
						cancelInitialFocused = cancel.HasFocus;
						closing = true;
						var mKeys = new Stack<ConsoleKeyInfo> ();
						var cki = new ConsoleKeyInfo ('\0', ConsoleKey.Enter, false, false, false);
						mKeys.Push (cki);
						cki = new ConsoleKeyInfo ('\0', ConsoleKey.RightArrow, false, false, false);
						mKeys.Push (cki);
						FakeConsole.MockKeyPresses = mKeys;
					}
					e.Handled = true;
				} else if (e.KeyEvent.Key == Key.CursorRight) {
					if (!cursorRight) {
						cursorRight = true;
					} else if (ok.HasFocus) {
						e.Handled = endingKeyPress = true;
					}
				}
			};
			d.Loaded += () => {
				var mKeys = new Stack<ConsoleKeyInfo> ();
				var cki = new ConsoleKeyInfo ('q', ConsoleKey.Q, false, false, true);
				mKeys.Push (cki);
				FakeConsole.MockKeyPresses = mKeys;
			};
			d.Closed += (_) => {
				if (okClicked && closing) {
					closed = true;
				}
			};

			top.Ready += () => Application.Run (d);

			Application.Iteration += () => {
				if (closed) {
					Application.RequestStop ();
				}
			};

			Application.Run ();

			Assert.False (okInitialFocused);
			Assert.True (cancelInitialFocused);
			Assert.True (okClicked);
			Assert.True (closing);
			Assert.True (cursorRight);
			Assert.True (endingKeyPress);
			Assert.True (closed);
			Assert.Empty (FakeConsole.MockKeyPresses);
		}

		[Fact, AutoInitShutdown]
		public void AddRune_On_Clip_Left_Or_Right_Replace_Previous_Or_Next_Wide_Rune_With_Space ()
		{
			var tv = new TextView () {
				Width = Dim.Fill (),
				Height = Dim.Fill (),
				Text = @"これは広いルーンラインです。
これは広いルーンラインです。
これは広いルーンラインです。
これは広いルーンラインです。
これは広いルーンラインです。
これは広いルーンラインです。
これは広いルーンラインです。
これは広いルーンラインです。"
			};
			var win = new Window ("ワイドルーン") { Width = Dim.Fill (), Height = Dim.Fill () };
			win.Add (tv);
			Application.Top.Add (win);
			var lbl = new Label ("ワイドルーン。");
			var dg = new Dialog ("テスト", 14, 4, new Button ("選ぶ"));
			dg.Add (lbl);
			Application.Begin (Application.Top);
			Application.Begin (dg);
			((FakeDriver)Application.Driver).SetBufferSize (30, 10);

			var expected = @"
┌ ワイドルーン ──────────────┐
│これは広いルーンラインです。│
│これは広いルーンラインです。│
│これは ┌ テスト ────┐ です。│
│これは │ワイドルーン│ です。│
│これは │  [ 選ぶ ]  │ です。│
│これは └────────────┘ です。│
│これは広いルーンラインです。│
│これは広いルーンラインです。│
└────────────────────────────┘
";

			var pos = GraphViewTests.AssertDriverContentsWithFrameAre (expected, output);
			Assert.Equal (new Rect (0, 0, 30, 10), pos);
		}

		[Fact, AutoInitShutdown]
		public void Write_Do_Not_Change_On_ProcessKey ()
		{
			var win = new Window ();
			Application.Begin (win);
			((FakeDriver)Application.Driver).SetBufferSize (20, 8);

			System.Threading.Tasks.Task.Run (() => {
				System.Threading.Tasks.Task.Delay (500).Wait ();
				Application.MainLoop.Invoke (() => {
					var lbl = new Label ("Hello World") { X = Pos.Center () };
					var dlg = new Dialog ("Test", new Button ("Ok"));
					dlg.Add (lbl);
					Application.Begin (dlg);

					var expected = @"
┌──────────────────┐
│┌ Test ─────────┐ │
││  Hello World  │ │
││               │ │
││               │ │
││    [ Ok ]     │ │
│└───────────────┘ │
└──────────────────┘
";

					var pos = GraphViewTests.AssertDriverContentsWithFrameAre (expected, output);
					Assert.Equal (new Rect (0, 0, 20, 8), pos);

					Assert.True (dlg.ProcessKey (new KeyEvent (Key.Tab, new KeyModifiers ())));
					dlg.Redraw (dlg.Bounds);

					expected = @"
┌──────────────────┐
│┌ Test ─────────┐ │
││  Hello World  │ │
││               │ │
││               │ │
││    [ Ok ]     │ │
│└───────────────┘ │
└──────────────────┘
";

					pos = GraphViewTests.AssertDriverContentsWithFrameAre (expected, output);
					Assert.Equal (new Rect (0, 0, 20, 8), pos);

					win.RequestStop ();
				});
			});

			Application.Run (win);
			Application.Shutdown ();
		}
		
		[Theory]
		[InlineData(0x0000001F, 0x241F)]
		[InlineData(0x0000007F, 0x247F)]
		[InlineData(0x0000009F, 0x249F)]
		[InlineData(0x0001001A, 0x241A)]
		public void MakePrintable_Converts_Control_Chars_To_Proper_Unicode (uint code, uint expected)
		{
			var actual = ConsoleDriver.MakePrintable(code);
				
			Assert.Equal (expected, actual.Value);
		}
		
		[Theory]
		[InlineData(0x20)]
		[InlineData(0x7E)]
		[InlineData(0xA0)]
		[InlineData(0x010020)]
		public void MakePrintable_Does_Not_Convert_Ansi_Chars_To_Unicode (uint code)
		{
			var actual = ConsoleDriver.MakePrintable(code);
				
			Assert.Equal (code, actual.Value);
		}

		/// <summary>
		/// Sometimes when using remote tools EventKeyRecord sends 'virtual keystrokes'.
		/// These are indicated with the wVirtualKeyCode of 231. When we see this code
		/// then we need to look to the unicode character (UnicodeChar) instead of the key
		/// when telling the rest of the framework what button was pressed. For full details
		/// see: https://github.com/gui-cs/Terminal.Gui/issues/2008
		/// </summary>
		[Theory, AutoInitShutdown]
		[InlineData ('A', false, false, false, Key.A)]
		[InlineData ('A', true, false, false, Key.A)]
		[InlineData ('A', true, true, false, Key.A | Key.AltMask)]
		[InlineData ('A', true, true, true, Key.A | Key.AltMask | Key.CtrlMask)]
		[InlineData ('z', false, false, false, Key.z)]
		[InlineData ('z', true, false, false, Key.z)]
		[InlineData ('z', true, true, false, Key.z | Key.AltMask)]
		[InlineData ('z', true, true, true, Key.z | Key.AltMask | Key.CtrlMask)]
		[InlineData ('英', false, false, false, (Key)'英')]
		[InlineData ('英', true, false, false, (Key)'英')]
		[InlineData ('英', true, true, false, (Key)'英' | Key.AltMask)]
		[InlineData ('英', true, true, true, (Key)'英' | Key.AltMask | Key.CtrlMask)]
		[InlineData ('+', false, false, false, (Key)'+')]
		[InlineData ('+', true, false, false, (Key)'+')]
		[InlineData ('+', true, true, false, (Key)'+' | Key.AltMask)]
		[InlineData ('+', true, true, true, (Key)'+' | Key.AltMask | Key.CtrlMask)]
		[InlineData ('0', false, false, false, Key.D0)]
		[InlineData ('=', true, false, false, (Key)'=')]
		[InlineData ('0', true, true, false, Key.D0 | Key.AltMask)]
		[InlineData ('0', true, true, true, Key.D0 | Key.AltMask | Key.CtrlMask)]
		[InlineData ('1', false, false, false, Key.D1)]
		[InlineData ('!', true, false, false, (Key)'!')]
		[InlineData ('1', true, true, false, Key.D1 | Key.AltMask)]
		[InlineData ('1', true, true, true, Key.D1 | Key.AltMask | Key.CtrlMask)]
		[InlineData ('2', false, false, false, Key.D2)]
		[InlineData ('"', true, false, false, (Key)'"')]
		[InlineData ('2', true, true, false, Key.D2 | Key.AltMask)]
		[InlineData ('2', true, true, true, Key.D2 | Key.AltMask | Key.CtrlMask)]
		[InlineData ('3', false, false, false, Key.D3)]
		[InlineData ('#', true, false, false, (Key)'#')]
		[InlineData ('3', true, true, false, Key.D3 | Key.AltMask)]
		[InlineData ('3', true, true, true, Key.D3 | Key.AltMask | Key.CtrlMask)]
		[InlineData ('4', false, false, false, Key.D4)]
		[InlineData ('$', true, false, false, (Key)'$')]
		[InlineData ('4', true, true, false, Key.D4 | Key.AltMask)]
		[InlineData ('4', true, true, true, Key.D4 | Key.AltMask | Key.CtrlMask)]
		[InlineData ('5', false, false, false, Key.D5)]
		[InlineData ('%', true, false, false, (Key)'%')]
		[InlineData ('5', true, true, false, Key.D5 | Key.AltMask)]
		[InlineData ('5', true, true, true, Key.D5 | Key.AltMask | Key.CtrlMask)]
		[InlineData ('6', false, false, false, Key.D6)]
		[InlineData ('&', true, false, false, (Key)'&')]
		[InlineData ('6', true, true, false, Key.D6 | Key.AltMask)]
		[InlineData ('6', true, true, true, Key.D6 | Key.AltMask | Key.CtrlMask)]
		[InlineData ('7', false, false, false, Key.D7)]
		[InlineData ('/', true, false, false, (Key)'/')]
		[InlineData ('7', true, true, false, Key.D7 | Key.AltMask)]
		[InlineData ('7', true, true, true, Key.D7 | Key.AltMask | Key.CtrlMask)]
		[InlineData ('8', false, false, false, Key.D8)]
		[InlineData ('(', true, false, false, (Key)'(')]
		[InlineData ('8', true, true, false, Key.D8 | Key.AltMask)]
		[InlineData ('8', true, true, true, Key.D8 | Key.AltMask | Key.CtrlMask)]
		[InlineData ('9', false, false, false, Key.D9)]
		[InlineData (')', true, false, false, (Key)')')]
		[InlineData ('9', true, true, false, Key.D9 | Key.AltMask)]
		[InlineData ('9', true, true, true, Key.D9 | Key.AltMask | Key.CtrlMask)]
		[InlineData ('\0', false, false, false, (Key)'\0')]
		[InlineData ('\0', true, false, false, (Key)'\0' | Key.ShiftMask)]
		[InlineData ('\0', true, true, false, (Key)'\0' | Key.ShiftMask | Key.AltMask)]
		[InlineData ('\0', true, true, true, (Key)'\0' | Key.ShiftMask | Key.AltMask | Key.CtrlMask)]
		public void TestVKPacket (char unicodeCharacter, bool shift, bool alt, bool control, Key expectedRemapping)
		{
			var before = new ConsoleKeyInfo (unicodeCharacter, ConsoleKey.Packet, shift, alt, control);
			var top = Application.Top;

			top.KeyPress += (e) => {
				var after = e.KeyEvent.Key;
				Assert.Equal (before.KeyChar, (char)after);
				Assert.Equal (expectedRemapping, after);
			};

			Application.Begin (top);

			Application.Driver.SendKeys (unicodeCharacter, ConsoleKey.Packet, shift, alt, control);
		}
	}
}
