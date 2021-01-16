using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IntruderLeather.Controls.IpAddress
{
    public class IpAddressControl : TextBox
    {
        static IpAddressControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(IpAddressControl), new FrameworkPropertyMetadata(typeof(IpAddressControl)));
        }

        private bool IsDigit(KeyEventArgs e)
        {
            bool isDigit =
                ((e.Key >= Key.D0 && e.Key < Key.D9)
                    || (e.Key >= Key.NumPad0 && e.Key < Key.NumPad9))
                    && !IsShift(e);
            if (IPV6 && !isDigit)
            {
                isDigit = e.Key >= Key.A && e.Key <= Key.F;
            }
            return isDigit;
        }
        private bool IsShift(KeyEventArgs e)
        {
            return e.KeyboardDevice.Modifiers == ModifierKeys.Shift;
        }

        private bool IsSeparator(KeyEventArgs e)
        {
            return (IPV6 && e.Key == Key.OemSemicolon && IsShift(e))
                || (!IPV6 && (e.Key == Key.OemPeriod || e.Key == Key.Decimal) && !IsShift(e));
        }

        private bool IsValidControlKey(KeyEventArgs e)
        {
            return e.Key == Key.Back
                || e.Key == Key.Delete
                || e.Key == Key.Left
                || e.Key == Key.Right
                || e.Key == Key.Home
                || e.Key == Key.End;
        }

        private bool IsValidKey(KeyEventArgs e)
        {
            return IsDigit(e) || IsSeparator(e) || IsValidControlKey(e);
        }

        bool IsDigit(int index)
        {
            bool isDigit = Text[index] >= '0' && Text[index] <= '9';
            if (!isDigit && IPV6)
            {
                isDigit = Text[index] >= 'A' && Text[index] <= 'F';
            }
            return isDigit;
        }

        private string GetAddressComponent(int index)
        {
            int start = index;
            int length = 0;
            while (start > 0 && IsDigit(start - 1))
            {
                --start;
            }
            while (start + length < Text.Length && IsDigit(start + length))
            {
                ++length;
            }
            return Text.Substring(start, length);

        }

        private void NormalizeComponent(int index = -1)
        {
            int start = index == -1 ? CaretIndex : index;
            int length = 0;
            while (start > 0 && IsDigit(start - 1)) --start;
            while (start + length < Text.Length && IsDigit(start + length)) ++length;
            string component = string.Empty;
            foreach (char c in Text.Substring(start, length))
            {
                if (!CanAppendToAddressComponent(component, int.Parse(c.ToString())))
                {
                    break;
                }
                component += c;
            }
            Text = Text.Substring(0, start) + component + Text.Substring(start + length);
            CaretIndex = start + component.Length;
        }

        private bool CanAppendToAddressComponent(string current, int digit)
        {
            if (IPV6)
            {
                return current.Length < 4;
            }
            int currentVal = string.IsNullOrWhiteSpace(current)
                ? 0 : int.Parse(current) * 10;
            return current.Length < 3 && currentVal + digit <= 255;
        }

        private int GetDigit(KeyEventArgs e)
        {
            if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                return e.Key - Key.D0;
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                return e.Key - Key.NumPad0;
            }
            else
            {
                return e.Key - Key.A;
            }
        }

        private string GetDigitString(KeyEventArgs e)
        {
            return GetDigit(e).ToString("X");
        }

        private int CaretComponent => Text.Substring(0, CaretIndex).Split(Separator).Length - 1;

        private bool _needsFormatting = false;

        private void Reformat()
        {
            _needsFormatting = false;
            int component = CaretComponent;
            IEnumerable<string> components = IPV6
                ? Text.Split(Separator).Select(t => string.IsNullOrEmpty(t) ? string.Empty : int.Parse(t).ToString("X4"))
                : Text.Split(Separator).Select(t => string.IsNullOrEmpty(t) ? string.Empty : int.Parse(t).ToString());
            Text = string.Join(Separator, components);
            for (int i = 0; i < Text.Length; ++i)
            {
                if (Text[i] == Separator)
                {
                    if (component == 0)
                    {
                        CaretIndex = i;
                        return;
                    }
                    --component;
                }
            }
            CaretIndex = Text.Length;
        }

        private char Separator => IPV6 ? ':' : '.';

        private bool CanAddSeparator => Text.Count(c => c == Separator) < 3;

        private void HandleSeparator(KeyEventArgs e)
        {
            int caretIndex = CaretIndex;

            // The simplest case is when the next character is a separator.
            // Just increment the cursor.
            if (Text.Length > CaretIndex && Text[CaretIndex] == Separator)
            {
                ++CaretIndex;
                e.Handled = true;
            }
            // If we're at the beginning of the address or the previous character
            // is a separator, add an empty component.
            else if (CaretIndex == 0 || Text[CaretIndex - 1] == Separator)
            {
                e.Handled = true;
                if (CanAddSeparator)
                {
                    string zeroComponent = IPV6 ? Separator + Text : "0" + Separator;
                    Text = Text.Substring(0, caretIndex) + zeroComponent + Text.Substring(caretIndex);
                    CaretIndex = caretIndex + zeroComponent.Length;
                    if (Text.Length > zeroComponent.Length)
                    {
                        NormalizeComponent();
                    }
                }
            }
            // If none of the above are true, and we already have enough
            // components, do nothing. Otherwise, let the default process do it.
            else if (!CanAddSeparator)
            {
                e.Handled = true;
            }
            return;
        }

        #region Event Handlers
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // We filter bad characters in the OnPreviewKeyDown handler, so we
            // will just assume that if we're here, it's a character that we're
            // expecting.

            int caretIndex = CaretIndex;

            // We'll let the system handle cursor manipulation.
            if (e.Key == Key.Left
                || e.Key == Key.Right
                || e.Key == Key.Home
                || e.Key == Key.End) return;

            // If we're at the end of the line, let the system handle
            // backspace.
            if (e.Key == Key.Back && CaretIndex == Text.Length) return;

            // If we're deleting, and it goes to the end, let the system handle it.
            if (e.Key == Key.Delete
                || e.Key == Key.Back
                && CaretIndex + SelectionLength == Text.Length) return;

            // Handle the separator.
            if (IsSeparator(e))
            {
                HandleSeparator(e);
                return;
            }

            // If nothing is selected, and backspace or delete is pressed,
            // select something to delete.
            if (SelectionLength == 0)
            {
                // If it's a delete, select the next character.
                if (e.Key == Key.Delete && CaretIndex < Text.Length)
                {
                    SelectionLength = 1;
                }
                // If it's backspace, select the previous character.
                else if (e.Key == Key.Back && CaretIndex > 0)
                {
                    --CaretIndex;
                    SelectionLength = 1;
                }
            }

            // Handle deleting selection.
            if (SelectionLength > 0)
            {
                SelectedText = string.Empty;
                NormalizeComponent();
            }

            // Handle a new digit.
            if (IsDigit(e))
            {
                if (CaretIndex != Text.Length)
                {
                    Text = Text.Insert(CaretIndex, GetDigitString(e));
                    e.Handled = true;
                    CaretIndex = caretIndex + 1;
                    NormalizeComponent();
                }
                else
                {
                    string current = GetAddressComponent(CaretIndex);
                    if (!CanAppendToAddressComponent(current, GetDigit(e)))
                    {
                        if (CanAddSeparator)
                        {
                            int caret = CaretIndex;
                            Text += Separator;
                            CaretIndex = caret + 1;
                        }
                        else
                        {
                            e.Handled = true;
                        }
                    }
                }
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (_needsFormatting)
            {
                Reformat();
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            // First, filter keys that are never right.
            if (!IsValidKey(e))
            {
                e.Handled = true;
            }
            // Let's feed backspace through the main filter.
            if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                OnKeyDown(e);
            }
        }
        #endregion

        public bool IPV6 { get; set; }
        private byte[] _ipV4Bytes = new byte[4];
        private ushort[] _ipV6Shorts = new ushort[4];
        public System.Net.IPAddress IPAddress { get; set; }
        public string IPAddressText { get; set; }
    }
}
