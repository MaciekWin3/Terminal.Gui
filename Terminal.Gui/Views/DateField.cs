//
// DateField.cs: text entry for date
//
// Author: Barry Nolte
//
// Licensed under the MIT license
//
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Terminal.Gui;

/// <summary>
///   Simple Date editing <see cref="View"/>
/// </summary>
/// <remarks>
///   The <see cref="DateField"/> <see cref="View"/> provides date editing functionality with mouse support.
/// </remarks>
public class DateField : TextField {
	DateTime date;
	bool isShort;
	readonly int longFieldLen = 10;
	readonly int shortFieldLen = 8;
	string sepChar;
	string longFormat;
	string shortFormat;

	int fieldLen => isShort ? shortFieldLen : longFieldLen;

	string format => isShort ? shortFormat : longFormat;

	/// <summary>
	///   DateChanged event, raised when the <see cref="Date"/> property has changed.
	/// </summary>
	/// <remarks>
	///   This event is raised when the <see cref="Date"/> property changes.
	/// </remarks>
	/// <remarks>
	///   The passed event arguments containing the old value, new value, and format string.
	/// </remarks>
	public event EventHandler<DateTimeEventArgs<DateTime>> DateChanged;

	/// <summary>
	///    Initializes a new instance of <see cref="DateField"/> using <see cref="LayoutStyle.Absolute"/> layout.
	/// </summary>
	/// <param name="x">The x coordinate.</param>
	/// <param name="y">The y coordinate.</param>
	/// <param name="date">Initial date contents.</param>
	/// <param name="isShort">If true, shows only two digits for the year.</param>
	public DateField (int x, int y, DateTime date, bool isShort = false) : base (x, y, isShort ? 10 : 12, "") => Initialize (date, isShort);

	/// <summary>
	///  Initializes a new instance of <see cref="DateField"/> using <see cref="LayoutStyle.Computed"/> layout.
	/// </summary>
	public DateField () : this (DateTime.MinValue) { }

	/// <summary>
	///  Initializes a new instance of <see cref="DateField"/> using <see cref="LayoutStyle.Computed"/> layout.
	/// </summary>
	/// <param name="date"></param>
	public DateField (DateTime date) : base ("")
	{
		Width = fieldLen + 2;
		Initialize (date);
	}

	void Initialize (DateTime date, bool isShort = false)
	{
		var cultureInfo = CultureInfo.CurrentCulture;
		sepChar = cultureInfo.DateTimeFormat.DateSeparator;
		longFormat = GetLongFormat (cultureInfo.DateTimeFormat.ShortDatePattern);
		shortFormat = GetShortFormat (longFormat);
		this.isShort = isShort;
		Date = date;
		CursorPosition = 1;
		TextChanged += DateField_Changed;

		// Things this view knows how to do
		AddCommand (Command.DeleteCharRight, () => {
			DeleteCharRight ();
			return true;
		});
		AddCommand (Command.DeleteCharLeft, () => {
			DeleteCharLeft (false);
			return true;
		});
		AddCommand (Command.LeftHome, () => MoveHome ());
		AddCommand (Command.Left, () => MoveLeft ());
		AddCommand (Command.RightEnd, () => MoveEnd ());
		AddCommand (Command.Right, () => MoveRight ());

		// Default keybindings for this view
		KeyBindings.Add (KeyCode.Delete, Command.DeleteCharRight);
		KeyBindings.Add (Key.D.WithCtrl, Command.DeleteCharRight);

		KeyBindings.Add (Key.Delete, Command.DeleteCharLeft);
		KeyBindings.Add (Key.Backspace, Command.DeleteCharLeft);

		KeyBindings.Add (Key.Home, Command.LeftHome);
		KeyBindings.Add (Key.A.WithCtrl, Command.LeftHome);

		KeyBindings.Add (Key.CursorLeft, Command.Left);
		KeyBindings.Add (Key.B.WithCtrl, Command.Left);

		KeyBindings.Add (Key.End, Command.RightEnd);
		KeyBindings.Add (Key.E.WithCtrl, Command.RightEnd);

		KeyBindings.Add (Key.CursorRight, Command.Right);
		KeyBindings.Add (Key.F.WithCtrl, Command.Right);

	}

