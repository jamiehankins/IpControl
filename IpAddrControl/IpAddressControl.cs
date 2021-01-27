using System;
using System.Globalization;
using System.Linq;
using System.Media;
using System.Net;
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

        public IpAddressControl()
        {
            // Until I work out how to keep undo from getting us into unsupported
            // situations, I'm going to remove undo/redo. The issue is that we do
            // a lot of adding steps to autoformat things when the user is typing.
            // When we allow undo, it sees our changes as individual edits and
            // and allows the user to undo them without the user change that triggered
            // them, which can result in an invalid address. There is functionality
            // to start and end an edit so that undo/redo does it correctly. For that
            // to work, we need to start the edit before the change that triggers the
            // formatting and end after the formatting. That will take a little work.
            IsUndoEnabled = false;
        }

        private bool IsDigit(char c)
        {
            bool isDigit = c >= '0' && c <= '9';
            if (IPV6 && !isDigit)
            {
                isDigit = (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            }
            return isDigit;
        }

        private bool IsDigit(int index)
        {
            return IsDigit(Text[index]);
        }

        private bool _normalizing = false;
        private bool _setting = false;
        private void SetText(string text)
        {
            _setting = true;
            Text = text;
            _setting = false;
        }

        private char Separator => IPV6 ? ':' : '.';

        private bool CanAddSeparator => Text.Count(c => c == Separator) < (IPV6 ? 6 : 3);

        private bool HandleSeparator()
        {
            bool result = true;
            int caretIndex = CaretIndex;

            // If the user had text selected, it gets a lot harder.
            if (SelectionLength > 0)
            {
                // There either needs to be at least one separator selected or
                // less than three separators already.
                if (SelectedText.Contains(Separator)
                    || Text.Count(c => c == Separator) < 3)
                {
                    SelectedText = Separator.ToString();
                    CaretIndex++;
                    // Incrementing the index gets the previous section formatted,
                    // but we want to format the new section, too.
                    new AddressComponent(this).Normalize();
                }
                else
                {
                    result = false;
                }
            }
            // The simplest case is when the next character is a separator.
            // Just increment the cursor.
            else if (Text.Length > CaretIndex && Text[CaretIndex] == Separator)
            {
                ++CaretIndex;
            }
            // If we're at the beginning of the address or the previous character
            // is a separator, add an empty component.
            else if (CaretIndex == 0 || Text[CaretIndex - 1] == Separator)
            {
                if (CanAddSeparator)
                {
                    string zeroComponent = "0" + Separator;
                    SetText(Text.Insert(caretIndex, "0" + Separator));
                    CaretIndex = caretIndex + zeroComponent.Length;
                }
                else
                {
                    // The negative case here does nothing since you can't insert
                    // another separator.
                    result = false;
                }
            }
            // Otherwise, just add the separator.
            else if (CanAddSeparator)
            {
                InsertChar(Separator);
            }
            else
            {
                result = false;
            }
            return result;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            DataObject.AddPastingHandler(this, OnPasting);
            DataObject.AddCopyingHandler(this, OnCopying);
            Dispatcher.ShutdownStarted += OnShutdownStarted;
            CommandManager.AddPreviewExecutedHandler(this, OnPreviewExecuted);
        }

        private void OnShutdownStarted(object sender, EventArgs e)
        {
            DataObject.RemovePastingHandler(this, OnPasting);
            DataObject.RemoveCopyingHandler(this, OnCopying);
            Dispatcher.ShutdownStarted -= OnShutdownStarted;
            CommandManager.RemovePreviewExecutedHandler(this, OnPreviewExecuted);
        }

        private bool _cut = false;
        private void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Cut)
            {
                if (SelectionLength == 0)
                {
                    SelectAll();
                }
                _cut = true;
            }
            else if (e.Command == ApplicationCommands.Copy)
            {
                if (SelectionLength == 0)
                {
                    SelectAll();
                }
                _cut = false;
            }
        }

        private bool HandleDeleteAndBackspace(bool delete = true)
        {
            // If nothing is selected, and backspace or delete is pressed,
            // select something to delete.
            if (SelectionLength == 0)
            {
                // If it's a delete, select the next character.
                if (delete && CaretIndex < Text.Length)
                {
                    SelectionLength = 1;
                }
                else if (!delete && CaretIndex > 0)
                {
                    --CaretIndex;
                    SelectionLength = 1;
                }
            }
            // Now, if we had anything selected or selected anything above,
            // delete it.
            if (SelectionLength > 0)
            {
                bool normalize = SelectedText.Any(c => c == Separator);
                SelectedText = string.Empty;
                if (normalize)
                {
                    new AddressComponent(this).Normalize();
                }
            }
            return true;
        }

        private void Error()
        {
            SystemSounds.Asterisk.Play();
        }

        #region Event Handlers
        private int _lastCaret = -1;
        protected override void OnSelectionChanged(RoutedEventArgs e)
        {
            base.OnSelectionChanged(e);
            if (!_setting)
            {
                if (!_normalizing
                    && _lastCaret != -1
                    && _lastCaret != CaretIndex
                    && Text.Length >= _lastCaret
                    && SelectionLength == 0)
                {
                    var component = new AddressComponent(this, _lastCaret);
                    if (CaretIndex < component.Start
                        || CaretIndex > component.Start + component.Length)
                    {
                        component.Normalize();
                    }
                }
                _lastCaret = CaretIndex;
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            bool handled = false;
            if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                if (HandleDeleteAndBackspace(e.Key == Key.Delete))
                {
                    e.Handled = handled = true;
                }
            }
            if (!handled) base.OnPreviewKeyDown(e);
        }

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            if (e.Text.Any(c => c == Separator || IsDigit(c)))
            {
                foreach (char c in e.Text.ToUpper())
                {
                    int caretIndex = CaretIndex;
                    if (c == Separator)
                    {
                        if (!HandleSeparator())
                        {
                            Error();
                        }
                    }
                    else if (IsDigit(c))
                    {
                        // Now, if we had anything selected or selected anything above,
                        // delete it.
                        if (SelectionLength > 0)
                        {
                            HandleDeleteAndBackspace();
                        }
                        var component = new AddressComponent(this);
                        if (!component.Insert(c))
                        {
                            if (HandleSeparator())
                            {
                                component = new AddressComponent(this);
                                if (!component.Insert(c))
                                {
                                    Error();
                                }
                            }
                            else
                            {
                                Error();
                            }
                        }
                    }
                }
            }
            e.Handled = true;
        }

        void InsertChar(char ch)
        {
            int caretIndex = CaretIndex;
            SetText(Text.Insert(CaretIndex, ch.ToString()));
            CaretIndex = caretIndex + 1;
        }

        private void Set(string address)
        {
            var eventArgs = new TextCompositionEventArgs(
                InputManager
                    .Current
                    .PrimaryKeyboardDevice,
                new TextComposition(
                    InputManager
                    .Current,
                    this,
                    address));
            eventArgs.RoutedEvent = TextCompositionManager.PreviewTextInputStartEvent;
            RaiseEvent(eventArgs);
            eventArgs.RoutedEvent = TextCompositionManager.TextInputStartEvent;
            RaiseEvent(eventArgs);
            eventArgs.RoutedEvent = TextCompositionManager.PreviewTextInputEvent;
            RaiseEvent(eventArgs);
            eventArgs.RoutedEvent = TextCompositionManager.TextInputEvent;
            RaiseEvent(eventArgs);
        }

        private void OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            var isText = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText) return;
            var text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
            if (SelectionLength == 0)
            {
                Set(text);
            }
            e.Handled = true;
            e.CancelCommand();
        }

        private void OnCopying(object sender, DataObjectCopyingEventArgs e)
        {
            if (SelectionLength == 0)
            {

            }
        }
        #endregion

        public bool IPV6 { get; set; }

        public static readonly DependencyProperty IsValidAddressProperty =
            DependencyProperty.Register(
                "IsValidAddress", typeof(bool),
                typeof(IpAddressControl));
        public bool IsValidAddress => IPAddress.TryParse(Text, out _);

        public static readonly DependencyProperty IPAddressProperty =
            DependencyProperty.Register(
                "IPAddress", typeof(IPAddress),
                typeof(IpAddressControl));
        public IPAddress IPAddress
        {
            get
            {
                if (!IPAddress.TryParse(Text, out IPAddress address))
                {
                    address = new IPAddress(0);
                }
                return address;
            }
            set
            {
                Set(value.ToString());
            }
        }

        private string CorrectedAddress
        {
            // We're going to lean on the functionality that's already there to
            // make sure we have a valid address. However, it's possible to have
            // an incomplete address, so we'll fill in a few cases of that here.
            get
            {
                string address = Text;
                int separators = Text.Count(t => t == Separator);
                if (separators < 3)
                {
                    address += new string(Separator, 3 - separators);
                }
                if (IPV6)
                {
                    address = address.Replace("..", ".0.");
                }

                return address;
            }
        }

        private class AddressComponent
        {
            private int _index;
            private IpAddressControl _control;
            private int Cursor => _index == -1 ? CaretIndex : _index;
            private int CaretIndex
            {
                get => _control?.CaretIndex ?? -1;
                set => _control.CaretIndex = value;
            }
            private string Text
            {
                get => _control?.Text ?? string.Empty;
                set => _control.SetText(value);
            }
            private int SelectionLength => _control?.SelectionLength ?? -1;
            private bool IPV6 => _control?.IPV6 ?? false;

            public AddressComponent(IpAddressControl control, int index = -1)
            {
                _control = control;
                _index = index;
            }
            public int Start
            {
                get
                {
                    int start = Cursor;
                    while (start > 0 && _control.IsDigit(Text[start - 1])) --start;
                    return start;
                }
            }

            public int Length
            {
                get
                {
                    int length = 0;
                    while (Start + length < Text.Length && _control.IsDigit(Start + length)) ++length;
                    return length;
                }
            }

            public void Normalize()
            {
                StringBuilder sb = new StringBuilder();
                int value = 0;
                foreach (char c in ToString())
                {
                    sb.Append(c);
                    int newValue = IPV6
                        ? int.Parse(sb.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                        : int.Parse(sb.ToString());
                    if (IPV6) int.TryParse(sb.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out newValue);
                    else int.TryParse(sb.ToString(), out newValue);
                    if (newValue > (IPV6 ? 0xffff : 0xff))
                    {
                        sb.Remove(sb.Length - 1, 1);
                        break;
                    }
                    value = newValue;
                }
                int cursor = CaretIndex;
                string newText = Text.Substring(0, Start)
                    + (IPV6 ? value.ToString("X") : value.ToString())
                    + Text.Substring(Start + Length, Text.Length - (Start + Length));
                if (newText != Text)
                {
                    int newCursor = cursor > Start + Length
                        ? cursor + (newText.Length - Text.Length)
                        : cursor;
                    Text = newText;
                    _control._normalizing = true;
                    CaretIndex = newCursor;
                    _control._normalizing = false;
                }
            }

            public bool Insert(char digitChar)
            {
                // Save original value in case we have to revert.
                string originalText = Text;
                int currentCursor = CaretIndex;

                bool result = false;
                if (SelectionLength > 0)
                {
                    _control.HandleDeleteAndBackspace();
                }

                // Handle leading zeros.
                if (ToString().FirstOrDefault() == '0')
                {
                    result = true;
                    if (digitChar != '0')
                    {
                        Replace(digitChar, Start);
                    }
                    // I think given that a lot of people add leading zeros to hex,
                    // it would be best not to play the error sound here for IPV6.
                    else if (!IPV6)
                    {
                        _control.Error();
                    }
                }
                // With hex, we can just look at string length.
                else if (IPV6 && ToString().Length == 4)
                {
                    // So, result is already false, meaning try to add a separator
                    // and continue after.
                    // If the cursor is set before the end of the component or
                    // the entire string length is more than one character past
                    // the end of the component (one character would be a separator)
                    // then we throw an error and return true.
                    if (Cursor < Start + Length || Text.Length > Start + Length + 1)
                    {
                        _control.Error();
                        result = true;
                    }
                    // Default case 
                }
                else
                {
                    int currentValue;
                    string newValue;
                    newValue = Text.Substring(Start, currentCursor - Start)
                        + digitChar
                        + Text.Substring(currentCursor, (Start + Length) - currentCursor);
                    if (IPV6)
                    {
                        int.TryParse(newValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out currentValue);
                    }
                    else
                    {
                        int.TryParse(newValue, out currentValue);
                    }
                    if (currentValue <= (IPV6 ? 0xffff : 255))
                    {
                        InsertChar(digitChar);
                        result = true;
                    }
                    else if (Cursor < Length)
                    {
                        _control.Error();
                        Text = originalText;
                        result = true;
                    }
                }
                return result;

                void InsertChar(char c)
                {
                    int caretIndex = CaretIndex;
                    Text = Text.Insert(caretIndex, c.ToString());
                    CaretIndex = caretIndex + 1;
                }
            }

            public void Replace(char c, int index = -1)
            {
                int caretIndex = CaretIndex;
                var sb = new StringBuilder(Text);
                int position = index == -1 ? caretIndex : index;
                sb.Remove(position, 1);
                sb.Insert(position, c);
                Text = sb.ToString();
                CaretIndex = caretIndex;
            }

            public override string ToString()
            {
                return Text.Substring(Start, Length);
            }
        }
    }
}
