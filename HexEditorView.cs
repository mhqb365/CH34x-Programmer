using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ch34xProgrammer;

public sealed class HexEditorView : FrameworkElement
{
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(HexEditorView),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(HexEditorView),
            new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    private const int BytesPerLine = 16;
    private const double LeftPad = 10;
    private const double HeaderHeight = 0;
    private const double LineHeight = 18;
    private const double AddressX = 10;
    private const double HexX = 98;
    private const double PreferredAsciiX = 506;
    private const double ByteCellWidth = 24;
    private const double CharCellWidth = 8;

    private byte[] _buffer = [];
    private Action<int, byte>? _byteChanged;
    private int _firstLine;
    private int _selectedOffset;
    private int _selectionAnchor;
    private int _selectionEnd;
    private bool _asciiEdit;
    private int _pendingNibble = -1;
    private bool _isSelecting;
    private readonly Stack<EditBatch> _undo = [];
    private readonly Stack<EditBatch> _redo = [];
    private readonly HashSet<int> _editedOffsets = [];

    public HexEditorView()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public void SetBuffer(byte[] buffer, Action<int, byte> byteChanged)
    {
        _buffer = buffer;
        _byteChanged = byteChanged;
        _firstLine = 0;
        _selectedOffset = 0;
        _selectionAnchor = 0;
        _selectionEnd = 0;
        _undo.Clear();
        _redo.Clear();
        _editedOffsets.Clear();
        InvalidateVisual();
    }

    public int SelectedOffset => _selectedOffset;

    public int FirstLine => _firstLine;

    public int TotalLines => Math.Max(1, (_buffer.Length + BytesPerLine - 1) / BytesPerLine);

    public int VisibleLines => Math.Max(1, (int)((ActualHeight - HeaderHeight) / LineHeight));

    public event EventHandler? ScrollChanged;

    public void ScrollToOffset(int offset)
    {
        if (_buffer.Length == 0)
        {
            return;
        }

        _selectedOffset = Math.Clamp(offset, 0, _buffer.Length - 1);
        _selectionAnchor = _selectedOffset;
        _selectionEnd = _selectedOffset;
        var line = _selectedOffset / BytesPerLine;
        var visibleLines = VisibleLines;
        if (line < _firstLine || line >= _firstLine + visibleLines)
        {
            SetFirstLine(Math.Max(0, line - visibleLines / 2));
        }

        InvalidateVisual();
    }

    public void SelectRange(int offset, int length)
    {
        if (_buffer.Length == 0)
        {
            return;
        }

        _selectedOffset = Math.Clamp(offset, 0, _buffer.Length - 1);
        _selectionAnchor = _selectedOffset;
        _selectionEnd = Math.Clamp(_selectedOffset + Math.Max(1, length) - 1, 0, _buffer.Length - 1);
        var line = _selectedOffset / BytesPerLine;
        var visibleLines = VisibleLines;
        if (line < _firstLine || line >= _firstLine + visibleLines)
        {
            SetFirstLine(Math.Max(0, line - visibleLines / 2));
        }

        InvalidateVisual();
    }