	/// <inheritdoc />
	public override bool OnProcessKeyDown (Key a)
	{
		// Ignore non-numeric characters.
		if (a >= Key.D0 && a <= Key.D9) {
			if (!ReadOnly) {
				if (SetText ((Rune)a)) {
					IncCursorPosition ();
				}
			}
			return true;
		}
		return false;
	}

	void DateField_Changed (object sender, TextChangedEventArgs e)
	{
		//string textDate = " " + date.Date.ToString (GetInvarianteFormat ());
	}

	private string TextDate {
		get => date.ToString (format);
		set {
			if (DateTime.TryParseExact (value, format, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime dt)) {
				Text = value;
			}
		}
	}

	string GetInvarianteFormat () => $"MM{sepChar}dd{sepChar}yyyy";

	string GetLongFormat (string lf)
	{
		string [] frm = lf.Split (sepChar);
		for (int i = 0; i < frm.Length; i++) {
			if (frm [i].Contains ('M') && frm [i].GetRuneCount () < 2) {
				lf = lf.Replace ("M", "MM");
			}
			if (frm [i].Contains ('d') && frm [i].GetRuneCount () < 2) {
				lf = lf.Replace ("d", "dd");
			}
			if (frm [i].Contains ('y') && frm [i].GetRuneCount () < 4) {
				lf = lf.Replace ("yy", "yyyy");
			}
		}
		return $" {lf}";
	}

	string GetShortFormat (string lf) => lf.Replace ("yyyy", "yy");

	/// <summary>
	///   Gets or sets the date of the <see cref="DateField"/>.
	/// </summary>
	/// <remarks>
	/// </remarks>
	public DateTime Date {
		get => date;
		set {
			if (ReadOnly) {
				return;
			}

			var oldData = date;
			date = value;
			TextDate = value.ToString (format);
			var args = new DateTimeEventArgs<DateTime> (oldData, value, format);
			if (oldData != value) {
				OnDateChanged (args);
			}
		}
	}

	/// <summary>
	/// Get or set the date format for the widget.
	/// </summary>
	public bool IsShortFormat {
		get => isShort;
		set {
			isShort = value;
			if (isShort) {
				Width = 10;
			} else {
				Width = 12;
			}
			bool ro = ReadOnly;
			if (ro) {
				ReadOnly = false;
			}
			if (date.Year > 99) {
				SetText (Text);
			}
			ReadOnly = ro;
			SetNeedsDisplay ();
		}
	}

	/// <inheritdoc/>
	public override int CursorPosition {
		get => base.CursorPosition;
		set => base.CursorPosition = Math.Max (Math.Min (value, fieldLen), 1);
	}

	bool SetText (Rune key)
	{
		var text = Text.EnumerateRunes ().ToList ();
		var newText = text.GetRange (0, CursorPosition);
		newText.Add (key);
		if (CursorPosition < fieldLen) {
			newText = newText.Concat (text.GetRange (CursorPosition + 1, text.Count - (CursorPosition + 1))).ToList ();
		}
		return SetText (StringExtensions.ToString (newText));
	}

	bool SetText (string text)
	{
		if (string.IsNullOrEmpty (text)) {
			return false;
		}

		var date = ParseDate (text);
		string formattedDate = FormatDate (date);

		bool canBeParsed = DateTime.TryParseExact (formattedDate, format,
					    CultureInfo.CurrentCulture, DateTimeStyles.None, out var result);
		if (!canBeParsed) {
			return false;
		}

		Date = result;
		return true;
	}

	private string FormatDate (DateTime date) => date.ToString (format);
	private DateTime ParseDate (string text)
	{
		var values = text.Split (sepChar);
		if (values.Length != 3) {
			throw new ArgumentException ("Invalid date format");
		}
		int year = ValidateYear (values [2], out bool isValidDate);
		int month = ValidateMonth (values [0], out isValidDate);
		int day = ValidateDay (values [1], year, month, out isValidDate);
		if (!isValidDate) {
			throw new ArgumentException ("Invalid date format");
		}
		return new DateTime (year, month, day);
	}

