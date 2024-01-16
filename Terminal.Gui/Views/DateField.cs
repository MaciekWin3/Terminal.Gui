﻿//
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
	DateTime _date;


	/// <summary>
	/// Format of date. The default is CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.
	/// </summary>
	//public string Format { get; set; } = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
	public string Format { get; set; } = " " + CultureInfo.GetCultureInfo ("pl-PL").DateTimeFormat.ShortDatePattern;

	private int FieldLen => Format.Trim ().Length;

	private char Separator {
		get {
			if (!string.IsNullOrEmpty (Format)) {
				var sep = Format
					.Where (c => !char.IsLetterOrDigit (c))
					.Where (c => !char.IsWhiteSpace (c))
					.FirstOrDefault ();
				return sep;
			}

			return '/';
		}
	}

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
	///  Initializes a new instance of <see cref="DateField"/> using <see cref="LayoutStyle.Computed"/> layout.
	/// </summary>
	public DateField () : this (DateTime.MinValue) { }

	/// <summary>
	///  Initializes a new instance of <see cref="DateField"/> using <see cref="LayoutStyle.Computed"/> layout.
	/// </summary>
	/// <param name="date"></param>
	public DateField (DateTime date) : base ("")
	{
		Width = FieldLen + 2;
		SetInitialProperties (date);
	}

	void SetInitialProperties (DateTime date)
	{
		//CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo ("en-GB");
		Date = date;
		CursorPosition = 1;
		TextChanging += DateField_Changing;

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

		KeyBindings.Add (Key.Backspace, Command.DeleteCharLeft);
		KeyBindings.Add (Key.D.WithAlt, Command.DeleteCharLeft);

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

	void DateField_Changing (object sender, TextChangingEventArgs e)
	{
		try {
			int spaces = 0;
			for (int i = 0; i < e.NewText.Length; i++) {
				if (e.NewText [i] == ' ') {
					spaces++;
				} else {
					break;
				}
			}
			spaces += FieldLen;
			//string trimedText = e.NewText [..spaces];
			string trimedText = e.NewText [..Math.Min (spaces, e.NewText.Length)];
			spaces -= FieldLen;
			trimedText = trimedText.Replace (new string (' ', spaces), " ").Trim ();
			string textDate = _date.ToString (Format);
			if (textDate != e.NewText) {
				e.NewText = textDate; // cos tu
			}
			AdjCursorPosition (CursorPosition, true);
		} catch (Exception) {
			e.Cancel = true;
		}
	}

	/// <summary>
	///   Gets or sets the date of the <see cref="DateField"/>.
	/// </summary>
	/// <remarks>
	/// </remarks>
	public DateTime Date {
		get => _date;
		set {
			if (ReadOnly) {
				return;
			}

			var oldData = _date;
			_date = value;
			Text = value.ToString (Format);
			var args = new DateTimeEventArgs<DateTime> (oldData, value, Format);
			if (oldData != value) {
				OnDateChanged (args);
			}
		}
	}

	/// <inheritdoc/>
	public override int CursorPosition {
		get => base.CursorPosition;
		set => base.CursorPosition = Math.Max (Math.Min (value, FieldLen), 1);
	}

	bool SetText (Rune key)
	{
		if (CursorPosition > FieldLen) {
			CursorPosition = FieldLen;
			return false;
		} else if (CursorPosition < 1) {
			CursorPosition = 1;
			return false;
		}

		var text = Text.EnumerateRunes ().ToList ();
		var newText = text.GetRange (0, CursorPosition);
		newText.Add (key);
		if (CursorPosition < FieldLen) {
			newText = [.. newText, .. text.GetRange (CursorPosition + 1, text.Count - (CursorPosition + 1))];
		}
		return SetText (StringExtensions.ToString (newText));
	}

	bool SetText (string text)
	{
		if (string.IsNullOrEmpty (text)) {
			return false;
		}

		text = NormalizeFormat (text);
		string [] vals = text.Split (Separator);
		string [] frm = Format.Split (Separator);
		int year;
		int month;
		int day;
		int idx = GetFormatIndex (frm, "y");
		if (Int32.Parse (vals [idx]) < 1) {
			year = 1;
			vals [idx] = "1";
		} else {
			year = Int32.Parse (vals [idx]);
		}
		idx = GetFormatIndex (frm, "M");
		if (Int32.Parse (vals [idx]) < 1) {
			month = 1;
			vals [idx] = "1";
		} else if (Int32.Parse (vals [idx]) > 12) {
			month = 12;
			vals [idx] = "12";
		} else {
			month = Int32.Parse (vals [idx]);
		}
		idx = GetFormatIndex (frm, "d");
		if (Int32.Parse (vals [idx]) < 1) {
			day = 1;
			vals [idx] = "1";
		} else if (Int32.Parse (vals [idx]) > 31) {
			day = DateTime.DaysInMonth (year, month);
			vals [idx] = day.ToString ();
		} else {
			day = Int32.Parse (vals [idx]);
		}
		string d = GetDate (month, day, year, frm);

		if (!DateTime.TryParseExact (d, Format, CultureInfo.CurrentCulture, DateTimeStyles.None, out var result)) {
			return false;
		}
		Date = result;
		return true;
	}

	string NormalizeFormat (string text, string fmt = null, string sepChar = null)
	{
		if (string.IsNullOrEmpty (fmt)) {
			fmt = Format;
		}
		if (string.IsNullOrEmpty (sepChar)) {
			sepChar = Separator.ToString ();
		}
		if (fmt.Length != text.Length) {
			return text;
		}

		var fmtText = text.ToCharArray ();
		for (int i = 0; i < text.Length; i++) {
			var c = fmt [i];
			if (c.ToString () == sepChar && text [i].ToString () != sepChar) {
				fmtText [i] = c;
			}
		}

		return new string (fmtText);
	}

	string GetDate (int month, int day, int year, string [] fm)
	{
		string date = " ";
		for (int i = 0; i < fm.Length; i++) {
			if (fm [i].Contains ('M')) {
				date += $"{month,2:00}";
			} else if (fm [i].Contains ('d')) {
				date += $"{day,2:00}";
			} else {
				date += $"{year,4:0000}";
			}
			if (i < 2) {
				date += $"{Separator}";
			}
		}
		return date;
	}

	static int GetFormatIndex (string [] fm, string t)
	{
		int idx = -1;
		for (int i = 0; i < fm.Length; i++) {
			if (fm [i].Contains (t)) {
				idx = i;
				break;
			}
		}
		return idx;
	}

	void IncCursorPosition ()
	{
		if (CursorPosition >= FieldLen) {
			CursorPosition = FieldLen;
			return;
		}
		CursorPosition++;
		AdjCursorPosition (CursorPosition);
	}

	void DecCursorPosition ()
	{
		if (CursorPosition <= 1) {
			CursorPosition = 1;
			return;
		}
		CursorPosition--;
		AdjCursorPosition (CursorPosition, false);
	}

	void AdjCursorPosition (int point, bool increment = true)
	{
		var newPoint = point;
		if (point > FieldLen) {
			newPoint = FieldLen;
		}
		if (point < 1) {
			newPoint = 1;
		}
		if (newPoint != point) {
			CursorPosition = newPoint;
		}

		while (Text [CursorPosition] == Separator) {
			if (increment) {
				CursorPosition++;
			} else {
				CursorPosition--;
			}
		}
	}

	bool MoveRight ()
	{
		ClearAllSelection ();
		IncCursorPosition ();
		return true;
	}

	new bool MoveEnd ()
	{
		ClearAllSelection ();
		CursorPosition = FieldLen;
		return true;
	}

	bool MoveLeft ()
	{
		ClearAllSelection ();
		DecCursorPosition ();
		return true;
	}

	bool MoveHome ()
	{
		// Home, C-A
		ClearAllSelection ();
		CursorPosition = 1;
		return true;
	}

	/// <inheritdoc/>
	public override void DeleteCharLeft (bool useOldCursorPos = true)
	{
		if (ReadOnly) {
			return;
		}

		ClearAllSelection ();
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

		ClearAllSelection ();
		SetText ((Rune)'0');
		return;
	}

	/// <inheritdoc/>
	public override bool MouseEvent (MouseEvent ev)
	{
		var result = base.MouseEvent (ev);

		if (result && SelectedLength == 0 && ev.Flags.HasFlag (MouseFlags.Button1Pressed)) {
			int point = ev.X;
			AdjCursorPosition (point, true);
		}
		return result;
	}

	/// <summary>
	/// Event firing method for the <see cref="DateChanged"/> event.
	/// </summary>
	/// <param name="args">Event arguments</param>
	public virtual void OnDateChanged (DateTimeEventArgs<DateTime> args) => DateChanged?.Invoke (this, args);
}