    public void SetFirstLine(int line)
    {
        var maxFirstLine = Math.Max(0, TotalLines - VisibleLines);
        var next = Math.Clamp(line, 0, maxFirstLine);
        if (_firstLine == next)
        {
            return;
        }

        _firstLine = next;
        ScrollChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var background = Background ?? Brushes.White;
        var foreground = Foreground ?? Brushes.Black;
        var darkBackground = IsDarkBrush(background);
        var editorBackground = darkBackground ? background : new SolidColorBrush(Color.FromRgb(252, 253, 255));
        var muted = new SolidColorBrush(darkBackground ? Color.FromRgb(90, 170, 210) : Color.FromRgb(0, 0, 205));
        var selection = new SolidColorBrush(darkBackground ? Color.FromRgb(64, 90, 120) : Color.FromRgb(190, 232, 248));
        var linkedSelection = new SolidColorBrush(darkBackground ? Color.FromArgb(120, 64, 90, 120) : Color.FromRgb(226, 246, 252));
        var editedBrush = new SolidColorBrush(darkBackground ? Color.FromRgb(255, 128, 128) : Color.FromRgb(255, 64, 64));
        var linkedEditedBrush = new SolidColorBrush(darkBackground ? Color.FromRgb(210, 110, 110) : Color.FromRgb(235, 95, 95));
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        dc.DrawRectangle(editorBackground, null, new Rect(0, 0, ActualWidth, ActualHeight));
        var asciiX = GetAsciiX();
        var selectionStart = Math.Min(_selectionAnchor, _selectionEnd);
        var selectionEnd = Math.Max(_selectionAnchor, _selectionEnd);

        var visibleLines = VisibleLines;
        for (var row = 0; row < visibleLines; row++)
        {
            var offset = (_firstLine + row) * BytesPerLine;
            if (offset >= _buffer.Length)
            {
                break;
            }

            var y = HeaderHeight + row * LineHeight;
            DrawText(dc, $"{offset:X8}", AddressX, y, muted);

            for (var i = 0; i < BytesPerLine && offset + i < _buffer.Length; i++)
            {
                var byteOffset = offset + i;
                var x = HexX + i * ByteCellWidth;
                if (byteOffset >= selectionStart && byteOffset <= selectionEnd)
                {
                    dc.DrawRectangle(_asciiEdit ? linkedSelection : selection, null, new Rect(x - 2, y, 20, LineHeight));
                }

                var text = _buffer[byteOffset].ToString("X2");
                var brush = _editedOffsets.Contains(byteOffset) ? editedBrush : foreground;
                if (!_asciiEdit && _pendingNibble >= 0 && byteOffset == _selectedOffset)
                {
                    text = $"{_pendingNibble:X1}_";
                    brush = editedBrush;
                }

                DrawText(dc, text, x, y, brush);
            }

            for (var i = 0; i < BytesPerLine && offset + i < _buffer.Length; i++)
            {
                var byteOffset = offset + i;
                var x = asciiX + i * CharCellWidth;
                if (byteOffset >= selectionStart && byteOffset <= selectionEnd)
                {
                    dc.DrawRectangle(_asciiEdit ? selection : linkedSelection, null, new Rect(x - 1, y, CharCellWidth, LineHeight));
                }

                var b = _buffer[byteOffset];
                var brush = _editedOffsets.Contains(byteOffset) ? linkedEditedBrush : foreground;
                DrawText(dc, b is >= 32 and <= 126 ? ((char)b).ToString() : ".", x, y, brush);
            }
        }
        dc.Pop();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus();
        var p = e.GetPosition(this);
        if (!TryHitTestOffset(p, out var offset, out var ascii))
        {
            return;
        }

        _asciiEdit = ascii;
        _pendingNibble = -1;
        _selectedOffset = offset;
        _selectionAnchor = offset;
        _selectionEnd = offset;
        _isSelecting = true;
        CaptureMouse();
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        if (TryHitTestOffset(e.GetPosition(this), out var offset, out _))
        {
            _selectedOffset = offset;
            _selectionEnd = offset;
            InvalidateVisual();
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        _isSelecting = false;
        ReleaseMouseCapture();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        SetFirstLine(_firstLine - e.Delta / 120 * 3);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_buffer.Length == 0)
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (e.Key == Key.C)
            {
                CopySelection();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.V)
            {
                PasteClipboard();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Y || e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                Redo();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Z)
            {
                Undo();
                e.Handled = true;
                return;
            }
        }

        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            MoveSelection(e.Key);
            e.Handled = true;
            return;
        }

        var ch = KeyToChar(e);
        if (ch is null)
        {
            return;
        }

        if (_asciiEdit)
        {
            SetByte(_selectedOffset, (byte)ch.Value);
            MoveBy(1);
            e.Handled = true;
            return;
        }

        var nibble = HexValue(ch.Value);
        if (nibble < 0)
        {
            return;
        }

        if (_pendingNibble < 0)
        {
            _pendingNibble = nibble;
            InvalidateVisual();
        }
        else
        {
            SetByte(_selectedOffset, (byte)((_pendingNibble << 4) | nibble));
            _pendingNibble = -1;
            MoveBy(1);
        }

        e.Handled = true;
    }

    private void CopySelection()
    {
        var start = Math.Min(_selectionAnchor, _selectionEnd);
        var end = Math.Max(_selectionAnchor, _selectionEnd);
        if ((uint)start >= _buffer.Length || end < start)
        {
            return;
        }

        end = Math.Min(end, _buffer.Length - 1);
        var text = _asciiEdit
            ? new string(_buffer[start..(end + 1)].Select(b => b is >= 32 and <= 126 ? (char)b : '.').ToArray())
            : string.Join(" ", _buffer[start..(end + 1)].Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
        Clipboard.SetText(text);
    }

    private void PasteClipboard()
    {
        if (!Clipboard.ContainsText())
        {
            return;
        }

        var text = Clipboard.GetText();
        var bytes = _asciiEdit ? TextToBytes(text) : TryParseHexBytes(text);
        if (bytes.Length == 0)
        {
            return;
        }

        var count = Math.Min(bytes.Length, _buffer.Length - _selectedOffset);
        if (count <= 0)
        {
            return;
        }

        var edits = new List<ByteEdit>(count);
        for (var i = 0; i < count; i++)
        {
            AddByteEdit(_selectedOffset + i, bytes[i], edits);
        }

        if (edits.Count > 0)
        {
            _undo.Push(new EditBatch(edits.ToArray()));
            _redo.Clear();
            InvalidateVisual();
        }

        _pendingNibble = -1;
        ScrollToOffset(_selectedOffset + count - 1);
    }

    private void MoveSelection(Key key)
    {
        var delta = key switch
        {
            Key.Left => -1,
            Key.Right => 1,
            Key.Up => -BytesPerLine,
            Key.Down => BytesPerLine,
            _ => 0
        };
        MoveBy(delta);
    }

    private void MoveBy(int delta)
    {
        ScrollToOffset(Math.Clamp(_selectedOffset + delta, 0, Math.Max(0, _buffer.Length - 1)));
        _selectionAnchor = _selectedOffset;
        _selectionEnd = _selectedOffset;
    }

    private void SetByte(int offset, byte value)
    {
        var oldValue = _buffer[offset];
        if (oldValue == value)
        {
            return;
        }

        _buffer[offset] = value;
        _editedOffsets.Add(offset);
        _undo.Push(new EditBatch([new ByteEdit(offset, oldValue, value)]));
        _redo.Clear();
        _byteChanged?.Invoke(offset, value);
        InvalidateVisual();
    }

    private void Undo()
    {
        if (!_undo.TryPop(out var batch))
        {
            return;
        }

        foreach (var edit in batch.Edits.Reverse())
        {
            _buffer[edit.Offset] = edit.OldValue;
            _editedOffsets.Remove(edit.Offset);
            _byteChanged?.Invoke(edit.Offset, edit.OldValue);
        }

        _redo.Push(batch);
        ScrollToOffset(batch.Edits[0].Offset);
    }

    private void Redo()
    {
        if (!_redo.TryPop(out var batch))
        {
            return;
        }

        foreach (var edit in batch.Edits)
        {
            _buffer[edit.Offset] = edit.NewValue;
            _editedOffsets.Add(edit.Offset);
            _byteChanged?.Invoke(edit.Offset, edit.NewValue);
        }

        _undo.Push(batch);
        ScrollToOffset(batch.Edits[^1].Offset);
    }

    private void AddByteEdit(int offset, byte value, ICollection<ByteEdit> edits)
    {
        var oldValue = _buffer[offset];
        if (oldValue == value)
        {
            return;
        }

        _buffer[offset] = value;
        _editedOffsets.Add(offset);
        edits.Add(new ByteEdit(offset, oldValue, value));
        _byteChanged?.Invoke(offset, value);
    }

    private bool TryHitTestOffset(Point p, out int offset, out bool ascii)
    {
        offset = 0;
        ascii = false;
        var row = (int)((p.Y - HeaderHeight) / LineHeight);
        if (row < 0)
        {
            return false;
        }

        var asciiX = GetAsciiX();
        var lineOffset = (_firstLine + row) * BytesPerLine;
        var byteIndex = p.X >= asciiX
            ? (int)((p.X - asciiX) / CharCellWidth)
            : (int)((p.X - HexX) / ByteCellWidth);
        if (byteIndex < 0 || byteIndex >= BytesPerLine)
        {
            return false;
        }

        offset = lineOffset + byteIndex;
        ascii = p.X >= asciiX;
        return (uint)offset < _buffer.Length;
    }

    private static void DrawText(DrawingContext dc, string text, double x, double y, Brush brush)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Consolas"), 12, brush, 1.0);
        dc.DrawText(formatted, new Point(x, y));
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        SetFirstLine(_firstLine);
        ScrollChanged?.Invoke(this, EventArgs.Empty);
    }

    private double GetAsciiX() => Math.Min(PreferredAsciiX, Math.Max(HexX + BytesPerLine * ByteCellWidth + 24, ActualWidth - 150));

    private static int HexValue(char ch) => ch switch
    {
        >= '0' and <= '9' => ch - '0',
        >= 'a' and <= 'f' => ch - 'a' + 10,
        >= 'A' and <= 'F' => ch - 'A' + 10,
        _ => -1
    };

    private static byte[] TextToBytes(string text) =>
        text.Select(ch => ch <= byte.MaxValue ? (byte)ch : (byte)'?').ToArray();

    private static byte[] TryParseHexBytes(string text)
    {
        var hex = text.Where(ch => HexValue(ch) >= 0).ToArray();
        if (hex.Length == 0 || hex.Length % 2 != 0)
        {
            return [];
        }

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((HexValue(hex[i * 2]) << 4) | HexValue(hex[i * 2 + 1]));
        }

        return bytes;
    }

    private static bool IsDarkBrush(Brush brush)
    {
        if (brush is not SolidColorBrush solid)
        {
            return false;
        }

        var color = solid.Color;
        var luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;
        return luminance < 0.45;
    }

    private static char? KeyToChar(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is >= Key.A and <= Key.Z)
        {
            var c = (char)('A' + key - Key.A);
            return Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? c : char.ToLowerInvariant(c);
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return (char)('0' + key - Key.D0);
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return (char)('0' + key - Key.NumPad0);
        }

        return key switch
        {
            Key.Space => ' ',
            Key.OemPeriod => '.',
            Key.OemMinus => '-',
            _ => null
        };
    }

    public Brush? Background
    {
        get => (Brush?)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Brush? Foreground
    {
        get => (Brush?)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    private sealed record EditBatch(ByteEdit[] Edits);

    private readonly record struct ByteEdit(int Offset, byte OldValue, byte NewValue);
}