	private int ValidateDay (string dayStr, int year, int month, out bool isValid)
	{
		bool canBeParsedToInt = int.TryParse (dayStr, out int day);

		if (!canBeParsedToInt) {
			isValid = false;
			return 0;
		}

		if (day < 1) {
			isValid = false;
			return 1;
		}

		if (day > DateTime.DaysInMonth (year, month)) {
			isValid = false;
			return DateTime.DaysInMonth (year, month);
		}

		isValid = true;
		return day;
	}

	private int ValidateMonth (string monthStr, out bool isValid)
	{
		bool canBeParsedToInt = int.TryParse (monthStr, out int month);

		if (!canBeParsedToInt) {
			isValid = false;
			return 0;
		}

		if (month < 1) {
			isValid = false;
			return 1;
		}

		if (month > 12) {
			isValid = false;
			return 12;
		}

		isValid = true;
		return month;
	}

	private int ValidateYear (string yearStr, out bool isValid)
	{
		bool canBeParsedToInt = int.TryParse (yearStr, out int year);

		if (!canBeParsedToInt) {
			isValid = false;
			return 0;
		}

		if (year < 1) {
			isValid = false;
			return 1;
		}

		if (year > 9999) {
			isValid = false;
			return 9999;
		}

		isValid = true;
		return year;
	}

	string FormatYear (int year) =>
	    (isShort, year.ToString ().Length) switch {
		    (false, 2) => $"{DateTime.Now.Year,2:00}{year:00}",
		    (false, 4) => $"{year:0000}",
		    (true, 2) => $"{year:00}",
		    (true, 4) => $"{year.ToString ().Substring (2, 2)}",
		    _ => $"{year:0000}"
	    };

	void IncCursorPosition ()
	{
		if (CursorPosition == fieldLen) {
			return;
		}
		if (Text [++CursorPosition] == sepChar.ToCharArray () [0]) {
			CursorPosition++;
		}
	}

	void DecCursorPosition ()
	{
		if (CursorPosition == 1) {
			return;
		}
		if (Text [--CursorPosition] == sepChar.ToCharArray () [0]) {
			CursorPosition--;
		}
	}

	void AdjCursorPosition ()
	{
		if (Text [CursorPosition] == sepChar.ToCharArray () [0]) {
			CursorPosition++;
		}
	}

	bool MoveRight ()
	{
		IncCursorPosition ();
		return true;
	}

	new bool MoveEnd ()
	{
		CursorPosition = fieldLen;
		return true;
	}

	bool MoveLeft ()
	{
		DecCursorPosition ();
		return true;
	}

	bool MoveHome ()
	{
		// Home, C-A
		CursorPosition = 1;
		return true;
	}

	/// <inheritdoc/>
	public override void DeleteCharLeft (bool useOldCursorPos = true)
	{
		if (ReadOnly) {
			return;
		}

		SetText ((Rune)'0');
		DecCursorPosition ();
		return;
	}

	/// <inheritdoc/>
	public override void DeleteCharRight ()
	{
		if (ReadOnly) {
			return;
		}

		SetText ((Rune)'0');
		return;
	}

	/// <inheritdoc/>
	public override bool MouseEvent (MouseEvent ev)
	{
		if (!ev.Flags.HasFlag (MouseFlags.Button1Clicked)) {
			return false;
		}
		if (!HasFocus) {
			SetFocus ();
		}

		int point = ev.X;
		if (point > fieldLen) {
			point = fieldLen;
		}
		if (point < 1) {
			point = 1;
		}
		CursorPosition = point;
		AdjCursorPosition ();
		return true;
	}

	/// <summary>
	/// Event firing method for the <see cref="DateChanged"/> event.
	/// </summary>
	/// <param name="args">Event arguments</param>
	public virtual void OnDateChanged (DateTimeEventArgs<DateTime> args) => DateChanged?.Invoke (this, args);
}